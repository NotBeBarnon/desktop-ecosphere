using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DesktopEcosphere {
    // The controller window remains outside Explorer, so losing a desktop child
    // cannot terminate the tray application or destroy its settings UI.
    sealed class WallpaperSurface : Form {
        readonly WebView2 web = new WebView2();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Screen screen;
        readonly Preferences prefs;
        readonly CoreWebView2Environment environment;
        readonly Func<Rectangle,int> rate;
        IntPtr parent;
        bool busy, closing;
        int appliedRate = -1;
        bool appliedBattery;
        string pointer = "";
        readonly FeedClock feedClock=new FeedClock();
        readonly System.Diagnostics.Stopwatch feedTime=System.Diagnostics.Stopwatch.StartNew();
        readonly SceneLoadWatch loadWatch=new SceneLoadWatch();
        bool navigating;
        bool? appliedInteraction;
        double? appliedLighting;
        public bool Ready { get; private set; }
        public bool Attached { get { return !IsDisposed && IsHandleCreated && Native.IsWindow(parent) && Native.GetParent(Handle) == parent; } }
        public bool BoundsValid { get { Native.RECT r; var b=screen.Bounds; return IsHandleCreated && Native.GetWindowRect(Handle,out r) && Math.Abs(r.Left-b.Left)<=2 && Math.Abs(r.Top-b.Top)<=2 && Math.Abs((r.Right-r.Left)-b.Width)<=2 && Math.Abs((r.Bottom-r.Top)-b.Height)<=2; } }
        public WallpaperSurface(Screen display,Preferences preferences,CoreWebView2Environment env,Func<Rectangle,int> policy) {
            screen = display; prefs = preferences; environment = env; rate = policy;
            Text = "生态缸壁纸 · " + display.DeviceName; FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Bounds = display.Bounds;
            BackColor = Color.FromArgb(8,14,12); web.Dock = DockStyle.Fill; Controls.Add(web);
            timer.Interval = 100; timer.Tick += async (s,e) => await Tick();
            Shown += async (s,e) => await Initialize();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        public void AttachTo(IntPtr host) {
            parent = host;
            int style = Native.GetWindowLong(Handle,-16);
            Native.SetWindowLong(Handle,-16,(style & ~unchecked((int)0x80000000)) | 0x40000000);
            Native.SetLastError(0); var previous = Native.SetParent(Handle,host);
            if(previous == IntPtr.Zero && Marshal.GetLastWin32Error() != 0) { Close(); return; }
            var b = screen.Bounds; var point = new Native.POINT { X=b.Left,Y=b.Top };
            Native.ScreenToClient(host,ref point);
            Native.SetWindowPos(Handle,IntPtr.Zero,point.X,point.Y,b.Width,b.Height,0x0010 | 0x0040 | 0x0020);
        }
        async Task Initialize() {
            try {
                await web.EnsureCoreWebView2Async(environment); if(closing) return;
                web.CoreWebView2.SetVirtualHostNameToFolderMapping("habitat.local",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"),CoreWebView2HostResourceAccessKind.DenyCors);
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                web.CoreWebView2.Settings.AreDevToolsEnabled = Program.Smoke;
                web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(Interaction.Setup);
                web.CoreWebView2.NavigationStarting += (s,e) => { if(!e.Uri.StartsWith("https://habitat.local/",StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
                web.CoreWebView2.NewWindowRequested += (s,e) => e.Handled = true;
                web.CoreWebView2.ProcessFailed += (s,e) => { Ready = false; if(!closing) web.CoreWebView2.Reload(); };
                web.CoreWebView2.NavigationCompleted += (s,e) => { navigating=false; Ready=false; appliedRate=-1; pointer=""; appliedInteraction=null; appliedLighting=null; };
                RefreshScene(); timer.Start();
            } catch(Exception e) { Program.Log(e); if(!closing) Close(); }
        }
        public void RefreshScene(bool retry=false) {
            if(closing || web.CoreWebView2 == null) return;
            string scene; if(!prefs.ScreenScenes.TryGetValue(screen.DeviceName,out scene)) scene = prefs.Scene;
            Ready = false; appliedRate = -1; pointer = "";
            navigating=true; appliedInteraction=null; feedClock.Reset(); loadWatch.Start(retry);
            appliedLighting=null;
            web.CoreWebView2.Navigate("https://habitat.local/scenes/" + scene + "/wallpaper.html?quality=" + prefs.Quality + "&population=" + prefs.Population);
        }
        async Task<string> Script(string script) {
            if(closing || web.CoreWebView2 == null) return "null";
            try { return await web.CoreWebView2.ExecuteScriptAsync(script); }
            catch(Exception e) { if(!closing) Program.Log(e); return "null"; }
        }
        async Task Tick() {
            if(busy || closing || !Attached) return; busy = true;
            try {
                if(!Ready) { Ready=!navigating && await Script("Boolean(window.habitatStats)")=="true"; if(!Ready) { bool error=await Script("Boolean(document.querySelector('#error')&&!document.querySelector('#error').hidden)")=="true"; if(loadWatch.Retry(error)) RefreshScene(true); feedClock.Reset(); return; } }
                int wanted = rate(screen.Bounds); bool battery = Program.OnBattery;
                double light=LightingPolicy.Multiplier(prefs.Brightness,prefs.DayNight,DateTime.Now);
                if((wanted>0 || !appliedLighting.HasValue) && appliedLighting!=light) { await Script(LightingPolicy.Script(light)); appliedLighting=light; }
                if(wanted != appliedRate || battery != appliedBattery) {
                    await Script("window.habitatPause(false);window.habitatPower(" + (battery ? "true" : "false") + ");window.habitatRate(" + wanted + ")");
                    appliedRate = wanted; appliedBattery = battery;
                }
                if(appliedInteraction!=prefs.PointerInteraction) { await Script(Interaction.PointerEnabled(prefs.PointerInteraction)); appliedInteraction=prefs.PointerInteraction; pointer=""; }
                if(feedClock.Due(feedTime.Elapsed.TotalSeconds,prefs.AutoFeedMinutes,wanted>0)) await Script(Interaction.Feed(prefs.FeedPortions));
                if(wanted > 0 && prefs.PointerInteraction) {
                    Point p = web.PointToClient(Cursor.Position);
                    string next = web.ClientRectangle.Contains(p) ? string.Format(CultureInfo.InvariantCulture,
                        "(()=>{{const c=document.querySelector('#scene');c.dispatchEvent(new PointerEvent('pointermove',{{clientX:{0}/devicePixelRatio,clientY:{1}/devicePixelRatio,bubbles:true}}));}})()",p.X,p.Y)
                        : "document.querySelector('#scene').dispatchEvent(new PointerEvent('pointerleave'))";
                    if(next != pointer) { await Script(next); pointer = next; }
                }
            } finally { busy = false; }
        }
        public async Task Feed() { if(Ready && rate(screen.Bounds)>0) { await Script(Interaction.Feed(prefs.FeedPortions)); feedClock.Reset(); } }
        public async Task<string> Diagnostics() {
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
            Native.RECT rect; Native.GetWindowRect(Handle,out rect);
            uint dpi; try { dpi=Native.GetDpiForWindow(Handle); } catch(EntryPointNotFoundException) { dpi=0; }
            return serializer.Serialize(new {device=screen.DeviceName,attached=Attached,boundsValid=BoundsValid,dpi,expected=new {x=screen.Bounds.X,y=screen.Bounds.Y,width=screen.Bounds.Width,height=screen.Bounds.Height},actual=new {x=rect.Left,y=rect.Top,width=rect.Right-rect.Left,height=rect.Bottom-rect.Top},page=serializer.DeserializeObject(await Script("({scene:location.pathname,quality:new URLSearchParams(location.search).get('quality'),viewport:[innerWidth,innerHeight],stats:window.habitatStats?.()})"))});
        }
        public async Task<bool> CheckScene(string expected) {
            return await Script("Boolean(location.pathname.includes('/" + expected + "/') && window.habitatStats)") == "true";
        }
        internal async Task<bool> CheckLightingReload() {
            Ready=false; navigating=true; web.CoreWebView2.Reload();
            for(int i=0;i<120 && !Ready && !closing;i++) await Task.Delay(500);
            if(!Ready) return false;
            await Task.Delay(300);
            double expected=LightingPolicy.Multiplier(prefs.Brightness,prefs.DayNight,DateTime.Now);
            return await Script("Math.abs(habitatStats().lighting-"+expected.ToString("0.###",CultureInfo.InvariantCulture)+")<0.001")=="true";
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            closing = true; timer.Stop(); timer.Dispose();
            if(IsHandleCreated) Native.SetParent(Handle,IntPtr.Zero);
            web.Dispose(); base.OnFormClosing(e);
        }
    }
}
