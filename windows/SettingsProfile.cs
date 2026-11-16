using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace DesktopEcosphere {
    static class SettingsProfile {
        const int Limit=65536;
        static readonly string[] Names={"Scene","Quality","Fps","AllScreens","ScreenScenes","BatteryMode","Occlusion","FeedPortions","AutoFeedMinutes","PointerInteraction","Population"};
        internal static string Export(Preferences p) {
            var data=new Dictionary<string,object> { {"Scene",p.Scene},{"Quality",p.Quality},{"Fps",p.Fps},{"AllScreens",p.AllScreens},{"ScreenScenes",p.ScreenScenes},{"BatteryMode",p.BatteryMode},{"Occlusion",p.Occlusion},{"FeedPortions",p.FeedPortions},{"AutoFeedMinutes",p.AutoFeedMinutes},{"PointerInteraction",p.PointerInteraction},{"Population",p.Population} };
            return new JavaScriptSerializer().Serialize(new {format="desktop-ecosphere.settings",schemaVersion=1,settings=data});
        }
        internal static Preferences Read(string path) { if(new FileInfo(path).Length>Limit) throw new FormatException("设置文件不能超过 64 KB。"); return Parse(File.ReadAllText(path)); }
        internal static Preferences Parse(string text) {
            if(string.IsNullOrWhiteSpace(text) || text.Length>Limit) throw new FormatException("设置文件为空或超过 64 KB。");
            var serializer=new JavaScriptSerializer { MaxJsonLength=Limit,RecursionLimit=16 };
            var root=serializer.DeserializeObject(text) as Dictionary<string,object>;
            if(root==null) throw new FormatException("设置文件必须是 JSON 对象。");
            Dictionary<string,object> data=root;
            if(root.ContainsKey("format")) {
                if(!(root["format"] is string) || (string)root["format"]!="desktop-ecosphere.settings") throw new FormatException("不是 Desktop Ecosphere 设置文件。");
                object version; if(!root.TryGetValue("schemaVersion",out version) || !(version is int) || (int)version!=1) throw new FormatException("不支持此设置文件版本。");
                object options; if(!root.TryGetValue("settings",out options) || (data=options as Dictionary<string,object>)==null) throw new FormatException("设置内容缺失。");
            }
            if(!Names.Any(data.ContainsKey)) throw new FormatException("文件中没有可导入的设置。");
            var p=new Preferences(); object v;
            if(data.TryGetValue("Scene",out v)) p.Scene=EnumValue(v,"场景","riverscape","reefscape");
            if(data.TryGetValue("Quality",out v)) p.Quality=EnumValue(v,"画质","eco","balanced","detail");
            if(data.TryGetValue("Population",out v)) p.Population=EnumValue(v,"鱼群密度","sparse","standard","lively");
            if(data.TryGetValue("BatteryMode",out v)) p.BatteryMode=EnumValue(v,"电池策略","limit","pause","normal");
            if(data.TryGetValue("Fps",out v)) p.Fps=Number(v,"帧率",15,30,60);
            if(data.TryGetValue("FeedPortions",out v)) p.FeedPortions=Number(v,"投食份量",1,2,3);
            if(data.TryGetValue("AutoFeedMinutes",out v)) p.AutoFeedMinutes=Number(v,"投食间隔",0,1,5,15,30,60);
            if(data.TryGetValue("AllScreens",out v)) p.AllScreens=Boolean(v,"显示器范围");
            if(data.TryGetValue("Occlusion",out v)) p.Occlusion=Boolean(v,"遮挡节能");
            if(data.TryGetValue("PointerInteraction",out v)) p.PointerInteraction=Boolean(v,"鼠标互动");
            if(data.TryGetValue("ScreenScenes",out v) && v!=null) {
                var screens=v as Dictionary<string,object>;
                if(screens==null) {
                    var entries=v as object[]; if(entries==null) throw new FormatException("显示器设置格式错误。");
                    screens=new Dictionary<string,object>(); foreach(var item in entries) { var entry=item as Dictionary<string,object>; object key,value;
                        if(entry==null || !entry.TryGetValue("Key",out key) || !(key is string) || !entry.TryGetValue("Value",out value)) throw new FormatException("旧版显示器设置格式错误。"); screens[(string)key]=value; }
                }
                if(screens.Count>100) throw new FormatException("显示器设置过多。");
                foreach(var screen in screens) { if(string.IsNullOrWhiteSpace(screen.Key) || screen.Key.Length>128) throw new FormatException("显示器标识无效。"); p.ScreenScenes[screen.Key]=EnumValue(screen.Value,"显示器场景","riverscape","reefscape"); }
            }
            return p;
        }
        static string EnumValue(object value,string name,params string[] allowed) { if(!(value is string) || !allowed.Contains((string)value)) throw new FormatException(name+"值无效。"); return (string)value; }
        static int Number(object value,string name,params int[] allowed) { if(!(value is int) || !allowed.Contains((int)value)) throw new FormatException(name+"值无效。"); return (int)value; }
        static bool Boolean(object value,string name) { if(!(value is bool)) throw new FormatException(name+"必须是布尔值。"); return (bool)value; }
        internal static void Write(string path,string contents) {
            Parse(contents);
            var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { File.WriteAllText(temporary,contents); if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path); }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
