using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>Pinned Lucide artwork, bundled locally and tinted by the native UI theme.</summary>
    public sealed class WorkbenchIcon : VisualElement
    {
        private static readonly Dictionary<string,string> Names=new Dictionary<string,string> {
            {"home","house"},{"person","user"},{"auto","zap"},{"loop","repeat"},{"shuffle","shuffle"},
            {"play","play"},{"sequence","list-ordered"},{"system","monitor"},{"motion","move-right"},
            {"sky","sun"},{"swarm","network"},{"file","file-text"},{"plus","plus"},{"copy","copy"},
            {"trash","trash"},{"grip","grip-vertical"},{"settings","sliders-horizontal"},{"stop","square"},
            {"camera","camera"},{"radio","radio"},{"pause","pause"}
        };
        private static readonly Dictionary<string,Texture2D> Textures=new Dictionary<string,Texture2D>();
        private readonly Texture2D texture;
        public WorkbenchIcon(string kind)
        {
            AddToClassList("studio-icon");pickingMode=PickingMode.Ignore;
            string name=Names.TryGetValue(kind??"",out string mapped)?mapped:"circle-dot";
            if(!Textures.TryGetValue(name,out texture)) { texture=Resources.Load<Texture2D>("ConfigurationUI/Icons/"+name);Textures[name]=texture; }
            generateVisualContent+=Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            if(texture==null || contentRect.width<=0 || contentRect.height<=0) return;
            float size=Mathf.Min(contentRect.width,contentRect.height),x=contentRect.x+(contentRect.width-size)/2,y=contentRect.y+(contentRect.height-size)/2;
            var mesh=context.Allocate(4,6,texture);var uv=mesh.uvRegion;Color32 tint=resolvedStyle.color;
            mesh.SetNextVertex(new Vertex {position=new Vector3(x,y,Vertex.nearZ),tint=tint,uv=new Vector2(uv.xMin,uv.yMax)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(x+size,y,Vertex.nearZ),tint=tint,uv=new Vector2(uv.xMax,uv.yMax)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(x+size,y+size,Vertex.nearZ),tint=tint,uv=new Vector2(uv.xMax,uv.yMin)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(x,y+size,Vertex.nearZ),tint=tint,uv=new Vector2(uv.xMin,uv.yMin)});
            mesh.SetNextIndex(0);mesh.SetNextIndex(1);mesh.SetNextIndex(2);mesh.SetNextIndex(2);mesh.SetNextIndex(3);mesh.SetNextIndex(0);
        }
    }
}
