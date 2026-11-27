using System;
namespace DesktopEcosphere {
    static class PopulationSettings {
        internal static string Query(Preferences p) {
            string profile=p.Population=="sparse" || p.Population=="lively"?p.Population:"standard";
            return "&layout="+(p.Aquascape=="garden" || p.Aquascape=="valley"?p.Aquascape:"river")+"&population="+profile+(p.CustomPopulation?"&riverFish="+Math.Max(1,Math.Min(48,p.RiverFish))+"&chromisFish="+Math.Max(2,Math.Min(18,p.ChromisFish))+"&anthiasFish="+Math.Max(2,Math.Min(14,p.AnthiasFish)):"");
        }
    }
}
