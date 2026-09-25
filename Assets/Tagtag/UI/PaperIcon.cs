using UnityEngine;
using UnityEngine.UIElements;

namespace Tagtag.UI
{
    // Consistent 24-unit pictograms. Illustrated sticker content stays raster.
    public sealed class PaperIcon : VisualElement
    {
        private readonly string kind;
        public PaperIcon(string kind,float size=24){this.kind=kind;style.width=size;style.height=size;AddToClassList("line-icon");pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;}
        private void Draw(MeshGenerationContext context)
        {
            var p=context.painter2D;float s=contentRect.width/24f;p.strokeColor=resolvedStyle.color;p.lineWidth=1.8f*s;p.lineCap=LineCap.Round;p.lineJoin=LineJoin.Round;
            System.Action<float[]> path=points=>{p.BeginPath();p.MoveTo(new Vector2(points[0]*s,points[1]*s));for(int i=2;i<points.Length;i+=2)p.LineTo(new Vector2(points[i]*s,points[i+1]*s));p.Stroke();};
            System.Action<float,float,float> circle=(x,y,r)=>{p.BeginPath();p.Arc(new Vector2(x*s,y*s),r*s,0,360);p.Stroke();};
            switch(kind){
                case "home":path(new float[]{3,11,12,3,21,11,21,21,15,21,15,14,9,14,9,21,3,21,3,11});break;
                case "stickers":path(new float[]{5,7,3,7,3,21,17,21,17,19});goto case "stick";
                case "stick":path(new float[]{7,3,21,3,21,12,14,19,7,19,7,3});path(new float[]{14,19,14,12,21,12});break;
                case "explore":path(new float[]{2,5,8,3,16,6,22,3,22,19,16,22,8,19,2,21,2,5});path(new float[]{8,3,8,19});path(new float[]{16,6,16,22});break;
                case "profile":circle(12,8,4);path(new float[]{4,21,4,19,7,15,17,15,20,19,20,21});break;
                case "location":
                    p.BeginPath();p.MoveTo(new Vector2(12*s,22*s));
                    p.BezierCurveTo(new Vector2(8*s,17*s),new Vector2(4*s,13*s),new Vector2(4*s,9*s));
                    p.BezierCurveTo(new Vector2(4*s,-1*s),new Vector2(20*s,-1*s),new Vector2(20*s,9*s));
                    p.BezierCurveTo(new Vector2(20*s,13*s),new Vector2(16*s,17*s),new Vector2(12*s,22*s));p.Stroke();circle(12,9,2.5f);break;
                case "lock":path(new float[]{5,10,19,10,19,21,5,21,5,10});path(new float[]{8,10,8,6,10,3,14,3,16,6,16,10});break;
                case "plus":path(new float[]{12,4,12,20});path(new float[]{4,12,20,12});break;
                case "check":path(new float[]{4,12,10,18,21,6});break;
                case "close":path(new float[]{5,5,19,19});path(new float[]{19,5,5,19});break;
                case "flip":path(new float[]{20,7,17,4,14,7});path(new float[]{17,4,17,10,12,13});path(new float[]{4,17,7,20,10,17});path(new float[]{7,20,7,14,12,11});break;
                case "back":path(new float[]{15,4,7,12,15,20});break;
                case "right":path(new float[]{9,4,17,12,9,20});break;
                case "journal":path(new float[]{4,3,20,3,20,21,4,21,4,3});path(new float[]{8,3,8,21});path(new float[]{12,8,17,8});break;
                case "info":circle(12,12,9);path(new float[]{12,10,12,17});circle(12,6.5f,.5f);break;
                case "warning":path(new float[]{12,3,22,21,2,21,12,3});path(new float[]{12,9,12,14});circle(12,18,.5f);break;
                default:circle(12,12,9);path(new float[]{12,7,12,13});circle(12,17,.5f);break;
            }
        }
    }
}
