using System;
using System.Drawing;
namespace DesktopEcosphere {
    static class PolicyTests {
        static void Equal(double expected,double actual,string label) { if(Math.Abs(expected-actual)>0.001) throw new Exception(label+": "+actual); Console.WriteLine("PASS "+label); }
        static void Main() {
            var serializer=new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Preferences));
            using(var oldData=new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Scene\":\"reefscape\",\"Fps\":30}"))) {
                var migrated=(Preferences)serializer.ReadObject(oldData);
                if(migrated.Population!="standard") throw new Exception("旧配置未回退到标准密度");
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
