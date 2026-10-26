using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

/// <summary>Indexed intra-frame AVI playback for tracker recordings, independent of OS video codecs.</summary>
public sealed class ReplayAviFrameSource : IDisposable
{
    public enum PixelEncoding { Jpeg, Png, Gray8, Bgr24, Bgra32 }
    public sealed class Frame
    {
        public byte[] Bytes { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public int RowStride { get; internal set; }
        public bool BottomUp { get; internal set; }
        public PixelEncoding Encoding { get; internal set; }
    }
    private struct Location { public long offset; public int size; }
    private const int MaximumFrameBytes=64*1024*1024,MaximumPixels=16*1024*1024,MaximumFrames=2000000;
    private readonly string path;
    private readonly long fileLength,lastWriteTicks;
    private readonly List<Location> locations=new List<Location>();
    private int width,height,bits,streamCount,videoStream=-1,disposed;
    private bool bottomUp;
    private string codec;
    private double fps;
    public int FrameCount=>locations.Count;
    public int Width=>width;
    public int Height=>height;
    public double NominalFramesPerSecond=>fps;
    public string Codec=>codec;
    private ReplayAviFrameSource(string source)
    {
        path=Path.GetFullPath(source);var info=new FileInfo(path);fileLength=info.Length;lastWriteTicks=info.LastWriteTimeUtc.Ticks;
    }
    public static ReplayAviFrameSource Open(string source,CancellationToken token=default)
    {
        var result=new ReplayAviFrameSource(source);
        using(var stream=File.Open(source,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var reader=new BinaryReader(stream,Encoding.ASCII,true))
        {
            if(stream.Length<12 || Tag(reader)!="RIFF")throw new InvalidDataException("Not a RIFF AVI recording.");
            stream.Position=0;result.Scan(reader,stream.Length,false,-1,0,token);
        }
        if(result.videoStream<0 || result.locations.Count==0)throw new InvalidDataException("The AVI has no complete video frames.");
        if(result.width<=0 || result.height<=0 || result.width>16384 || result.height>16384 || (long)result.width*result.height>MaximumPixels)
            throw new InvalidDataException("Tracker video dimensions exceed the playback limit (16 megapixels).");
        result.ResolveEncoding();return result;
    }
    private static string Tag(BinaryReader reader)=>Encoding.ASCII.GetString(reader.ReadBytes(4));
    private void Scan(BinaryReader reader,long end,bool inMovie,int streamIndex,int depth,CancellationToken token)
    {
        if(depth>16)throw new InvalidDataException("AVI container nesting is invalid.");
        var input=reader.BaseStream;
        while(input.Position+8<=end)
        {
            token.ThrowIfCancellationRequested();string tag=Tag(reader);uint size=reader.ReadUInt32();long start=input.Position,limit=start+size;
            if(limit>end || limit>fileLength)throw new InvalidDataException("The AVI contains an unfinished or truncated chunk.");
            if(tag=="RIFF" || tag=="LIST")
            {
                if(size<4)throw new InvalidDataException("AVI container has no type.");
                string type=Tag(reader);
                if(tag=="RIFF" && type!="AVI " && type!="AVIX")throw new InvalidDataException("Unsupported RIFF video container.");
                int childStream=type=="strl"?streamCount++:streamIndex;
                if(tag=="RIFF" || type=="hdrl" || type=="strl" || type=="movi" || type=="rec ")
                    Scan(reader,limit,inMovie || type=="movi",childStream,depth+1,token);
            }
            else if(tag=="avih" && size>=40)
            {
                uint micros=reader.ReadUInt32();if(micros>0)fps=1000000d/micros;
                input.Position=start+32;width=reader.ReadInt32();height=Math.Abs(reader.ReadInt32());
            }
            else if(tag=="strh" && size>=28)
            {
                string type=Tag(reader),handler=Tag(reader);
                if(type=="vids")
                {
                    if(videoStream>=0 && videoStream!=streamIndex)throw new InvalidDataException("Choose a tracker AVI containing one video stream.");
                    videoStream=streamIndex;codec=handler.TrimEnd('\0',' ');
                    input.Position=start+20;uint scale=reader.ReadUInt32(),rate=reader.ReadUInt32();if(scale>0 && rate>0)fps=rate/(double)scale;
                }
            }
            else if(tag=="strf" && streamIndex==videoStream && size>=40)
            {
                uint header=reader.ReadUInt32();if(header<40 || header>size)throw new InvalidDataException("Invalid AVI image header.");
                width=reader.ReadInt32();int signedHeight=reader.ReadInt32();if(signedHeight==int.MinValue)throw new InvalidDataException("Invalid AVI height.");
                height=Math.Abs(signedHeight);bottomUp=signedHeight>0;reader.ReadUInt16();bits=reader.ReadUInt16();
                byte[] compression=reader.ReadBytes(4);uint code=BitConverter.ToUInt32(compression,0);
                codec=code==0?"DIB":Encoding.ASCII.GetString(compression).TrimEnd('\0',' ');
            }
            else if(inMovie && tag.Length==4 && tag.StartsWith(videoStream.ToString("00"),StringComparison.Ordinal) && (tag.EndsWith("dc",StringComparison.Ordinal) || tag.EndsWith("db",StringComparison.Ordinal)))
            {
                if(size==0 || size>MaximumFrameBytes)throw new InvalidDataException("AVI frame exceeds the 64 MB playback limit.");
                if(locations.Count>=MaximumFrames)throw new InvalidDataException("AVI segment exceeds the two-million-frame playback limit.");
                locations.Add(new Location{offset=start,size=(int)size});
            }
            long next=limit+(size&1);if(next>end)throw new InvalidDataException("The AVI is missing its chunk padding.");
            input.Position=next;
        }
        if(input.Position!=end)throw new InvalidDataException("The AVI contains an incomplete chunk header.");
    }
    private PixelEncoding ResolveEncoding()
    {
        switch((codec??"").ToUpperInvariant())
        {
            case "MJPG":case "JPEG":return PixelEncoding.Jpeg;
            case "MPNG":case "PNG":return PixelEncoding.Png;
            case "Y800":case "GREY":return PixelEncoding.Gray8;
            case "DIB":if(bits==24)return PixelEncoding.Bgr24;if(bits==32)return PixelEncoding.Bgra32;break;
        }
        throw new InvalidDataException("Unsupported tracker AVI codec: "+codec+". Use MJPEG, PNG, raw Y800, or a platform-supported video proxy.");
    }
    public Frame ReadFrame(long index,CancellationToken token=default)
    {
        if(Volatile.Read(ref disposed)!=0)throw new ObjectDisposedException(nameof(ReplayAviFrameSource));
        if(index<0 || index>=locations.Count)throw new ArgumentOutOfRangeException(nameof(index));
        token.ThrowIfCancellationRequested();var info=new FileInfo(path);
        if(info.Length!=fileLength || info.LastWriteTimeUtc.Ticks!=lastWriteTicks)throw new IOException("The archived tracker video changed during playback.");
        Location entry=locations[(int)index];byte[] data=new byte[entry.size];
        using(var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            stream.Position=entry.offset;int offset=0;
            while(offset<data.Length){token.ThrowIfCancellationRequested();int count=stream.Read(data,offset,data.Length-offset);if(count==0)throw new EndOfStreamException("Truncated tracker frame.");offset+=count;}
        }
        PixelEncoding encoding=ResolveEncoding();int stride=0;
        if(encoding==PixelEncoding.Jpeg || encoding==PixelEncoding.Png)
        {
            ImageDimensions(data,encoding,out int imageWidth,out int imageHeight);
            if(imageWidth!=width || imageHeight!=height)throw new InvalidDataException("Encoded frame dimensions do not match the AVI header.");
        }
        else
        {
            stride=encoding==PixelEncoding.Gray8?width:checked(((width*(encoding==PixelEncoding.Bgr24?3:4))+3)&~3);
            if((long)stride*height!=data.Length)throw new InvalidDataException("Raw AVI frame length does not match its dimensions.");
        }
        // Native tracker Y800 payloads retain top-to-bottom camera luma rows;
        // only Windows bitmap formats use the signed DIB height convention.
        return new Frame{Bytes=data,Width=width,Height=height,RowStride=stride,Encoding=encoding,BottomUp=(encoding==PixelEncoding.Bgr24 || encoding==PixelEncoding.Bgra32) && bottomUp};
    }
    private static void ImageDimensions(byte[] data,PixelEncoding encoding,out int width,out int height)
    {
        width=height=0;
        if(encoding==PixelEncoding.Png)
        {
            byte[] signature={137,80,78,71,13,10,26,10};
            if(data.Length<24)throw new InvalidDataException("Truncated PNG tracker frame.");
            for(int i=0;i<8;i++)if(data[i]!=signature[i])throw new InvalidDataException("Invalid PNG tracker frame.");
            if(data[12]!=73 || data[13]!=72 || data[14]!=68 || data[15]!=82)throw new InvalidDataException("PNG dimensions are absent.");
            width=checked((int)((uint)data[16]<<24 | (uint)data[17]<<16 | (uint)data[18]<<8 | data[19]));
            height=checked((int)((uint)data[20]<<24 | (uint)data[21]<<16 | (uint)data[22]<<8 | data[23]));return;
        }
        if(data.Length<4 || data[0]!=255 || data[1]!=216)throw new InvalidDataException("Invalid JPEG tracker frame.");
        int p=2;
        while(p+3<data.Length)
        {
            if(data[p++]!=255)throw new InvalidDataException("Invalid JPEG frame marker.");
            while(p<data.Length && data[p]==255)p++;
            if(p>=data.Length)break;int marker=data[p++];
            if(marker==0xd9 || marker==0xda)break;
            if(marker==0x01 || (marker>=0xd0 && marker<=0xd7))continue;
            if(p+2>data.Length)break;int size=(data[p]<<8)|data[p+1];
            if(size<2 || p+size>data.Length)throw new InvalidDataException("Truncated JPEG tracker frame.");
            bool sof=marker>=0xc0 && marker<=0xcf && marker!=0xc4 && marker!=0xc8 && marker!=0xcc;
            if(sof && size>=8){height=(data[p+3]<<8)|data[p+4];width=(data[p+5]<<8)|data[p+6];return;}
            p+=size;
        }
        throw new InvalidDataException("JPEG tracker frame dimensions are absent.");
    }
    public void Dispose()=>Interlocked.Exchange(ref disposed,1);
}
