using UnityEngine;
using UnityEngine.UIElements;

namespace ConfigurationUI
{
    /// <summary>Small native vector icons; no font glyph or raster texture dependency.</summary>
    public sealed class WorkbenchIcon : VisualElement
    {
        private readonly string kind;
        public WorkbenchIcon(string kind) { this.kind=kind; AddToClassList("studio-icon"); pickingMode=PickingMode.Ignore; generateVisualContent+=Draw; }
        private void Draw(MeshGenerationContext context)
        {
            var p=context.painter2D; p.strokeColor=resolvedStyle.color; p.fillColor=p.strokeColor; p.lineWidth=1.6f; p.lineCap=LineCap.Round; p.lineJoin=LineJoin.Round;
            switch(kind)
            {
                case "play": Line(p,7,4,19,12,7,20,7,4); break;
                case "sequence": for(int y=5;y<=19;y+=7) { Line(p,8,y,21,y); Circle(p,3,y,1); } break;
                case "system": Line(p,3,4,21,4,21,17,3,17,3,4); Line(p,12,17,12,21,7,21,17,21); break;
                case "motion": Line(p,3,12,21,12,16,7); Line(p,21,12,16,17); Line(p,7,7,7,17); break;
                case "sky": Circle(p,12,12,5); for(int i=0;i<8;i++) { float a=i*Mathf.PI/4; Line(p,12+8*Mathf.Cos(a),12+8*Mathf.Sin(a),12+10*Mathf.Cos(a),12+10*Mathf.Sin(a)); } break;
                case "swarm": Circle(p,6,6,2); Circle(p,18,7,2); Circle(p,11,17,2); Line(p,8,7,16,7); Line(p,7,8,10,15); Line(p,16,9,13,15); break;
                case "file": Line(p,5,2,15,2,20,7,20,22,5,22,5,2); Line(p,15,2,15,7,20,7); Line(p,8,12,17,12); Line(p,8,16,15,16); break;
                case "plus": Line(p,12,4,12,20); Line(p,4,12,20,12); break;
                case "copy": Line(p,8,8,21,8,21,21,8,21,8,8); Line(p,16,5,16,2,2,2,2,16,5,16); break;
                case "trash": Line(p,5,6,19,6); Line(p,9,3,15,3); Line(p,7,6,8,21,16,21,17,6); Line(p,11,10,11,17); Line(p,14,10,14,17); break;
                case "grip": for(int y=5;y<=19;y+=7) { Circle(p,8,y,1); Circle(p,15,y,1); } break;
                case "settings": Line(p,3,6,21,6); Line(p,3,12,21,12); Line(p,3,18,21,18); Circle(p,8,6,2); Circle(p,16,12,2); Circle(p,10,18,2); break;
                default: Circle(p,12,12,8); Line(p,12,7,12,13); Circle(p,12,17,1); break;
            }
        }
        private static void Line(Painter2D p,params float[] xy) { p.BeginPath(); p.MoveTo(new Vector2(xy[0],xy[1])); for(int i=2;i<xy.Length;i+=2) p.LineTo(new Vector2(xy[i],xy[i+1])); p.Stroke(); }
        private static void Circle(Painter2D p,float x,float y,float radius)
        { p.BeginPath(); for(int i=0;i<=16;i++) { float a=i*Mathf.PI/8; var point=new Vector2(x+radius*Mathf.Cos(a),y+radius*Mathf.Sin(a)); if(i==0) p.MoveTo(point); else p.LineTo(point); } p.Stroke(); }
    }
}
