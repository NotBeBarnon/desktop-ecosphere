using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace DesktopEcosphere {
    static class SettingsProfileTests {
        static void Check(bool valid,string label) { if(!valid) throw new Exception(label); Console.WriteLine("PASS "+label); }
        static void Reject(string json) { bool failed=false; try { SettingsProfile.Parse(json); } catch { failed=true; } Check(failed,"拒绝无效设置："+json.Substring(0,Math.Min(40,json.Length))); }
        internal static void Run() {
            var original=new Preferences { Scene="reefscape",Quality="detail",Fps=60,Population="lively",AllScreens=true,FeedPortions=3,AutoFeedMinutes=15,PointerInteraction=false,BatteryMode="pause",Occlusion=false,Wallpaper=true,Paused=true };
            original.ScreenScenes["offline-monitor"]="riverscape";
            original.Aquascape="garden"; original.Brightness=75; original.DayNight=true;
            original.CustomPopulation=true; original.RiverFish=17; original.ChromisFish=6; original.AnthiasFish=8;
            string exported=SettingsProfile.Export(original); var copy=SettingsProfile.Parse(exported);
            Check(copy.Aquascape=="garden" && SettingsProfile.Parse("{\"Scene\":\"riverscape\"}").Aquascape=="river","造景导出往返与旧配置迁移");
            Reject("{\"Aquascape\":\"invalid\"}");
            Check(copy.Brightness==75 && copy.DayNight,"灯光设置导入导出");
            Check(copy.CustomPopulation && copy.RiverFish==17 && copy.ChromisFish==6 && copy.AnthiasFish==8,"自定义鱼种数量导入导出");
            Check(copy.Scene==original.Scene && copy.Quality==original.Quality && copy.Fps==60 && copy.Population=="lively" && copy.AllScreens && copy.FeedPortions==3 && copy.AutoFeedMinutes==15 && !copy.PointerInteraction && copy.BatteryMode=="pause" && !copy.Occlusion && copy.ScreenScenes["offline-monitor"]=="riverscape","全部便携设置往返、保留离线显示器");
            var document=(System.Collections.Generic.Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(exported);
            var values=(System.Collections.Generic.Dictionary<string,object>)document["settings"];
            Check(!values.ContainsKey("Paused") && !values.ContainsKey("Wallpaper") && !values.ContainsKey("Startup") && !copy.Paused && !copy.Wallpaper,"不导出运行模式和开机启动");
            var legacy=SettingsProfile.Parse("{\"Scene\":\"reefscape\",\"ScreenScenes\":[{\"Key\":\"old-screen\",\"Value\":\"reefscape\"}],\"Paused\":true,\"Wallpaper\":true}");
            Check(legacy.Brightness==100 && !legacy.DayNight,"旧版设置文件灯光缺省值");
            Check(!legacy.CustomPopulation && legacy.RiverFish==24 && legacy.ChromisFish==9 && legacy.AnthiasFish==7,"旧版文件保留密度模式");
            var partial=SettingsProfile.Parse("{\"CustomPopulation\":true,\"RiverFish\":17}");
            Check(partial.CustomPopulation && partial.RiverFish==17 && partial.ChromisFish==9 && partial.AnthiasFish==7,"仅含生物数量的设置文件");
            Reject("{\"RiverFish\":0}"); Reject("{\"ChromisFish\":19}"); Reject("{\"AnthiasFish\":1}"); Reject("{\"CustomPopulation\":\"true\"}");
            Reject("{\"Brightness\":24}"); Reject("{\"Brightness\":151}"); Reject("{\"Brightness\":\"100\"}"); Reject("{\"DayNight\":1}");
            Check(legacy.ScreenScenes["old-screen"]=="reefscape" && legacy.Population=="standard" && legacy.Fps==30 && !legacy.Paused && !legacy.Wallpaper,"兼容旧配置、缺省值和运行状态隔离");
            foreach(var invalid in new[]{"{}","[]","null","broken","{\"Fps\":1000}","{\"PointerInteraction\":\"false\"}","{\"Scene\":\"unknown\"}","{\"FeedPortions\":0}","{\"Population\":null}","{\"ScreenScenes\":{\"x\":\"invalid\"}}","{\"format\":\"desktop-ecosphere.settings\",\"schemaVersion\":2,\"settings\":{\"Fps\":30}}"}) Reject(invalid);
            bool oversized=false; try { SettingsProfile.Parse(new string('x',65537)); } catch(FormatException) { oversized=true; } Check(oversized,"拒绝超长文件");
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"profile-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); string path=Path.Combine(folder,"settings.json");
            SettingsProfile.Write(path,exported); SettingsProfile.Write(path,SettingsProfile.Export(new Preferences())); Check(SettingsProfile.Read(path).Scene=="riverscape","原子覆盖导出文件");
            bool invalidWrite=false; try { SettingsProfile.Write(path,"{}"); } catch { invalidWrite=true; } Check(invalidWrite && SettingsProfile.Read(path).Scene=="riverscape" && Directory.GetFiles(folder).Length==1,"导出失败保留旧文件且无临时残留");
            File.Delete(path); Directory.Delete(folder);
        }
    }
}
