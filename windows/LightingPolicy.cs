using System;
using System.Globalization;
namespace DesktopEcosphere {
    static class LightingPolicy {
        internal static double Multiplier(int brightness,bool dayNight,DateTime localTime) {
            double level=Math.Max(25,Math.Min(150,brightness))/100.0;
            if(!dayNight) return level;
            double hour=localTime.TimeOfDay.TotalHours;
            double daylight=hour<6 || hour>=20 ? 0 : hour<8 ? Smooth((hour-6)/2) : hour<18 ? 1 : 1-Smooth((hour-18)/2);
            return Math.Round(level*(0.35+0.65*daylight),3);
        }
        static double Smooth(double t) { return t*t*(3-2*t); }
        internal static string Script(double multiplier) { return "window.habitatLighting("+multiplier.ToString("0.###",CultureInfo.InvariantCulture)+")"; }
    }
}
