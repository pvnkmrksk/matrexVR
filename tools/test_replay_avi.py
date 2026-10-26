#!/usr/bin/env python3
"""Exercise the actual pure AVI indexer with bounded, malformed and multi-chunk fixtures.
Image decoding and presentation require the native Unity player test separately.
"""
from pathlib import Path
import os
import re
import shutil
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[1]
PROGRAM=r'''
using System;using System.IO;using System.Linq;using System.Text;using System.Threading;
static class Regression {
 static int checks,serial;static string root;
 static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static void Reject<T>(Action operation,string label) where T:Exception {checks++;try{operation();}catch(T){return;}throw new Exception(label);}
 static byte[] Bytes(Action<BinaryWriter> write){using(var stream=new MemoryStream()){using(var output=new BinaryWriter(stream,Encoding.ASCII,true))write(output);return stream.ToArray();}}
 static byte[] Join(params byte[][] arrays)=>arrays.SelectMany(a=>a).ToArray();
 static byte[] Chunk(string tag,byte[] payload)=>Bytes(w=>{w.Write(Encoding.ASCII.GetBytes(tag));w.Write((uint)payload.Length);w.Write(payload);if(payload.Length%2!=0)w.Write((byte)0);});
 static byte[] List(string kind,params byte[][] chunks)=>Chunk("LIST",Join(new[]{Encoding.ASCII.GetBytes(kind)}.Concat(chunks).ToArray()));
 static byte[] Riff(params byte[][] chunks)=>Chunk("RIFF",Join(new[]{Encoding.ASCII.GetBytes("AVI ")}.Concat(chunks).ToArray()));
 static byte[] Header(string codec,int width,int height,int frames,int bits=8,bool audio=false){
  var avih=new byte[56];BitConverter.GetBytes(33333).CopyTo(avih,0);BitConverter.GetBytes(frames).CopyTo(avih,16);BitConverter.GetBytes(width).CopyTo(avih,32);BitConverter.GetBytes(Math.Abs(height)).CopyTo(avih,36);
  var strh=new byte[56];Encoding.ASCII.GetBytes("vids").CopyTo(strh,0);Encoding.ASCII.GetBytes(codec.PadRight(4)).CopyTo(strh,4);BitConverter.GetBytes(1).CopyTo(strh,20);BitConverter.GetBytes(30).CopyTo(strh,24);
  var strf=Bytes(w=>{w.Write(40);w.Write(width);w.Write(height);w.Write((ushort)1);w.Write((ushort)bits);w.Write(codec=="DIB"?new byte[4]:Encoding.ASCII.GetBytes(codec.PadRight(4)));w.Write(new byte[20]);});
  byte[] video=List("strl",Chunk("strh",strh),Chunk("strf",strf));
  if(audio){var ah=new byte[56];Encoding.ASCII.GetBytes("auds").CopyTo(ah,0);return List("hdrl",Chunk("avih",avih),List("strl",Chunk("strh",ah)),video);}
  return List("hdrl",Chunk("avih",avih),video);
 }
 static byte[] Jpeg(int width,int height)=>new byte[]{255,216,255,192,0,11,8,(byte)(height>>8),(byte)height,(byte)(width>>8),(byte)width,1,1,17,0,255,217};
 static string Save(byte[] content){string path=Path.Combine(root,(++serial)+".avi");File.WriteAllBytes(path,content);return path;}
 static string Avi(string codec,byte[][] frames,int width=2,int height=2,int bits=8,bool audio=false)=>Save(Riff(Header(codec,width,height,frames.Length,bits,audio),List("movi",frames.Select(f=>Chunk(audio?"01dc":"00dc",f)).ToArray())));
 static void Main(){root=Path.Combine(Path.GetTempPath(),"matrex-avi-fixtures-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
  var jpeg=Jpeg(2,2);string path=Avi("MJPG",new[]{jpeg,jpeg});byte[] original=File.ReadAllBytes(path);
  using(var source=ReplayAviFrameSource.Open(path)){
   Check(source.FrameCount==2&&source.Width==2&&source.Height==2,"video dimensions/index count");Check(Math.Abs(source.NominalFramesPerSecond-30)<.00001,"rational stream FPS");
   var frame=source.ReadFrame(0);Check(frame.Encoding==ReplayAviFrameSource.PixelEncoding.Jpeg&&frame.Bytes.SequenceEqual(jpeg),"JPEG payload retained, including odd-size chunk padding");Check(source.ReadFrame(1).Bytes.SequenceEqual(jpeg),"last zero-based frame");
   Reject<ArgumentOutOfRangeException>(()=>source.ReadFrame(-1),"negative frame accepted");Reject<ArgumentOutOfRangeException>(()=>source.ReadFrame(2),"past-end frame accepted");
   var cancelled=new CancellationToken(true);Reject<OperationCanceledException>(()=>source.ReadFrame(0,cancelled),"cancelled frame read");
  }
  Check(File.ReadAllBytes(path).SequenceEqual(original),"indexing/reading changed source bytes");
  var disposed=ReplayAviFrameSource.Open(path);disposed.Dispose();Reject<ObjectDisposedException>(()=>disposed.ReadFrame(0),"read after dispose");
  Reject<OperationCanceledException>(()=>ReplayAviFrameSource.Open(path,new CancellationToken(true)),"cancelled indexing");
  byte[] png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/9l8AAAAASUVORK5CYII=");
  using(var p=ReplayAviFrameSource.Open(Avi("MPNG",new[]{png},1,1)))Check(p.ReadFrame(0).Encoding==ReplayAviFrameSource.PixelEncoding.Png,"PNG frame index");
  var raw=new byte[]{0,64,128,255};using(var p=ReplayAviFrameSource.Open(Avi("Y800",new[]{raw}))){var f=p.ReadFrame(0);Check(f.Encoding==ReplayAviFrameSource.PixelEncoding.Gray8&&f.RowStride==2&&!f.BottomUp&&f.Bytes.SequenceEqual(raw),"native Y800 top-to-bottom luma");}
  using(var p=ReplayAviFrameSource.Open(Avi("DIB",new[]{new byte[16]},2,2,24))){var f=p.ReadFrame(0);Check(f.Encoding==ReplayAviFrameSource.PixelEncoding.Bgr24&&f.RowStride==8&&f.BottomUp,"padded bottom-up BGR24");}
  using(var p=ReplayAviFrameSource.Open(Avi("DIB",new[]{new byte[16]},2,-2,32)))Check(!p.ReadFrame(0).BottomUp,"top-down BGRA32");
  Reject<InvalidDataException>(()=>ReplayAviFrameSource.Open(Avi("DIB",new[]{raw},2,2,8)),"palette video silently treated as luma");
  Reject<InvalidDataException>(()=>ReplayAviFrameSource.Open(Avi("H264",new[]{raw})),"unsupported predictive codec accepted");
  using(var p=ReplayAviFrameSource.Open(Avi("MJPG",new[]{Jpeg(3,2)})))Reject<InvalidDataException>(()=>p.ReadFrame(0),"encoded dimensions mismatch accepted");
  using(var p=ReplayAviFrameSource.Open(Avi("MJPG",new[]{new byte[]{255,216,255,217}})))Reject<InvalidDataException>(()=>p.ReadFrame(0),"JPEG without dimensions accepted");
  using(var p=ReplayAviFrameSource.Open(Avi("Y800",new[]{new byte[3]})))Reject<InvalidDataException>(()=>p.ReadFrame(0),"raw dimension mismatch accepted");
  string changed=Avi("Y800",new[]{raw});using(var p=ReplayAviFrameSource.Open(changed)){File.SetLastWriteTimeUtc(changed,DateTime.UtcNow.AddMinutes(-5));Reject<IOException>(()=>p.ReadFrame(0),"concurrent archive change accepted");}
  Reject<InvalidDataException>(()=>ReplayAviFrameSource.Open(Save(original.Take(original.Length-1).ToArray())),"truncated/padding chunk accepted");
  Reject<InvalidDataException>(()=>ReplayAviFrameSource.Open(Save(new byte[12])),"non-RIFF data accepted");
  Reject<InvalidDataException>(()=>ReplayAviFrameSource.Open(Save(Riff(Header("Y800",0,2,1),List("movi",Chunk("00db",raw))))),"invalid dimensions accepted");
  using(var p=ReplayAviFrameSource.Open(Avi("Y800",new[]{raw},2,2,8,true)))Check(p.FrameCount==1,"video after an audio stream must use its own chunk ID");
  var continuation=Chunk("RIFF",Join(Encoding.ASCII.GetBytes("AVIX"),List("movi",Chunk("00dc",jpeg))));
  using(var p=ReplayAviFrameSource.Open(Save(Join(original,continuation))))Check(p.FrameCount==3&&p.ReadFrame(2).Bytes.SequenceEqual(jpeg),"OpenDML continuation indexing");
  using(var p=ReplayAviFrameSource.Open(Save(Riff(Header("MJPG",2,2,1),Chunk("JUNK",new byte[]{1,2,3}),List("movi",List("rec ",Chunk("00dc",jpeg)))))))Check(p.FrameCount==1,"nested movie records and JUNK padding");
  Console.WriteLine("PASS: "+checks+" production AVI index, boundary, cancellation and source-preservation checks; no native image-decode claim.");
 }finally{Directory.Delete(root,true);}}
}
'''

def main():
    dotnet=shutil.which('dotnet')
    if not dotnet:raise SystemExit('A local .NET SDK is required.')
    version=max(map(int,re.findall(r'^(\d+)\.',subprocess.check_output([dotnet,'--list-sdks'],text=True),re.M)))
    with tempfile.TemporaryDirectory(prefix='matrex-avi-tests-') as temporary:
        folder=Path(temporary)
        shutil.copy2(ROOT/'Assets/Scripts/Replay/ReplayAviFrameSource.cs',folder/'ReplayAviFrameSource.cs')
        (folder/'Program.cs').write_text(PROGRAM)
        (folder/'Regression.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{version}.0</TargetFramework></PropertyGroup></Project>')
        (folder/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env=dict(os.environ,DOTNET_CLI_HOME=str(folder),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        built=subprocess.run([dotnet,'build',str(folder/'Regression.csproj'),'--configfile',str(folder/'NuGet.Config'),'-p:UseSharedCompilation=false','-v:q'],env=env,text=True,capture_output=True)
        if built.returncode:raise SystemExit(built.stdout+built.stderr)
        subprocess.run([dotnet,str(folder/f'bin/Debug/net{version}.0/Regression.dll')],env=env,check=True)

if __name__=='__main__':main()
