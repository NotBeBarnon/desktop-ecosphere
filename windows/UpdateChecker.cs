using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DesktopEcosphere {
    sealed class UpdateInfo {
        internal string Tag;
        internal Version Version;
        internal bool TagOnly;
        internal bool Newer { get { return Version>new Version(AppInfo.Version); } }
        internal string Page { get { return AppInfo.Repository+(TagOnly?"/tree/":"/releases/tag/")+Uri.EscapeDataString(Tag); } }
        internal string Message { get { return TagOnly ? "最新稳定标签："+Tag+"。仓库尚无公开 Release 安装包。" : Newer ? "发现新版本："+Tag+"，可打开版本页下载。" : Version<new Version(AppInfo.Version) ? "当前版本比最新正式发布版更新。" : "当前已是最新正式发布版。"; } }
    }
    static class UpdateChecker {
        const int Limit=524288;
        static readonly Regex Stable=new Regex(@"^[vV]?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$",RegexOptions.CultureInvariant);
        internal static Version ParseVersion(string tag) { Version version; if(tag==null || tag.Length>64 || !Stable.IsMatch(tag) || !Version.TryParse(tag.TrimStart('v','V'),out version)) return null; return version; }
        static object Json(string text) { if(text==null || text.Length>Limit) throw new FormatException("版本数据过大或为空。"); return new JavaScriptSerializer { MaxJsonLength=Limit,RecursionLimit=20 }.DeserializeObject(text); }
        internal static UpdateInfo Release(string text) {
            var data=Json(text) as Dictionary<string,object>; if(data==null) throw new FormatException("版本数据格式错误。");
            object tag,draft,preview;
            if(!data.TryGetValue("draft",out draft) || !(draft is bool) || (bool)draft || !data.TryGetValue("prerelease",out preview) || !(preview is bool) || (bool)preview || !data.TryGetValue("tag_name",out tag) || !(tag is string)) throw new FormatException("没有可用的正式版本。");
            var version=ParseVersion((string)tag); if(version==null) throw new FormatException("正式版本号不受支持。");
            return new UpdateInfo {Tag=(string)tag,Version=version};
        }
        internal static UpdateInfo Tags(string text) {
            var tags=Json(text) as object[]; if(tags==null) throw new FormatException("版本标签数据格式错误。"); UpdateInfo latest=null;
            foreach(var value in tags) { var item=value as Dictionary<string,object>; object name; if(item==null || !item.TryGetValue("name",out name) || !(name is string)) continue;
                var version=ParseVersion((string)name); if(version!=null && (latest==null || version>latest.Version)) latest=new UpdateInfo {Tag=(string)name,Version=version,TagOnly=true}; }
            if(latest==null) throw new FormatException("最近的标签中没有稳定版本。"); return latest;
        }
        internal static async Task<UpdateInfo> Check(CancellationToken token) {
            using(var handler=new HttpClientHandler {AllowAutoRedirect=false,SslProtocols=System.Security.Authentication.SslProtocols.Tls12}) using(var client=new HttpClient(handler)) return await Check(client,token);
        }
        internal static async Task<UpdateInfo> Check(HttpClient client,CancellationToken token) {
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)) {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DesktopEcosphere/"+AppInfo.Version);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                string release=await Get(client,"releases/latest",timeout.Token,true);
                if(release!=null) return Release(release);
                return Tags(await Get(client,"tags?per_page=100",timeout.Token,false));
            }
        }
        static async Task<string> Get(HttpClient client,string suffix,CancellationToken token,bool allowMissing) {
            using(var response=await client.GetAsync("https://api.github.com/repos/NotBeBarnon/desktop-ecosphere/"+suffix,HttpCompletionOption.ResponseHeadersRead,token)) {
                if(allowMissing && response.StatusCode==HttpStatusCode.NotFound) return null;
                if(response.StatusCode==HttpStatusCode.Forbidden || (int)response.StatusCode==429) throw new HttpRequestException("GitHub 访问受限，请稍后重试或打开版本页。");
                if(!response.IsSuccessStatusCode) throw new HttpRequestException("无法获取版本信息，请稍后重试。");
                if(response.Content.Headers.ContentLength>Limit) throw new FormatException("版本数据过大。");
                using(var input=await response.Content.ReadAsStreamAsync()) using(var memory=new MemoryStream()) {
                    var buffer=new byte[8192]; int count; while((count=await input.ReadAsync(buffer,0,buffer.Length,token))>0) { if(memory.Length+count>Limit) throw new FormatException("版本数据过大。"); memory.Write(buffer,0,count); }
                    return System.Text.Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }
    }
}
