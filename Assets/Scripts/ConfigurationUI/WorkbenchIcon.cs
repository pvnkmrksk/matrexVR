using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>Pinned Lucide artwork, bundled locally and tinted by the native UI theme.</summary>
    public sealed class WorkbenchIcon : VisualElement
    {
        private static readonly Dictionary<string,string> Names=new Dictionary<string,string> {
            {"visibility","eye"},{"save","save"},{"palette","palette"},{"image","image"},{"object","box"},{"code","code-xml"},{"reset","refresh-cw"},{"undo","rotate-ccw"},{"redo","rotate-ccw"},{"up","chevron-down"},{"down","chevron-down"},{"refresh","refresh-cw"},{"overview","layout-dashboard"},{"more","ellipsis"},{"power","power"},{"back","arrow-left"},{"home","house"},{"person","user"},{"auto","zap"},{"loop","repeat"},{"shuffle","shuffle"},
            {"play","play"},{"sequence","list-ordered"},{"system","monitor"},{"motion","move-right"},
            {"sky","sun"},{"swarm","network"},{"file","file-text"},{"plus","plus"},{"copy","copy"},
            {"trash","trash"},{"grip","grip-vertical"},{"settings","sliders-horizontal"},{"stop","square"},
            {"camera","camera"},{"radio","radio"},{"pause","pause"},{"navigation","panel-left"},
            {"flying","bird"},{"walking","footprints"},{"forest","trees"},{"night","moon-star"},{"help","circle-help"},{"external","external-link"},{"next","chevron-right"}
        };
        private static readonly Dictionary<string,Texture2D> Textures=new Dictionary<string,Texture2D>();
        private readonly Texture2D texture;
        private readonly string vectorKind;
        private readonly bool mirror,upsideDown;
        public WorkbenchIcon(string kind)
        {
            AddToClassList("studio-icon");pickingMode=PickingMode.Ignore;
            mirror=kind=="redo";upsideDown=kind=="up";
            if(kind=="led-panels"||kind=="monitor-array")
            {vectorKind=kind;generateVisualContent+=Draw;return;}
            string name=Names.TryGetValue(kind??"",out string mapped)?mapped:"circle-dot";
            if(!Textures.TryGetValue(name,out texture)) { texture=Resources.Load<Texture2D>("ConfigurationUI/Icons/"+name);Textures[name]=texture; }
            generateVisualContent+=Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            if(vectorKind!=null){DrawOutputIcon(context);return;}
            if(texture==null || contentRect.width<=0 || contentRect.height<=0) return;
            float size=Mathf.Min(contentRect.width,contentRect.height),x=contentRect.x+(contentRect.width-size)/2,y=contentRect.y+(contentRect.height-size)/2;
            var mesh=context.Allocate(4,6,texture);Color32 tint=resolvedStyle.color;
            mesh.SetNextVertex(new Vertex {position=new Vector3(x,y,Vertex.nearZ),tint=tint,uv=new Vector2(mirror?1:0,upsideDown?0:1)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(x+size,y,Vertex.nearZ),tint=tint,uv=new Vector2(mirror?0:1,upsideDown?0:1)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(x+size,y+size,Vertex.nearZ),tint=tint,uv=new Vector2(mirror?0:1,upsideDown?1:0)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(x,y+size,Vertex.nearZ),tint=tint,uv=new Vector2(mirror?1:0,upsideDown?1:0)});
            mesh.SetNextIndex(0);mesh.SetNextIndex(1);mesh.SetNextIndex(2);mesh.SetNextIndex(2);mesh.SetNextIndex(3);mesh.SetNextIndex(0);
        }
        private void DrawOutputIcon(MeshGenerationContext context)
        {
            float size=Mathf.Min(contentRect.width,contentRect.height);
            if(size<=0)return;
            var painter=context.painter2D;painter.lineWidth=Mathf.Max(1.25f,size/20);painter.strokeColor=resolvedStyle.color;
            float x=contentRect.x+(contentRect.width-size)/2,y=contentRect.y+(contentRect.height-size)/2;
            System.Action<Vector2[]> path=points=>{painter.BeginPath();painter.MoveTo(new Vector2(x+points[0].x*size,y+points[0].y*size));for(int i=1;i<points.Length;i++)painter.LineTo(new Vector2(x+points[i].x*size,y+points[i].y*size));painter.ClosePath();painter.Stroke();};
            if(vectorKind=="led-panels")
            {
                path(new[]{new Vector2(.08f,.08f),new Vector2(.92f,.08f),new Vector2(.92f,.92f),new Vector2(.08f,.92f)});
                for(int row=0;row<4;row++)for(int column=0;column<4;column++)
                {
                    float left=.2f+column*.17f,top=.2f+row*.17f;
                    path(new[]{new Vector2(left,top),new Vector2(left+.07f,top),new Vector2(left+.07f,top+.07f),new Vector2(left,top+.07f)});
                }
            }
            else
            {
                // Three screens angled inward, sharing the same Lucide-like stroke.
                path(new[]{new Vector2(.03f,.18f),new Vector2(.29f,.26f),new Vector2(.29f,.73f),new Vector2(.03f,.64f)});
                path(new[]{new Vector2(.35f,.27f),new Vector2(.65f,.27f),new Vector2(.65f,.75f),new Vector2(.35f,.75f)});
                path(new[]{new Vector2(.71f,.26f),new Vector2(.97f,.18f),new Vector2(.97f,.64f),new Vector2(.71f,.73f)});
                painter.BeginPath();painter.MoveTo(new Vector2(x+.5f*size,y+.75f*size));painter.LineTo(new Vector2(x+.5f*size,y+.9f*size));painter.Stroke();
                painter.BeginPath();painter.MoveTo(new Vector2(x+.35f*size,y+.9f*size));painter.LineTo(new Vector2(x+.65f*size,y+.9f*size));painter.Stroke();
            }
        }
    }
}
