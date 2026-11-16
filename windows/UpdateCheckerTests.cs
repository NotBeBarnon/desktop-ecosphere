using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace DesktopEcosphere {
    static class UpdateCheckerTests {
        sealed class Fake : HttpMessageHandler {
            internal int Calls;
            internal HttpStatusCode First=HttpStatusCode.NotFound;
            internal string Body="[{\"name\":\"v1.0.0\"}]";
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
                token.ThrowIfCancellationRequested(); Calls++;
                if(request.RequestUri.Host!="api.github.com") throw new Exception("意外请求域名");
                return Task.FromResult(new HttpResponseMessage(Calls==1?First:HttpStatusCode.OK) { Content=new StringContent(Body) });
            }
        }
        static void Check(bool condition,string label) { if(!condition) throw new Exception(label); Console.WriteLine("PASS "+label); }
        static void Reject(Action action,string label) { bool rejected=false; try { action(); } catch { rejected=true; } Check(rejected,label); }
        internal static void Run() {
            Check(UpdateChecker.ParseVersion("v0.10.0")>UpdateChecker.ParseVersion("v0.9.0"),"版本号按数值比较");
            foreach(var invalid in new[]{"1.2","v1.2.3-beta","v01.2.3","https://evil.invalid","v999999999999.0.0"}) Check(UpdateChecker.ParseVersion(invalid)==null,"忽略非稳定版本 "+invalid);
            var tags=UpdateChecker.Tags("[{\"name\":\"v0.9.0\"},{\"name\":\"v1.0.0-beta\"},{\"name\":\"v0.10.0\"}]");
            Check(tags.Tag=="v0.10.0" && tags.TagOnly && tags.Newer && tags.Page.StartsWith(AppInfo.Repository+"/tree/"),"标签回退不冒充安装包");
            var release=UpdateChecker.Release("{\"tag_name\":\"v1.0.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://evil.invalid\"}");
            Check(release.Newer && release.Page==AppInfo.Repository+"/releases/tag/v1.0.0","下载入口固定在本仓库");
            Reject(()=>UpdateChecker.Release("{\"tag_name\":\"v1.0.0\",\"draft\":true,\"prerelease\":false}"),"拒绝草稿版本");
            Reject(()=>UpdateChecker.Tags("[]"),"无稳定标签时不宣称最新");
            Reject(()=>UpdateChecker.Tags(new string('x',524289)),"拒绝超大版本数据");
            var fake=new Fake(); using(var client=new HttpClient(fake)) { var info=UpdateChecker.Check(client,CancellationToken.None).GetAwaiter().GetResult(); Check(fake.Calls==2 && info.TagOnly,"Release 404 后查询标签"); }
            fake=new Fake {First=HttpStatusCode.Forbidden}; using(var client=new HttpClient(fake)) Reject(()=>UpdateChecker.Check(client,CancellationToken.None).GetAwaiter().GetResult(),"限流不会误报最新版本");
            fake=new Fake(); using(var client=new HttpClient(fake)) using(var cancel=new CancellationTokenSource()) { cancel.Cancel(); Reject(()=>UpdateChecker.Check(client,cancel.Token).GetAwaiter().GetResult(),"检查更新可取消"); }
            string versionPath=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","VERSION");
            Check(System.IO.File.ReadAllText(versionPath).Trim()==AppInfo.Version,"应用版本与 VERSION 文件一致");
        }
    }
}
