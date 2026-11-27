using System;
using System.Drawing;
namespace DesktopEcosphere {
    static class PolicyTests {
        static void Equal(double expected,double actual,string label) { if(Math.Abs(expected-actual)>0.001) throw new Exception(label+": "+actual); Console.WriteLine("PASS "+label); }
        static void Main() {
            UpdateCheckerTests.Run();
            SettingsProfileTests.Run();
            foreach(var layout in new[]{"river","garden","valley"}) { var option=new Preferences { Aquascape=layout }; if(!PopulationSettings.Query(option).Contains("layout="+layout+"&")) throw new Exception("造景参数没有传递："+layout); }
            if(!PopulationSettings.Query(new Preferences { Aquascape="unknown" }).Contains("layout=river&")) throw new Exception("未知造景没有回退");
            Console.WriteLine("PASS 三套造景传参与未知值回退");
            var populationOptions=new Preferences { RiverFish=48 };
            if(PopulationSettings.Query(populationOptions).Contains("riverFish")) throw new Exception("预设模式意外使用自定义数量");
            populationOptions.CustomPopulation=true; populationOptions.RiverFish=17; populationOptions.ChromisFish=6; populationOptions.AnthiasFish=8;
            string populationQuery=PopulationSettings.Query(populationOptions);
            if(!populationQuery.Contains("riverFish=17") || !populationQuery.Contains("chromisFish=6") || !populationQuery.Contains("anthiasFish=8")) throw new Exception("自定义数量没有传递给场景");
            Console.WriteLine("PASS 预设与自定义数量参数隔离");
            Equal(1,LightingPolicy.Multiplier(100,false,new DateTime(2026,1,1,0,0,0)),"手动亮度不随时间变化");
            Equal(0.35,LightingPolicy.Multiplier(100,true,new DateTime(2026,1,1,0,0,0)),"夜间亮度下限");
            Equal(1,LightingPolicy.Multiplier(100,true,new DateTime(2026,1,1,12,0,0)),"白天保持亮度");
            Equal(0.675,LightingPolicy.Multiplier(100,true,new DateTime(2026,1,1,7,0,0)),"黎明过渡");
            Equal(0.675,LightingPolicy.Multiplier(100,true,new DateTime(2026,1,1,19,0,0)),"黄昏过渡");
            Equal(0.175,LightingPolicy.Multiplier(50,true,new DateTime(2026,1,1,23,0,0)),"手动亮度与昼夜叠加");
            foreach(int hour in new[]{6,8,18,20}) { var boundary=new DateTime(2026,1,1,hour,0,0); Equal(LightingPolicy.Multiplier(100,true,boundary.AddSeconds(-1)),LightingPolicy.Multiplier(100,true,boundary.AddSeconds(1)),"昼夜边界连续 "+hour); }
            var serializer=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Preferences));
            using(var oldData=new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Scene\":\"reefscape\",\"Fps\":30}"))) {
                var migrated=(Preferences)serializer.ReadObject(oldData);
                if(migrated.Population!="standard" || migrated.Brightness!=100 || migrated.DayNight || migrated.CustomPopulation || migrated.RiverFish!=24 || migrated.ChromisFish!=9 || migrated.AnthiasFish!=7) throw new Exception("旧配置默认值迁移失败");
                migrated.Population="lively";
                using(var saved=new System.IO.MemoryStream()) { serializer.WriteObject(saved,migrated); saved.Position=0; if(((Preferences)serializer.ReadObject(saved)).Population!="lively") throw new Exception("鱼群密度保存失败"); }
                Console.WriteLine("PASS 旧配置密度迁移、新设置序列化");
            }
            var feed=new FeedClock();
            Equal(0,feed.Due(0,1,true)?1:0,"自动投食等待间隔"); Equal(1,feed.Due(60,1,true)?1:0,"间隔到期投食"); Equal(0,feed.Due(61,1,true)?1:0,"不会重复投食"); Equal(0,feed.Due(120,1,false)?1:0,"暂停时不投食"); Equal(0,feed.Due(121,1,true)?1:0,"恢复不补投"); Equal(1,feed.Due(600,1,true)?1:0,"长时间跳跃仅投一次"); feed.Reset(); Equal(0,feed.Due(601,1,true)?1:0,"手动投食后重新计时");
            Equal(0,EnergyPolicy.Rate(60,true,false,"normal",0),"锁屏/关闭显示器停止");
            Equal(15,EnergyPolicy.Rate(60,false,true,"limit",0),"电池限帧");
            Equal(0,EnergyPolicy.Rate(30,false,true,"pause",0),"电池暂停");
            Equal(10,EnergyPolicy.Rate(60,false,false,"normal",0.8),"大面积遮挡降帧");
            Equal(0,EnergyPolicy.Rate(60,false,false,"normal",0.99),"完全遮挡暂停");
            Equal(60,EnergyPolicy.Rate(60,false,false,"normal",0),"解除节能恢复");
            var primary=new Rectangle(0,0,1920,1080); var portrait=new Rectangle(-1080,-400,1080,1920);
            Equal(0.5,EnergyPolicy.Coverage(primary,new[]{new Rectangle(0,0,960,1080),new Rectangle(0,0,960,1080)}),"重叠窗口不重复计数");
            Equal(0,EnergyPolicy.Coverage(primary,new[]{portrait}),"副屏遮挡不影响主屏");
            Equal(1,EnergyPolicy.Coverage(portrait,new[]{portrait}),"负坐标竖屏覆盖");
            Equal(0.5,EnergyPolicy.Coverage(new Rectangle(1920,0,3840,2160),new[]{new Rectangle(1920,0,1920,2160)}),"4K 副屏覆盖");
        }
    }
}
