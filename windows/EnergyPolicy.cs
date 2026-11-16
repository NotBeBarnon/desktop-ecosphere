using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DesktopEcosphere {
    static class EnergyPolicy {
        internal static int Rate(int requested,bool stopped,bool battery,string batteryMode,double coverage) {
            if(stopped || (battery && batteryMode=="pause") || coverage>=0.98) return 0;
            int fps=battery && batteryMode=="limit"?Math.Min(15,requested):requested;
            return coverage>=0.70?Math.Min(10,fps):fps;
        }
        // Region subtraction accounts for overlaps without counting windows twice.
        internal static double Coverage(Rectangle monitor,IEnumerable<Rectangle> windows) {
            if(monitor.Width<=0 || monitor.Height<=0) return 0;
            using(var region=new Region(monitor)) using(var matrix=new System.Drawing.Drawing2D.Matrix()) {
                foreach(var rectangle in windows) region.Exclude(Rectangle.Intersect(monitor,rectangle));
                double visible=0; foreach(var r in region.GetRegionScans(matrix)) visible+=r.Width*r.Height;
                return Math.Max(0,Math.Min(1,1-visible/((double)monitor.Width*monitor.Height)));
            }
        }
        internal static List<Rectangle> VisibleWindows() {
            var rectangles=new List<Rectangle>();
            EnumWindows((h,p)=>{
                if(!IsWindowVisible(h) || IsIconic(h) || Native.IsDesktop(h)) return true;
                int cloaked; if(DwmGetWindowAttribute(h,14,out cloaked,4)==0 && cloaked!=0) return true;
                Native.RECT r; if(Native.GetWindowRect(h,out r) && r.Right>r.Left && r.Bottom>r.Top) rectangles.Add(Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom)); return true;
            },IntPtr.Zero); return rectangles;
        }
        [DllImport("user32.dll")] static extern bool EnumWindows(Native.EnumWindowsProc callback,IntPtr param);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int value,int size);
    }
}
