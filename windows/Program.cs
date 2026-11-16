using System;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DesktopEcosphere {
    [DataContract] public class Preferences {
        [DataMember] public string Scene = "riverscape";
        [DataMember] public string Quality = "balanced";
        [DataMember] public int Fps = 30;
        [DataMember] public bool Paused;
        [DataMember] public bool Wallpaper;
        [DataMember] public bool AllScreens;
        [DataMember] public Dictionary<string,string> ScreenScenes = new Dictionary<string,string>();
        [DataMember] public string BatteryMode = "limit";
        [DataMember] public bool Occlusion = true;
        [DataMember] public int FeedPortions=1;
        [DataMember] public int AutoFeedMinutes;
        [DataMember] public bool PointerInteraction=true;
        [DataMember] public string Population="standard";
        [DataMember] public int Brightness=100;
        [DataMember] public bool DayNight;
        [DataMember] public bool CustomPopulation;
        [DataMember] public int RiverFish=24;
        [DataMember] public int ChromisFish=9;
        [DataMember] public int AnthiasFish=7;
        [OnDeserializing] void Defaults(StreamingContext context) { BatteryMode="limit"; Occlusion=true; FeedPortions=1; PointerInteraction=true; Population="standard"; Brightness=100; RiverFish=24; ChromisFish=9; AnthiasFish=7; }
    }
    static class Program {
        internal static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopHabitatsWindows");
        internal static bool Smoke;
        internal static bool? TestBattery;
        internal static bool OnBattery { get { return Smoke && TestBattery.HasValue ? TestBattery.Value : SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Offline; } }
        [STAThread] static void Main(string[] args) {
            Smoke = Array.IndexOf(args, "--smoke-test") >= 0;
            using (var mutex = new Mutex(false, "Local\\DesktopHabitatsWindows" + (Smoke ? "Smoke" : ""))) {
                if (!mutex.WaitOne(0)) { MessageBox.Show("生态缸已在运行，请打开系统托盘菜单。", "桌面生态缸"); return; }
                Directory.CreateDirectory(Data);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s,e) => { Log(e.Exception); MessageBox.Show(e.Exception.Message, "桌面生态缸"); };
                Application.Run(new Habitat());
                mutex.ReleaseMutex();
            }
        }
        internal static void Log(Exception e) { File.AppendAllText(Path.Combine(Data,"error.log"), DateTime.Now + " " + e + Environment.NewLine); }
    }
    sealed class Habitat : Form {
        readonly WebView2 web = new WebView2();
        readonly FlowLayoutPanel bar = new FlowLayoutPanel();
        readonly Label status = new Label();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Preferences prefs;
        readonly Button pause = new Button();
        readonly ToolStripMenuItem pauseMenu = new ToolStripMenuItem();
        readonly List<WallpaperSurface> surfaces = new List<WallpaperSurface>();
        bool wallpaper, locked, suspended, busy, closing, ready;
        int tick, appliedRate = -1;
        bool appliedBattery;
        string lastPointer = "";
        bool displayOff;
        IntPtr powerNotification;
        static Guid DisplayState = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");
        DateTime coverageUpdated = DateTime.MinValue;
        List<Rectangle> coverageWindows = new List<Rectangle>();
        readonly FeedClock feedClock=new FeedClock();
        readonly System.Diagnostics.Stopwatch feedTime=System.Diagnostics.Stopwatch.StartNew();
        readonly SceneLoadWatch loadWatch=new SceneLoadWatch();
        bool navigating;
        bool? appliedInteraction;
        double? appliedLighting;
        readonly string settings = Path.Combine(Program.Data,"settings.json");
        public Habitat() {
            prefs = Program.Smoke ? new Preferences() : LoadPreferences();
            Text = "桌面生态缸 · Desktop Ecosphere"; Size = new Size(1120,740); MinimumSize = new Size(800,520);
            StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(8,14,12);
            Font = new Font("Microsoft YaHei UI",9);
            web.Dock = DockStyle.Fill; Controls.Add(web);
            bar.Dock = DockStyle.Top; bar.Height = 54; bar.Padding = new Padding(10,10,0,0); bar.BackColor = Color.FromArgb(242,246,244); Controls.Add(bar);
            var scene = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            scene.Items.AddRange(new object[]{"淡水生态缸","珊瑚海水缸"}); scene.SelectedIndex = prefs.Scene == "reefscape" ? 1 : 0;
            scene.SelectedIndexChanged += (s,e) => { prefs.Scene = scene.SelectedIndex == 0 ? "riverscape" : "reefscape"; Save(); Navigate(); RefreshSurfaces(); }; bar.Controls.Add(scene);
            var quality = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            quality.Items.AddRange(new object[]{"节能画质","均衡画质","精细画质"}); quality.SelectedIndex = prefs.Quality == "eco" ? 0 : prefs.Quality == "detail" ? 2 : 1;
            quality.SelectedIndexChanged += (s,e) => { prefs.Quality = new[]{"eco","balanced","detail"}[quality.SelectedIndex]; Save(); Navigate(); RefreshSurfaces(); }; bar.Controls.Add(quality);
            var fps = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 82 };
            fps.Items.AddRange(new object[]{"15 FPS","30 FPS","60 FPS"}); fps.SelectedIndex = prefs.Fps == 15 ? 0 : prefs.Fps == 60 ? 2 : 1;
            fps.SelectedIndexChanged += (s,e) => { prefs.Fps = new[]{15,30,60}[fps.SelectedIndex]; Save(); }; bar.Controls.Add(fps);
            AddButton("投食", Feed); pause.Text = prefs.Paused ? "继续" : "暂停"; pause.Width = 64; pause.Click += (s,e) => TogglePause(); bar.Controls.Add(pause);
            AddButton("设为桌面壁纸", Attach);
            AddButton("开机启动", ToggleStartup);
            Action openSettings = () => {
                using(var dialog=new SettingsForm(prefs,RuntimeStatus,ExportDiagnostics)) {
                    if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                    try { SettingsForm.SetStartup(dialog.Startup); } catch(Exception ex) { MessageBox.Show("开机启动设置失败："+ex.Message); return; }
                    var value=dialog.Result;
                    prefs.FeedPortions=value.FeedPortions; prefs.AutoFeedMinutes=value.AutoFeedMinutes; prefs.PointerInteraction=value.PointerInteraction; feedClock.Reset();
                    prefs.Population=value.Population;
                    prefs.CustomPopulation=value.CustomPopulation; prefs.RiverFish=value.RiverFish; prefs.ChromisFish=value.ChromisFish; prefs.AnthiasFish=value.AnthiasFish;
                    prefs.Brightness=value.Brightness; prefs.DayNight=value.DayNight;
                    prefs.Scene=value.Scene; prefs.Quality=value.Quality; prefs.Fps=value.Fps; prefs.AllScreens=value.AllScreens; prefs.ScreenScenes=value.ScreenScenes; prefs.BatteryMode=value.BatteryMode; prefs.Occlusion=value.Occlusion;
                    scene.SelectedIndex=prefs.Scene=="reefscape"?1:0; quality.SelectedIndex=prefs.Quality=="eco"?0:prefs.Quality=="detail"?2:1; fps.SelectedIndex=prefs.Fps==15?0:prefs.Fps==60?2:1;
                    Save(); Navigate(); if(wallpaper) RebuildSurfaces();
                }
            };
            AddButton("设置",openSettings);
            AddButton("重新加载",()=>{ Navigate(); RefreshSurfaces(); });
            bar.AutoSize=true;
            status.Dock = DockStyle.Bottom; status.Height = 28; status.ForeColor = Color.Gainsboro; status.Padding = new Padding(10,5,0,0); status.Text = "正在初始化 WebView2…"; Controls.Add(status);
            var menu = new ContextMenuStrip();
            menu.Items.Add("设置…",null,(s,e)=>openSettings());
            menu.Items.Add("重新加载场景",null,(s,e)=>{ Navigate(); RefreshSurfaces(); });
            menu.Items.Add("打开预览窗口",null,(s,e) => Detach());
            menu.Items.Add("设为桌面壁纸",null,(s,e) => Attach());
            menu.Items.Add("投食",null,(s,e) => Feed());
            pauseMenu.Text = prefs.Paused ? "继续" : "暂停"; pauseMenu.Click += (s,e) => TogglePause(); menu.Items.Add(pauseMenu);
            menu.Items.Add("切换淡水 / 海水",null,(s,e) => { scene.SelectedIndex = 1 - scene.SelectedIndex; });
            var qualityMenu = new ToolStripMenuItem("画质");
            for(int i=0;i<3;i++) { int index = i; var item = new ToolStripMenuItem(quality.Items[i].ToString()); item.Click += (s,e) => quality.SelectedIndex = index; qualityMenu.DropDownItems.Add(item); }
            menu.Items.Add(qualityMenu);
            var fpsMenu = new ToolStripMenuItem("帧率上限");
            for(int i=0;i<3;i++) { int index = i; var item = new ToolStripMenuItem(fps.Items[i].ToString()); item.Click += (s,e) => fps.SelectedIndex = index; fpsMenu.DropDownItems.Add(item); }
            menu.Items.Add(fpsMenu);
            var screenMenu = new ToolStripMenuItem("显示器与独立场景"); menu.Items.Add(screenMenu);
            menu.Opening += (s,e) => {
                for(int i=0;i<3;i++) { ((ToolStripMenuItem)qualityMenu.DropDownItems[i]).Checked = quality.SelectedIndex == i; ((ToolStripMenuItem)fpsMenu.DropDownItems[i]).Checked = fps.SelectedIndex == i; }
                screenMenu.DropDownItems.Clear();
                var all = new ToolStripMenuItem("在所有显示器运行") { Checked = prefs.AllScreens };
                all.Click += (o,a) => { prefs.AllScreens = !prefs.AllScreens; Save(); if(wallpaper) RebuildSurfaces(); }; screenMenu.DropDownItems.Add(all);
                screenMenu.DropDownItems.Add(new ToolStripSeparator());
                foreach(var display in Screen.AllScreens) {
                    string device = display.DeviceName;
                    var displayMenu = new ToolStripMenuItem(device + (display.Primary ? "（主屏）" : ""));
                    string chosen; prefs.ScreenScenes.TryGetValue(device,out chosen);
                    string[] names = {"跟随全局场景","淡水生态缸","珊瑚海水缸"}; string[] values = {"","riverscape","reefscape"};
                    for(int i=0;i<3;i++) { string value = values[i]; var option = new ToolStripMenuItem(names[i]) { Checked = (chosen ?? "") == value };
                        option.Click += (o,a) => { if(value == "") prefs.ScreenScenes.Remove(device); else prefs.ScreenScenes[device] = value; Save(); RefreshSurfaces(); }; displayMenu.DropDownItems.Add(option); }
                    screenMenu.DropDownItems.Add(displayMenu);
                }
            };
            menu.Items.Add("开机启动：切换",null,(s,e) => ToggleStartup());
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("退出",null,(s,e) => Close());
            tray.Icon = SystemIcons.Application; tray.Text = "桌面生态缸"; tray.ContextMenuStrip = menu; tray.Visible = !Program.Smoke;
            tray.DoubleClick += (s,e) => Detach();
            timer.Interval = 100; timer.Tick += async (s,e) => await Tick();
            SystemEvents.SessionSwitch += SessionChanged; SystemEvents.PowerModeChanged += PowerChanged; SystemEvents.DisplaySettingsChanged += DisplayChanged;
            Shown += async (s,e) => await Initialize();
        }
        void AddButton(string label, Action action) { var b = new Button { Text = label, AutoSize = true }; b.Click += (s,e) => action(); bar.Controls.Add(b); }
        string RuntimeStatus() {
            var lines=new List<string> { "运行模式："+(wallpaper?"桌面壁纸":"窗口预览"),"电源："+(Program.OnBattery?"电池":"接通电源"),"显示器电源通知："+(powerNotification==IntPtr.Zero?"未注册成功":"已注册"),"当前状态："+(displayOff?"显示器关闭":locked?"锁屏":suspended?"休眠":prefs.Paused?"手动暂停":"正常") };
            foreach(var display in Screen.AllScreens) lines.Add(display.DeviceName+" · "+display.Bounds.Width+" × "+display.Bounds.Height+" · 当前上限 "+(wallpaper?SurfaceRate(display.Bounds):Rate())+" FPS");
            return string.Join(Environment.NewLine,lines);
        }
        async void ExportDiagnostics() {
            using(var dialog=new SaveFileDialog { Filter="诊断文件 (*.json)|*.json",FileName="生态缸诊断.json" }) {
                if(dialog.ShowDialog()!=DialogResult.OK) return;
                try { await WriteDiagnostics(dialog.FileName); MessageBox.Show("诊断文件已保存。","桌面生态缸"); } catch(Exception e) { MessageBox.Show("保存失败："+e.Message); }
            }
        }
        async Task WriteDiagnostics(string path) {
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
            var reports=new List<object>(); foreach(var surface in surfaces.ToArray()) if(!surface.IsDisposed) reports.Add(serializer.DeserializeObject(await surface.Diagnostics()));
            var report=new { version=AppInfo.Version,createdUtc=DateTime.UtcNow.ToString("o"),windows=Environment.OSVersion.VersionString,status=RuntimeStatus(),webview=web.CoreWebView2==null?null:web.CoreWebView2.Environment.BrowserVersionString,displays=Screen.AllScreens.Select(x=>new {device=x.DeviceName,primary=x.Primary,x=x.Bounds.X,y=x.Bounds.Y,width=x.Bounds.Width,height=x.Bounds.Height}).ToArray(),surfaces=reports,preview=serializer.DeserializeObject(await Script("window.habitatStats?.() || null")) };
            File.WriteAllText(path,serializer.Serialize(report));
        }
        Preferences LoadPreferences() {
            try { using(var f = File.OpenRead(settings)) { var p = (Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(f);
                if(p.Scene != "riverscape" && p.Scene != "reefscape") p.Scene = "riverscape";
                if(p.Quality != "eco" && p.Quality != "balanced" && p.Quality != "detail") p.Quality = "balanced";
                if(p.Fps != 15 && p.Fps != 30 && p.Fps != 60) p.Fps = 30;
                if(p.BatteryMode!="limit" && p.BatteryMode!="pause" && p.BatteryMode!="normal") p.BatteryMode="limit";
                p.FeedPortions=Math.Max(1,Math.Min(3,p.FeedPortions));
                if(p.Population!="sparse" && p.Population!="lively" && p.Population!="standard") p.Population="standard";
                p.Brightness=Math.Max(25,Math.Min(150,p.Brightness));
                p.RiverFish=Math.Max(1,Math.Min(48,p.RiverFish)); p.ChromisFish=Math.Max(2,Math.Min(18,p.ChromisFish)); p.AnthiasFish=Math.Max(2,Math.Min(14,p.AnthiasFish));
                if(!new[]{0,1,5,15,30,60}.Contains(p.AutoFeedMinutes)) p.AutoFeedMinutes=0;
                if(p.ScreenScenes == null) p.ScreenScenes = new Dictionary<string,string>();
                foreach(var device in p.ScreenScenes.Keys.ToArray()) if(p.ScreenScenes[device] != "riverscape" && p.ScreenScenes[device] != "reefscape") p.ScreenScenes.Remove(device);
                return p; }
            } catch { return new Preferences(); }
        }
        void Save() { if(Program.Smoke) return; try { var temp = settings + ".tmp"; using(var f = File.Create(temp)) new DataContractJsonSerializer(typeof(Preferences)).WriteObject(f,prefs); if(File.Exists(settings)) File.Replace(temp,settings,null); else File.Move(temp,settings); } catch(Exception e) { Program.Log(e); status.Text = "设置保存失败：" + e.Message; } }
        async Task Initialize() {
            try {
                var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
                if(Program.Smoke) { var oldError = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-error.txt"); if(File.Exists(oldError)) File.Delete(oldError); }
                if(!File.Exists(Path.Combine(root,"scenes",prefs.Scene,"wallpaper.html"))) throw new FileNotFoundException("场景资源缺失，请重新运行 windows/build.ps1。");
                var env = await CoreWebView2Environment.CreateAsync(null,Path.Combine(Program.Data,Program.Smoke ? "SmokeWebView" : "WebView"));
                await web.EnsureCoreWebView2Async(env);
                web.CoreWebView2.SetVirtualHostNameToFolderMapping("habitat.local",root,CoreWebView2HostResourceAccessKind.DenyCors);
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                web.CoreWebView2.Settings.AreDevToolsEnabled = Program.Smoke;
                await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(Interaction.Setup);
                web.CoreWebView2.NavigationCompleted+=(s,e)=>{ navigating=false; ready=false; appliedLighting=null; appliedInteraction=null; appliedRate=-1; };
                await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.hostErrors=[];window.addEventListener('error',e=>hostErrors.push(e.message+' '+e.filename));window.addEventListener('unhandledrejection',e=>hostErrors.push(String(e.reason)));");
                web.CoreWebView2.NavigationStarting += (s,e) => { if(!e.Uri.StartsWith("https://habitat.local/",StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
                web.CoreWebView2.NewWindowRequested += (s,e) => e.Handled = true;
                web.CoreWebView2.ProcessFailed += (s,e) => { ready = false; status.Text = "渲染进程异常，请退出后重新打开。"; };
                Navigate(); timer.Start();
                if(Program.Smoke) await Smoke(); else if(prefs.Wallpaper) Attach();
            } catch(Exception e) { Program.Log(e); status.Text = "初始化失败：" + e.Message; if(Program.Smoke) { Environment.ExitCode = 1; File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-error.txt"),e.ToString()); Close(); } else MessageBox.Show(e.Message + "\n请确认 Microsoft Edge WebView2 Runtime 已安装。","启动失败"); }
        }
        void Navigate(bool retry=false) { if(web.CoreWebView2 == null) return; navigating=true; ready=false; appliedRate=-1; appliedInteraction=null; appliedLighting=null; lastPointer=""; feedClock.Reset(); loadWatch.Start(retry); web.CoreWebView2.Navigate("https://habitat.local/scenes/" + prefs.Scene + "/wallpaper.html?quality=" + prefs.Quality + PopulationSettings.Query(prefs)); status.Text="正在加载场景…"; }
        async Task<string> Script(string script) { if(closing || web.CoreWebView2 == null) return "null"; try { return await web.CoreWebView2.ExecuteScriptAsync(script); } catch(Exception e) { Program.Log(e); return "null"; } }
        int Rate() {
            return EnergyPolicy.Rate(prefs.Fps,wallpaper || prefs.Paused || locked || suspended || displayOff || WindowState==FormWindowState.Minimized,Program.OnBattery,prefs.BatteryMode,0);
        }
        int SurfaceRate(Rectangle screen) {
            if(prefs.Occlusion && (DateTime.UtcNow-coverageUpdated).TotalSeconds>=1) { coverageWindows=EnergyPolicy.VisibleWindows(); coverageUpdated=DateTime.UtcNow; }
            return EnergyPolicy.Rate(prefs.Fps,prefs.Paused || locked || suspended || displayOff,Program.OnBattery,prefs.BatteryMode,prefs.Occlusion?EnergyPolicy.Coverage(screen,coverageWindows):0);
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); powerNotification=Native.RegisterPowerSettingNotification(Handle,ref DisplayState,0); }
        protected override void OnHandleDestroyed(EventArgs e) { if(powerNotification!=IntPtr.Zero) Native.UnregisterPowerSettingNotification(powerNotification); powerNotification=IntPtr.Zero; base.OnHandleDestroyed(e); }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x218 && m.WParam.ToInt32()==0x8013 && m.LParam!=IntPtr.Zero) {
                Guid id=(Guid)Marshal.PtrToStructure(m.LParam,typeof(Guid));
                if(id==DisplayState && Marshal.ReadInt32(m.LParam,16)>=4) displayOff=Marshal.ReadInt32(m.LParam,20)==0;
            }
            base.WndProc(ref m);
        }
        async Task Tick() {
            if(busy || closing) return; busy = true;
            try {
                if(wallpaper && ++tick % 30 == 0 && (surfaces.Count == 0 || surfaces.Any(x => x.IsDisposed || !x.Attached))) RebuildSurfaces();
                if(!ready) { ready=!navigating && await Script("Boolean(window.habitatStats)")=="true"; if(!ready) { bool failed=await Script("Boolean(document.querySelector('#error')&&!document.querySelector('#error').hidden)")=="true"; if(loadWatch.Retry(failed)) Navigate(true); status.Text=failed?"场景加载失败，可点击重新加载（自动重试最多两次）":"正在加载场景…"; feedClock.Reset(); return; } }
                int rate = Rate(); bool battery = Program.OnBattery;
                double light=LightingPolicy.Multiplier(prefs.Brightness,prefs.DayNight,DateTime.Now);
                if((rate>0 || !appliedLighting.HasValue) && appliedLighting!=light) { await Script(LightingPolicy.Script(light)); appliedLighting=light; }
                if(rate != appliedRate || battery != appliedBattery) {
                    await Script("window.habitatPause(" + (prefs.Paused ? "true" : "false") + ");window.habitatPower(" + (battery ? "true" : "false") + ");window.habitatRate(" + rate + ");");
                    appliedRate = rate; appliedBattery = battery;
                    status.Text = (rate == 0 ? "已暂停" : "运行中 · 上限 " + rate + " FPS") + " · " + (prefs.Scene == "riverscape" ? "淡水生态缸" : "珊瑚海水缸") + " · 托盘右键可控制";
                }
                if(appliedInteraction!=prefs.PointerInteraction) { await Script(Interaction.PointerEnabled(prefs.PointerInteraction)); appliedInteraction=prefs.PointerInteraction; lastPointer=""; }
                if(feedClock.Due(feedTime.Elapsed.TotalSeconds,prefs.AutoFeedMinutes,rate>0)) await Script(Interaction.Feed(prefs.FeedPortions));
                if(rate > 0 && prefs.PointerInteraction) {
                    Point p = web.PointToClient(Cursor.Position); bool inside = web.ClientRectangle.Contains(p);
                    string pointer = inside ? string.Format(CultureInfo.InvariantCulture,"(()=>{{const c=document.querySelector('#scene');c.dispatchEvent(new PointerEvent('pointermove',{{clientX:{0}/devicePixelRatio,clientY:{1}/devicePixelRatio,bubbles:true}}));}})()",p.X,p.Y) : "document.querySelector('#scene').dispatchEvent(new PointerEvent('pointerleave'));";
                    if(pointer != lastPointer) { await Script(pointer); lastPointer = pointer; }
                }
            } finally { busy = false; }
        }
        async void Feed() { if(wallpaper) { foreach(var surface in surfaces.ToArray()) await surface.Feed(); } else if(ready && Rate()>0) { await Script(Interaction.Feed(prefs.FeedPortions)); feedClock.Reset(); } }
        void TogglePause() { prefs.Paused = !prefs.Paused; pause.Text = pauseMenu.Text = prefs.Paused ? "继续" : "暂停"; appliedRate = -1; Save(); }
        void Attach() {
            if(wallpaper) return;
            if(web.CoreWebView2 == null) { status.Text = "请等待场景初始化后再设置壁纸。"; return; }
            var parent = Native.FindWallpaperHost();
            if(parent == IntPtr.Zero) { MessageBox.Show("未找到桌面壁纸层，保留预览窗口。可重启资源管理器后重试。","桌面挂载失败"); return; }
            wallpaper = true; prefs.Wallpaper = true; RebuildSurfaces(); Hide(); appliedRate = -1; Save();
            if(!Program.Smoke) tray.ShowBalloonTip(3500,"生态缸已设为桌面壁纸","右键系统托盘图标可投食、暂停或退出。",ToolTipIcon.Info);
        }
        void RefreshSurfaces() { foreach(var surface in surfaces.ToArray()) surface.RefreshScene(); }
        void RebuildSurfaces() {
            foreach(var surface in surfaces.ToArray()) if(!surface.IsDisposed) surface.Close(); surfaces.Clear();
            if(!wallpaper || closing) return;
            var host = Native.FindWallpaperHost(); if(host == IntPtr.Zero) return;
            foreach(var display in Screen.AllScreens.Where(x => prefs.AllScreens || x.Primary)) {
                var surface = new WallpaperSurface(display,prefs,web.CoreWebView2.Environment,SurfaceRate); surfaces.Add(surface); surface.Show();
                surface.AttachTo(host);
            }
        }
        void RestorePreview() { wallpaper = false; foreach(var surface in surfaces.ToArray()) if(!surface.IsDisposed) surface.Close(); surfaces.Clear(); WindowState = FormWindowState.Normal; Show(); Activate(); appliedRate = -1; }
        void Detach() { RestorePreview(); prefs.Wallpaper = false; Save(); }
        void ToggleStartup() {
            try { using(var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if(key.GetValue("DesktopHabitatsWindows") == null) { key.SetValue("DesktopHabitatsWindows","\"" + Application.ExecutablePath + "\""); MessageBox.Show("已开启当前用户的开机启动。","桌面生态缸"); }
                else { key.DeleteValue("DesktopHabitatsWindows",false); MessageBox.Show("已关闭开机启动。","桌面生态缸"); }
            }} catch(Exception e) { Program.Log(e); MessageBox.Show(e.Message,"设置失败"); }
        }
        void SessionChanged(object sender,SessionSwitchEventArgs e) { if(IsDisposed) return; BeginInvoke((Action)(() => { if(e.Reason == SessionSwitchReason.SessionLock) locked = true; else if(e.Reason == SessionSwitchReason.SessionUnlock) locked = false; })); }
        void PowerChanged(object sender,PowerModeChangedEventArgs e) { if(IsDisposed) return; BeginInvoke((Action)(() => { if(e.Mode == PowerModes.Suspend) suspended = true; if(e.Mode == PowerModes.Resume) suspended = false; })); }
        void DisplayChanged(object sender,EventArgs e) { if(!IsDisposed) BeginInvoke((Action)RebuildSurfaces); }
        void SimulateDisplayState(int value) {
            IntPtr data=Marshal.AllocHGlobal(24);
            try { Marshal.StructureToPtr(DisplayState,data,false); Marshal.WriteInt32(data,16,4); Marshal.WriteInt32(data,20,value); var message=Message.Create(Handle,0x218,new IntPtr(0x8013),data); WndProc(ref message); } finally { Marshal.FreeHGlobal(data); }
        }
        async Task VerifyPowerIntegration() {
            for(int i=0;i<200 && busy;i++) await Task.Delay(50);
            if(busy) throw new Exception("宿主调度未在测试前完成");
            string original=prefs.BatteryMode;
            try {
                SimulateDisplayState(0); await Tick();
                bool off=await Script("(habitatStats().loop?.fps ?? habitatStats().effectiveFPS)===0")=="true";
                SimulateDisplayState(1); Program.TestBattery=true; prefs.BatteryMode="pause"; await Tick();
                bool batteryPaused=await Script("(habitatStats().loop?.fps ?? habitatStats().effectiveFPS)===0")=="true";
                prefs.BatteryMode="limit"; await Tick();
                bool limited=await Script("(habitatStats().loop?.fps ?? habitatStats().effectiveFPS)===15")=="true";
                Program.TestBattery=false; prefs.BatteryMode=original; await Tick();
                bool resumed=await Script("(habitatStats().loop?.fps ?? habitatStats().effectiveFPS)>0")=="true";
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-power-"+prefs.Scene+".json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {displayOff=off,batteryPaused,limited,resumed,simulated=true}));
                if(!off || !batteryPaused || !limited || !resumed) throw new Exception("电源消息到场景渲染的集成验证失败："+RuntimeStatus()+"; window="+WindowState+"; visible="+Visible+"; "+await Script("habitatStats()"));
            } finally { Program.TestBattery=null; prefs.BatteryMode=original; displayOff=false; }
            await Tick();
        }
        async Task VerifyPopulationProfiles() {
            var reports=new List<object>();
            foreach(var profile in new[]{"sparse","standard","lively"}) {
                prefs.Population=profile; Navigate(); timer.Start();
                for(int i=0;i<240 && !ready;i++) await Task.Delay(500);
                if(!ready) throw new Exception("鱼群密度场景加载超时："+profile);
                await Task.Delay(200);
                int expected=prefs.Scene=="riverscape"?(profile=="sparse"?12:profile=="lively"?36:24):(profile=="sparse"?12:profile=="lively"?28:19);
                string actual=await Script("(()=>{const s=habitatStats();return s.fishCount??(s.population.clownfish+s.population.chromis+s.population.anthias)})()");
                if(actual!=expected.ToString(CultureInfo.InvariantCulture)) throw new Exception("鱼群数量不匹配："+profile+" "+actual);
                string healthy=await Script("(()=>{const s=habitatStats();return Boolean((s.renderedFrames||s.frames)>0 && !s.contextLost && !s.shaderErrors && s.finite!==false)})()");
                for(int i=0;i<75 && healthy!="true";i++) { await Task.Delay(200); healthy=await Script("(()=>{const s=habitatStats();return Boolean((s.renderedFrames||s.frames)>0 && !s.contextLost && !s.shaderErrors && s.finite!==false)})()"); }
                timer.Stop();
                if(healthy!="true") throw new Exception("密度切换后场景状态异常："+profile+" "+await Script("habitatStats()"));
                reports.Add(new {profile,expected,actual=int.Parse(actual),healthy=true});
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-population-"+prefs.Scene+".json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(reports));
            prefs.Population="standard"; Navigate(); timer.Start();
            for(int i=0;i<240 && !ready;i++) await Task.Delay(500);
            if(!ready) throw new Exception("恢复标准鱼群密度失败"); timer.Stop();
        }
        async Task VerifyCustomPopulation() {
            prefs.CustomPopulation=true; prefs.RiverFish=17; prefs.ChromisFish=6; prefs.AnthiasFish=8;
            Navigate(); timer.Start();
            for(int i=0;i<240 && !ready;i++) await Task.Delay(500);
            if(!ready) throw new Exception("自定义鱼群加载超时");
            string valid="false";
            for(int i=0;i<75 && valid!="true";i++) {
                await Task.Delay(200);
                valid=await Script("(()=>{const s=habitatStats();return Boolean((s.fishCount===17 || (s.population.chromis===6 && s.population.anthias===8 && s.population.clownfish===3 && s.population.shrimp===2)) && (s.renderedFrames||s.frames)>0 && !s.shaderErrors && s.finite!==false)})()");
            }
            timer.Stop(); if(valid!="true") throw new Exception("自定义鱼群实际数量或渲染状态不正确");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-custom-population-"+prefs.Scene+".json"),"{\"counts\":true,\"rendered\":true}");
            prefs.CustomPopulation=false; Navigate(); timer.Start();
            for(int i=0;i<240 && !ready;i++) await Task.Delay(500);
            if(!ready) throw new Exception("切回密度模式失败"); timer.Stop();
        }
        async Task<double> CaptureLighting(string path) {
            using(var file=File.Create(path)) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
            using(var bitmap=new Bitmap(path)) { double total=0; int count=0; for(int y=0;y<bitmap.Height;y+=8) for(int x=0;x<bitmap.Width;x+=8) { var pixel=bitmap.GetPixel(x,y); total+=pixel.R+pixel.G+pixel.B; count+=3; } return total/count; }
        }
        async Task VerifyLighting() {
            await Script("window.habitatRate(0);window.habitatLighting(1.25)"); await Task.Delay(500);
            double bright=await CaptureLighting(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-light-bright-"+prefs.Scene+".png"));
            await Script("window.habitatLighting(0.25)"); await Task.Delay(500);
            double dark=await CaptureLighting(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-light-dark-"+prefs.Scene+".png"));
            bool stopped=await Script("(habitatStats().loop?.fps ?? habitatStats().effectiveFPS)===0")=="true";
            if(!(bright>dark*1.15) || !stopped) throw new Exception("灯光调整没有改变实际图像，或唤醒了暂停动画");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-lighting-"+prefs.Scene+".json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {bright,dark,stopped}));
            appliedLighting=null; appliedRate=-1; await Tick();
        }
        async Task RecordMotionPreview() {
            if(prefs.Scene!="reefscape") return;
            await Script("window.reef.setView('fish');window.habitatPause(false);window.habitatRate(30);window.__motionClip=null;window.__motionError=null;(()=>{try{const canvas=document.querySelector('#scene');const stream=canvas.captureStream(24);const mime=MediaRecorder.isTypeSupported('video/webm;codecs=vp8')?'video/webm;codecs=vp8':'video/webm';const chunks=[];const recorder=new MediaRecorder(stream,{mimeType:mime,videoBitsPerSecond:900000});recorder.ondataavailable=e=>{if(e.data.size)chunks.push(e.data)};recorder.onerror=e=>{window.__motionError=String(e.error);stream.getTracks().forEach(t=>t.stop())};recorder.onstop=()=>{stream.getTracks().forEach(t=>t.stop());const reader=new FileReader();reader.onload=()=>window.__motionClip=reader.result.split(',')[1];reader.readAsDataURL(new Blob(chunks,{type:mime}))};recorder.start();setTimeout(()=>{if(recorder.state==='recording')recorder.stop()},8000)}catch(e){window.__motionError=String(e)}})()");
            for(int i=0;i<60;i++) { await Task.Delay(250); if(await Script("Boolean(window.__motionClip||window.__motionError)")=="true") break; }
            string encoded=await Script("window.__motionClip||null");
            if(encoded!="null") { var serializer=new System.Web.Script.Serialization.JavaScriptSerializer {MaxJsonLength=20*1024*1024}; string data=serializer.Deserialize<string>(encoded); File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"motion-preview-reefscape.webm"),Convert.FromBase64String(data)); }
            else File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"motion-preview-error.txt"),await Script("window.__motionError||'Recording timeout'"));
            await Script("window.reef.setView('wide')");
        }
        async Task Smoke() {
            if(prefs.Scene=="riverscape") {
                using(var settingsWindow=new SettingsForm(prefs,RuntimeStatus,null,token=>Task.FromResult(UpdateChecker.Tags("[{\"name\":\"v0.9.0\"}]")))) {
                    settingsWindow.Show(); await Task.Delay(500);
                    var imported=new Preferences { Scene="reefscape",Population="lively",Fps=60,AutoFeedMinutes=15,FeedPortions=3,Brightness=75,DayNight=true,CustomPopulation=true,RiverFish=17,ChromisFish=6,AnthiasFish=8 };
                    imported.ScreenScenes["offline-smoke-screen"]="reefscape";
                    settingsWindow.ImportDraft(SettingsProfile.Export(imported)); var draft=SettingsProfile.Parse(settingsWindow.ExportDraft());
                    if(draft.Brightness!=75 || !draft.DayNight || prefs.Brightness!=100 || prefs.DayNight) throw new Exception("灯光设置导入或隔离失败");
                    if(!draft.CustomPopulation || draft.RiverFish!=17 || draft.ChromisFish!=6 || draft.AnthiasFish!=8 || prefs.CustomPopulation) throw new Exception("自定义生物数量导入或隔离失败");
                    if(draft.Scene!="reefscape" || draft.Population!="lively" || draft.Fps!=60 || draft.AutoFeedMinutes!=15 || draft.FeedPortions!=3 || draft.ScreenScenes["offline-smoke-screen"]!="reefscape" || prefs.Scene!="riverscape" || prefs.Population!="standard") throw new Exception("设置界面导入或草稿隔离失败");
                    bool rejected=false; try { settingsWindow.ImportDraft("{\"Fps\":999}"); } catch(FormatException) { rejected=true; }
                    if(!rejected || SettingsProfile.Parse(settingsWindow.ExportDraft()).Scene!="reefscape") throw new Exception("无效导入修改了设置草稿");
                    settingsWindow.ResetDraft(); draft=SettingsProfile.Parse(settingsWindow.ExportDraft());
                    if(draft.Brightness!=100 || draft.DayNight) throw new Exception("灯光默认值恢复失败");
                    if(draft.CustomPopulation || draft.RiverFish!=24 || draft.ChromisFish!=9 || draft.AnthiasFish!=7) throw new Exception("生物数量恢复默认失败");
                    if(draft.Scene!="riverscape" || draft.Fps!=30 || draft.Population!="standard" || draft.AutoFeedMinutes!=0 || draft.ScreenScenes.Count!=0) throw new Exception("设置界面恢复默认失败");
                    if(!settingsWindow.CheckUpdateForTest()) throw new Exception("更新检查界面未显示标签提示");
                    settingsWindow.CaptureTabs(AppDomain.CurrentDomain.BaseDirectory); settingsWindow.Close();
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-settings-transfer.json"),"{\"import\":true,\"export\":true,\"reset\":true,\"draftIsolated\":true}");
                }
            }
            for(int i=0;i<120 && !ready;i++) { await Task.Delay(1000); if(i == 10) File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-loading.json"),await Script("JSON.stringify({text:document.body.innerText,errors:window.hostErrors,scripts:[...document.scripts].map(s=>({src:s.src,type:s.type})),resources:performance.getEntriesByType('resource').map(x=>({name:x.name,duration:x.duration}))})")); }
            if(!ready) { using(var f = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-failure.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,f); throw new Exception("场景在 120 秒内未完成加载：" + await Script("JSON.stringify({text:document.body.innerText,errors:window.hostErrors,resources:performance.getEntriesByType('resource').map(x=>({name:x.name,duration:x.duration}))})")); }
            await Task.Delay(3000);
            timer.Stop();
            string before = await Script("window.habitatStats()");
            await Script("window.habitatFeed();window.habitatRate(0)");
            string stopped = await Script("window.habitatStats()");
            await Script("window.habitatRate(30)"); await Task.Delay(1500);
            string resumed = await Script("window.habitatStats()");
            string valid = await Script("(()=>{const s=habitatStats();return Boolean((s.renderedFrames||s.frames)>0 && !(document.querySelector('#error')&&!document.querySelector('#error').hidden))})()");
            if(valid != "true") throw new Exception("场景未产生有效渲染帧：" + resumed);
            string target = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-" + prefs.Scene);
            File.WriteAllText(target + ".json","{\"before\":" + before + ",\"stopped\":" + stopped + ",\"resumed\":" + resumed + "}");
            await VerifyPowerIntegration();
            await VerifyLighting();
            await Script("window.__interactionMoves=0;document.querySelector('#scene').addEventListener('pointermove',()=>window.__interactionMoves++);");
            await Script(Interaction.PointerEnabled(false));
            await Script("document.querySelector('#scene').dispatchEvent(new PointerEvent('pointermove',{bubbles:true,clientX:20,clientY:20}));");
            bool blocked=await Script("window.__interactionMoves===0")=="true";
            await Script(Interaction.PointerEnabled(true));
            await Script("document.querySelector('#scene').dispatchEvent(new PointerEvent('pointermove',{bubbles:true,clientX:20,clientY:20}));");
            bool enabled=await Script("window.__interactionMoves===1")=="true";
            bool portions=true;
            if(prefs.Scene=="reefscape") {
                portions=await Script("(()=>{const before=habitatStats().food;window.habitatFeed(2);return habitatStats().food-before===16})()")=="true";
            }
            if(!blocked || !enabled || !portions) throw new Exception("鼠标互动开关或投食份量验证失败");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-interaction-"+prefs.Scene+".json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {blocked,enabled,portions}));
            using(var f = File.Create(target + ".png")) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,f);
            await RecordMotionPreview();
            await VerifyPopulationProfiles();
            await VerifyCustomPopulation();
            if(prefs.Scene == "riverscape") { prefs.Scene = "reefscape"; Navigate(); timer.Start(); await Smoke(); } else {
                var host = Native.FindWallpaperHost();
                if(host != IntPtr.Zero) {
                    prefs.AllScreens = true;
                    prefs.CustomPopulation=true; prefs.RiverFish=17; prefs.ChromisFish=6; prefs.AnthiasFish=8;
                    prefs.ScreenScenes[Screen.PrimaryScreen.DeviceName] = "riverscape";
                    Attach();
                    for(int i=0;i<120 && surfaces.Any(x => !x.Ready);i++) await Task.Delay(500);
                    bool attached = wallpaper && surfaces.Count == Screen.AllScreens.Length && surfaces.All(x => x.Attached && x.Ready && x.BoundsValid);
                    bool customWallpaper=true; foreach(var surface in surfaces) if(!await surface.CheckCustomPopulation()) customWallpaper=false;
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-wallpaper-population.json"),"{\"customCounts\":"+customWallpaper.ToString().ToLowerInvariant()+"}");
                    if(!customWallpaper) throw new Exception("壁纸窗口未应用自定义生物数量");
                    prefs.Brightness=65;
                    bool lightingReload=true; foreach(var surface in surfaces) if(!await surface.CheckLightingReload()) lightingReload=false;
                    prefs.Brightness=100;
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-wallpaper-lighting.json"),"{\"reloadRetained\":"+lightingReload.ToString().ToLowerInvariant()+"}");
                    if(!lightingReload) throw new Exception("壁纸重载后未保留灯光设置");
                    var snapshots = new List<string>();
                    foreach(var surface in surfaces) snapshots.Add(await surface.Diagnostics());
                    await WriteDiagnostics(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-diagnostics.json"));
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-displays.json"),"[" + string.Join(",",snapshots) + "]");
                    prefs.Scene = "riverscape";
                    prefs.ScreenScenes[Screen.PrimaryScreen.DeviceName] = "reefscape";
                    RefreshSurfaces(); await Task.Delay(1000);
                    for(int i=0;i<120 && surfaces.Any(x => !x.Ready);i++) await Task.Delay(500);
                    var primaryIndex = Array.FindIndex(Screen.AllScreens,x => x.Primary);
                    bool independent = await surfaces[primaryIndex].CheckScene("reefscape");
                    surfaces[0].Close(); tick = 29; await Tick();
                    for(int i=0;i<120 && surfaces.Any(x => !x.Ready);i++) await Task.Delay(500);
                    bool recovered = surfaces.Count == Screen.AllScreens.Length && surfaces.All(x => x.Attached && x.Ready);
                    Detach();
                    bool detached = !wallpaper && surfaces.Count == 0 && Visible;
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-desktop.json"),"{\"attached\":" + attached.ToString().ToLowerInvariant() + ",\"detached\":" + detached.ToString().ToLowerInvariant() + ",\"independentScene\":" + independent.ToString().ToLowerInvariant() + ",\"recovered\":" + recovered.ToString().ToLowerInvariant() + ",\"screenCount\":" + Screen.AllScreens.Length + "}");
                    if(!attached || !detached || !independent || !recovered) throw new Exception("桌面挂载、独立场景、恢复或还原失败");
                } else File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-desktop.json"),"{\"skipped\":true,\"reason\":\"WorkerW unavailable\"}");
                Close();
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            closing = true; timer.Stop(); timer.Dispose(); tray.Visible = false; tray.Dispose();
            SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            foreach(var surface in surfaces.ToArray()) if(!surface.IsDisposed) surface.Close(); surfaces.Clear(); web.Dispose(); base.OnFormClosing(e);
        }
    }
    static class Native {
        internal delegate bool EnumWindowsProc(IntPtr h,IntPtr p);
        [StructLayout(LayoutKind.Sequential)] internal struct RECT { internal int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { internal int X,Y; }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string c,string t);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr p,IntPtr a,string c,string t);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc f,IntPtr p);
        [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr SetParent(IntPtr child,IntPtr parent);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] internal static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")] internal static extern int SetWindowLong(IntPtr h,int index,int value);
        [DllImport("kernel32.dll")] internal static extern void SetLastError(uint e);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool ScreenToClient(IntPtr h,ref POINT p);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr z,int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h,out RECT r);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr h);
        [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr RegisterPowerSettingNotification(IntPtr h,ref Guid setting,uint flags);
        [DllImport("user32.dll")] internal static extern bool UnregisterPowerSettingNotification(IntPtr h);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder name,int size);
        internal static bool IsDesktop(IntPtr h) { var name=new System.Text.StringBuilder(128); GetClassName(h,name,name.Capacity); return name.ToString()=="WorkerW" || name.ToString()=="Progman" || FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null) != IntPtr.Zero; }
        internal static IntPtr FindWallpaperHost() {
            var progman = FindWindow("Progman",null); if(progman == IntPtr.Zero) return IntPtr.Zero;
            IntPtr result; SendMessageTimeout(progman,0x052C,IntPtr.Zero,IntPtr.Zero,2,1000,out result);
            // Windows 11 24H2+ can place WorkerW inside Progman instead of beside it.
            var childWorker = FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);
            if(childWorker != IntPtr.Zero) return childWorker;
            SendMessageTimeout(progman,0x052C,new IntPtr(0xD),IntPtr.Zero,2,1000,out result);
            SendMessageTimeout(progman,0x052C,new IntPtr(0xD),new IntPtr(1),2,1000,out result);
            childWorker = FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);
            if(childWorker != IntPtr.Zero) return childWorker;
            IntPtr worker = IntPtr.Zero;
            EnumWindows((h,p) => { if(FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null) != IntPtr.Zero) worker = FindWindowEx(IntPtr.Zero,h,"WorkerW",null); return true; },IntPtr.Zero);
            return worker;
        }
    }
}
