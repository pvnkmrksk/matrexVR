using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Operator graphics may use free display pixels; calibrated LED rectangles always win.</summary>
public static class OperatorOverlayLayout
{
    private sealed class Reservation { public int display;public Rect desired,actual; }
    private static readonly Dictionary<string,Reservation> reservations=new Dictionary<string,Reservation>();
    public static int Revision { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset(){reservations.Clear();Revision=0;}
    public static void Invalidate(){Revision++;}
    public static void Release(string owner){if(reservations.Remove(owner))Revision++;}
    public static Vector2 DisplaySize(int display)
    {
        if(display==0)return new Vector2(Screen.width,Screen.height);
        if(display<0||display>=Display.displays.Length)return Vector2.zero;
        return new Vector2(Display.displays[display].renderingWidth,Display.displays[display].renderingHeight);
    }
    public static List<Rect> ProtectedRects(int display,int width,int height)
    {
        var result=new List<Rect>();var main=MainController.Instance;
        if(main==null||width<=0||height<=0)return result;
        foreach(var rig in main.SystemConfigs.Values)
        {
            if(rig.desktopPreview||rig.targetDisplay!=display)continue;
            string order=ViewportSetter.NormalizeDisplayOrder(rig.displayOrder);
            for(int slot=0;slot<order.Length;slot++)
            {
                var r=ViewportSetter.PanelRect(slot,rig,width,height);
                result.Add(new Rect(r.x*width,r.y*height,r.width*width,r.height*height));
            }
        }
        return result;
    }
    public static bool Reserve(string owner,int display,Rect desired,out Rect actual)
    {
        var size=DisplaySize(display);var bounds=new Rect(0,0,size.x,size.y);
        var blocked=ProtectedRects(display,(int)size.x,(int)size.y);
        blocked.AddRange(reservations.Where(p=>p.Key!=owner&&p.Value.display==display).Select(p=>p.Value.actual));
        if(reservations.TryGetValue(owner,out var previous)&&previous.display==display&&previous.desired==desired&&Fits(previous.actual,bounds,blocked))
        {actual=previous.actual;return true;}
        if(!TryPlace(desired,bounds,blocked,out actual)){Release(owner);return false;}
        if(previous==null||previous.display!=display||previous.actual!=actual||previous.desired!=desired)Revision++;
        reservations[owner]=new Reservation{display=display,desired=desired,actual=actual};return true;
    }
    private static bool Fits(Rect candidate,Rect bounds,IEnumerable<Rect> blocked) =>
        candidate.width>0&&candidate.height>0&&candidate.xMin>=bounds.xMin&&candidate.yMin>=bounds.yMin&&candidate.xMax<=bounds.xMax&&candidate.yMax<=bounds.yMax&&!blocked.Any(candidate.Overlaps);
    // Sweep candidate rows and merge occupied horizontal intervals. A complete LED
    // display simply has no operator space: omit the widget instead of covering it.
    public static bool TryPlace(Rect desired,Rect bounds,IEnumerable<Rect> obstacles,out Rect actual)
    {
        actual=default;
        if(desired.width<=0||desired.height<=0||desired.width>bounds.width||desired.height>bounds.height)return false;
        var blocked=obstacles.Where(r=>r.width>0&&r.height>0&&r.Overlaps(bounds)).ToList();
        if(Fits(desired,bounds,blocked)){actual=desired;return true;}
        var rows=new HashSet<float>{bounds.yMin,bounds.yMax-desired.height,Mathf.Clamp(desired.y,bounds.yMin,bounds.yMax-desired.height)};
        foreach(var r in blocked){rows.Add(r.yMax+4);rows.Add(r.yMin-desired.height-4);}
        float best=float.PositiveInfinity;
        foreach(float y in rows.Where(y=>y>=bounds.yMin&&y+desired.height<=bounds.yMax))
        {
            float left=bounds.xMin;
            var intervals=blocked.Where(r=>r.yMin<y+desired.height&&r.yMax>y).OrderBy(r=>r.xMin).ToList();
            foreach(var r in intervals.Concat(new[]{new Rect(bounds.xMax+4,y,0,desired.height)}))
            {
                float right=Mathf.Min(bounds.xMax,r.xMin-4);
                if(right-left>=desired.width)
                {
                    float x=Mathf.Clamp(desired.x,left,right-desired.width);
                    var candidate=new Rect(x,y,desired.width,desired.height);
                    float score=(x-desired.x)*(x-desired.x)+(y-desired.y)*(y-desired.y);
                    if(score<best&&Fits(candidate,bounds,blocked)){best=score;actual=candidate;}
                }
                left=Mathf.Max(left,r.xMax+4);
            }
        }
        return !float.IsPositiveInfinity(best);
    }
}

/// <summary>Tab hides operator graphics together; setup keeps ordinary Tab navigation.</summary>
public static class OperatorOverlayVisibility
{
    public static bool Hidden { get; private set; }
    public static event Action Changed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset(){Hidden=false;Changed=null;}
    public static void Toggle()=>SetHidden(!Hidden);
    public static void SetHidden(bool value)
    {
        if(Hidden==value)return;Hidden=value;Changed?.Invoke();
        if(value)for(int i=0;i<Display.displays.Length;i++)if(Display.displays[i].active)DisplayFrameClear.ClearOnce(i);
    }
}
