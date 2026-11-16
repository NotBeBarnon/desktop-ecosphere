using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopEcosphere {
    sealed class SettingsForm : Form {
        public readonly Preferences Result;
        public bool Startup;
        readonly TableLayoutPanel general = Page();
        readonly TableLayoutPanel displays = Page();
        readonly TableLayoutPanel energy = Page();
        readonly TabControl tabs=new TabControl { Dock=DockStyle.Fill };
        readonly System.Windows.Forms.Timer refresh=new System.Windows.Forms.Timer();
        Action readDraft;
        Action<Preferences> loadDraft;
        readonly System.Threading.CancellationTokenSource updateCancel=new System.Threading.CancellationTokenSource();
        Button updateButton;
        Label updateResult;
        static TableLayoutPanel Page() { return new TableLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,ColumnCount=2,Padding=new Padding(20),AutoSize=false }; }
        static ComboBox Choice(string[] items,int selected) { var c=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=220 }; c.Items.AddRange(items); c.SelectedIndex=selected; return c; }
        static void Row(TableLayoutPanel page,string text,Control control) { int row=page.RowCount++; page.RowStyles.Add(new RowStyle(SizeType.AutoSize)); page.Controls.Add(new Label { Text=text,AutoSize=true,Margin=new Padding(0,8,18,16) },0,row); page.Controls.Add(control,1,row); }
        public SettingsForm(Preferences source,Func<string> runtime=null,Action export=null,Func<System.Threading.CancellationToken,System.Threading.Tasks.Task<UpdateInfo>> updateQuery=null) {
            Result = new Preferences { Scene=source.Scene,Quality=source.Quality,Fps=source.Fps,Paused=source.Paused,Wallpaper=source.Wallpaper,AllScreens=source.AllScreens,ScreenScenes=new Dictionary<string,string>(source.ScreenScenes),BatteryMode=source.BatteryMode,Occlusion=source.Occlusion };
            Result.FeedPortions=source.FeedPortions; Result.AutoFeedMinutes=source.AutoFeedMinutes; Result.PointerInteraction=source.PointerInteraction;
            Result.Population=source.Population;
            Result.Brightness=source.Brightness; Result.DayNight=source.DayNight;
            Result.CustomPopulation=source.CustomPopulation; Result.RiverFish=source.RiverFish; Result.ChromisFish=source.ChromisFish; Result.AnthiasFish=source.AnthiasFish;
            Text="桌面生态缸 · 设置"; ClientSize=new Size(670,520); MinimumSize=new Size(640,500); StartPosition=FormStartPosition.CenterParent;
            Font=new Font("Microsoft YaHei UI",10); AutoScaleMode=AutoScaleMode.Dpi;
            Controls.Add(tabs);
            foreach(var pair in new[]{Tuple.Create("常规",general),Tuple.Create("显示器",displays),Tuple.Create("节能",energy)}) { var tab=new TabPage(pair.Item1); tab.Controls.Add(pair.Item2); tabs.TabPages.Add(tab); }
            var scene=Choice(new[]{"淡水生态缸","珊瑚海水缸"},source.Scene=="reefscape"?1:0); Row(general,"默认场景",scene);
            var quality=Choice(new[]{"节能","均衡","精细"},source.Quality=="eco"?0:source.Quality=="detail"?2:1); Row(general,"画质",quality);
            var fps=Choice(new[]{"15 FPS","30 FPS","60 FPS"},source.Fps==15?0:source.Fps==60?2:1); Row(general,"帧率上限",fps);
            var population=Choice(new[]{"清静","标准","热闹"},source.Population=="sparse"?0:source.Population=="lively"?2:1); Row(general,"鱼群密度",population);
            Row(general,"数量说明",new Label { Text="淡水：12 / 24 / 36 条。\n海水：12 / 19 / 28 条，另有 2 只虾。\n修改密度会重新加载场景。",AutoSize=true });
            var biology=Page(); var biologyTab=new TabPage("生物数量"); biologyTab.Controls.Add(biology); tabs.TabPages.Add(biologyTab);
            var custom=new CheckBox { Text="使用各鱼种数量，替代密度档位",AutoSize=true,Checked=source.CustomPopulation }; Row(biology,"数量模式",custom);
            var riverFish=new NumericUpDown {Minimum=1,Maximum=48,Value=source.RiverFish,Width=160}; Row(biology,"淡水群游鱼",riverFish);
            var chromisFish=new NumericUpDown {Minimum=2,Maximum=18,Value=source.ChromisFish,Width=160}; Row(biology,"蓝绿群游鱼（Chromis）",chromisFish);
            var anthiasFish=new NumericUpDown {Minimum=2,Maximum=14,Value=source.AnthiasFish,Width=160}; Row(biology,"橙紫群游鱼（Anthias）",anthiasFish);
            Action updateMode=()=>{ population.Enabled=!custom.Checked; riverFish.Enabled=chromisFish.Enabled=anthiasFish.Enabled=custom.Checked; }; custom.CheckedChanged+=(s,e)=>updateMode(); updateMode();
            Row(biology,"说明",new Label {Text="淡水 1–48 条；两类海水群游鱼至少各 2 条。\n海水缸固定保留 3 条小丑鱼、2 只虾。\n设置对所有显示器生效，保存后重新加载场景。\n增加数量会提高模拟和渲染开销。",AutoSize=true});
            var startup=new CheckBox { Text="登录 Windows 后启动",AutoSize=true,Checked=StartupEnabled() }; Row(general,"开机启动",startup);
            Row(general,"说明",new Label { Text="画质、帧率受显卡和节能策略影响。\n设置在点击“保存”后生效。",AutoSize=true });
            var lights=Page(); var lightTab=new TabPage("灯光"); lightTab.Controls.Add(lights); tabs.TabPages.Add(lightTab);
            var brightness=new NumericUpDown { Minimum=25,Maximum=150,Increment=5,Value=source.Brightness,Width=160 }; Row(lights,"亮度（%）",brightness);
            var dayNight=new CheckBox { Text="随本地时间变化",AutoSize=true,Checked=source.DayNight }; Row(lights,"昼夜变化",dayNight);
            Row(lights,"说明",new Label { Text="100% 保持原场景观感，范围 25%–150%。\n昼夜模式：6–8 点渐亮，18–20 点渐暗。\n夜间为设定亮度的 35%，不完全熄灯。\n停止渲染时不持续刷新，恢复后同步当前时间。\n设置保存后生效，不改变生物行为或模拟时间。",AutoSize=true });
            var interaction=Page(); var interactionTab=new TabPage("互动与投食"); interactionTab.Controls.Add(interaction); tabs.TabPages.Add(interactionTab);
            var portions=Choice(new[]{"少量（1 份）","适量（2 份）","较多（3 份）"},Math.Max(0,Math.Min(2,source.FeedPortions-1))); Row(interaction,"每次投食",portions);
            int[] intervals={0,1,5,15,30,60}; var interval=Choice(new[]{"关闭","每 1 分钟","每 5 分钟","每 15 分钟","每 30 分钟","每 60 分钟"},Math.Max(0,Array.IndexOf(intervals,source.AutoFeedMinutes))); Row(interaction,"自动投食",interval);
            var pointer=new CheckBox { Text="鱼群响应鼠标、预览窗口点击投食",AutoSize=true,Checked=source.PointerInteraction }; Row(interaction,"鼠标互动",pointer);
            Row(interaction,"说明",new Label { Text="自动投食仅在场景运行时生效。\n暂停、关屏或被遮挡期间不会累积补投。\n每份沿用场景原有投食量；手动投食后重新计时。",AutoSize=true });
            var layout=new MonitorLayout(); Row(displays,"当前屏幕布局",layout);
            var all=new CheckBox { Text="所有显示器（关闭时仅主屏）",AutoSize=true,Checked=source.AllScreens }; Row(displays,"运行范围",all);
            var screenPicks=new Dictionary<string,ComboBox>();
            foreach(var display in Screen.AllScreens) { string device=display.DeviceName, saved; source.ScreenScenes.TryGetValue(device,out saved);
                var pick=Choice(new[]{"跟随默认场景","淡水生态缸","珊瑚海水缸"},saved=="riverscape"?1:saved=="reefscape"?2:0);
                screenPicks[device]=pick;
                Row(displays,device+(display.Primary?" · 主屏":"")+"\n"+display.Bounds.Width+" × "+display.Bounds.Height,pick);
                pick.SelectedIndexChanged+=(s,e)=>{ if(pick.SelectedIndex==0) Result.ScreenScenes.Remove(device); else Result.ScreenScenes[device]=pick.SelectedIndex==1?"riverscape":"reefscape"; };
            }
            var battery=Choice(new[]{"电池时最高 15 FPS","电池时暂停","沿用帧率上限"},source.BatteryMode=="pause"?1:source.BatteryMode=="normal"?2:0); Row(energy,"电池策略",battery);
            var covered=new CheckBox { Text="窗口遮挡时自动降帧或暂停",AutoSize=true,Checked=source.Occlusion }; Row(energy,"桌面遮挡",covered);
            Row(energy,"自动节能",new Label { Text="锁屏、休眠、显示器关闭时停止渲染。\n遮挡 ≥ 70%：最高 10 FPS；≥ 98%：暂停。\n多屏分别估算遮挡面积。",AutoSize=true });
            var diagnostics=Page(); var diagnosticTab=new TabPage("运行状态"); diagnosticTab.Controls.Add(diagnostics); tabs.TabPages.Add(diagnosticTab);
            var state=new Label { AutoSize=true,MaximumSize=new Size(440,0),Text=runtime==null?"尚未连接运行状态":runtime() }; Row(diagnostics,"当前状态",state);
            var save=new Button { Text="导出诊断文件…",AutoSize=true,Enabled=export!=null }; save.Click+=(s,e)=>{ if(export!=null) export(); }; Row(diagnostics,"问题排查",save);
            Row(diagnostics,"说明",new Label { Text="诊断包含系统版本、屏幕布局和渲染统计。\n文件仅保存在你选择的位置。",AutoSize=true });
            refresh.Interval=1000; refresh.Tick+=(s,e)=>{ if(runtime!=null) state.Text=runtime(); }; refresh.Start();
            FormClosed+=(s,e)=>{ refresh.Stop(); refresh.Dispose(); };
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=58,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10) }; Controls.Add(buttons);
            var ok=new Button { Text="保存",AutoSize=true }; var cancel=new Button { Text="取消",DialogResult=DialogResult.Cancel,AutoSize=true }; buttons.Controls.Add(ok); buttons.Controls.Add(cancel); AcceptButton=ok; CancelButton=cancel;
            readDraft=()=>{ Result.CustomPopulation=custom.Checked; Result.RiverFish=(int)riverFish.Value; Result.ChromisFish=(int)chromisFish.Value; Result.AnthiasFish=(int)anthiasFish.Value; Result.Scene=scene.SelectedIndex==0?"riverscape":"reefscape"; Result.Quality=new[]{"eco","balanced","detail"}[quality.SelectedIndex]; Result.Fps=new[]{15,30,60}[fps.SelectedIndex]; Result.AllScreens=all.Checked; Result.BatteryMode=new[]{"limit","pause","normal"}[battery.SelectedIndex]; Result.Occlusion=covered.Checked; Result.FeedPortions=portions.SelectedIndex+1; Result.AutoFeedMinutes=intervals[interval.SelectedIndex]; Result.PointerInteraction=pointer.Checked; Result.Population=new[]{"sparse","standard","lively"}[population.SelectedIndex]; Result.Brightness=(int)brightness.Value; Result.DayNight=dayNight.Checked; Startup=startup.Checked; };
            loadDraft=p=>{ custom.Checked=p.CustomPopulation; riverFish.Value=p.RiverFish; chromisFish.Value=p.ChromisFish; anthiasFish.Value=p.AnthiasFish; brightness.Value=p.Brightness; dayNight.Checked=p.DayNight; scene.SelectedIndex=p.Scene=="reefscape"?1:0; quality.SelectedIndex=Array.IndexOf(new[]{"eco","balanced","detail"},p.Quality); fps.SelectedIndex=Array.IndexOf(new[]{15,30,60},p.Fps); population.SelectedIndex=Array.IndexOf(new[]{"sparse","standard","lively"},p.Population); all.Checked=p.AllScreens; battery.SelectedIndex=Array.IndexOf(new[]{"limit","pause","normal"},p.BatteryMode); covered.Checked=p.Occlusion; portions.SelectedIndex=p.FeedPortions-1; interval.SelectedIndex=Array.IndexOf(intervals,p.AutoFeedMinutes); pointer.Checked=p.PointerInteraction; Result.ScreenScenes=new Dictionary<string,string>(p.ScreenScenes); foreach(var displayPick in screenPicks) { string selected; p.ScreenScenes.TryGetValue(displayPick.Key,out selected); displayPick.Value.SelectedIndex=selected=="riverscape"?1:selected=="reefscape"?2:0; } readDraft(); };
            ok.Click+=(s,e)=>{ readDraft(); DialogResult=DialogResult.OK; };
            var transfer=Page(); var transferTab=new TabPage("设置文件"); transferTab.Controls.Add(transfer); tabs.TabPages.Add(transferTab);
            var importButton=new Button { Text="导入设置…",AutoSize=true };
            importButton.Click+=(s,e)=>{ using(var file=new OpenFileDialog { Filter="设置文件 (*.json)|*.json" }) { if(file.ShowDialog(this)!=DialogResult.OK) return; try { loadDraft(SettingsProfile.Read(file.FileName)); MessageBox.Show("设置已载入，点击保存后生效。","桌面生态缸"); } catch(Exception error) { MessageBox.Show("导入失败："+error.Message,"桌面生态缸"); } } };
            Row(transfer,"导入",importButton);
            var exportButton=new Button { Text="导出当前设置…",AutoSize=true };
            exportButton.Click+=(s,e)=>{ using(var file=new SaveFileDialog { Filter="设置文件 (*.json)|*.json",FileName="desktop-ecosphere-settings.json" }) { if(file.ShowDialog(this)!=DialogResult.OK) return; try { SettingsProfile.Write(file.FileName,ExportDraft()); MessageBox.Show("设置文件已保存。","桌面生态缸"); } catch(Exception error) { MessageBox.Show("导出失败："+error.Message,"桌面生态缸"); } } };
            Row(transfer,"导出",exportButton);
            var defaults=new Button { Text="恢复默认设置",AutoSize=true };
            defaults.Click+=(s,e)=>{ if(MessageBox.Show("恢复场景、互动和节能的默认值？点击保存后生效。","恢复默认",MessageBoxButtons.OKCancel)==DialogResult.OK) ResetDraft(); };
            Row(transfer,"恢复默认",defaults);
            Row(transfer,"说明",new Label { Text="导入和恢复默认只修改设置草稿，保存后生效。\n导出包含尚未保存的界面设置。\n开机启动和当前壁纸/暂停状态不在设置文件中。\n恢复默认保留开机启动选项；配置文件兼容旧版本。",AutoSize=true });
            var about=Page(); var aboutTab=new TabPage("关于与更新"); aboutTab.Controls.Add(about); tabs.TabPages.Add(aboutTab);
            Row(about,"桌面生态缸",new Label { Text="Desktop Ecosphere v"+AppInfo.Version+" · Windows x64",AutoSize=true });
            Row(about,"运行环境",new Label { Text=Environment.OSVersion.VersionString+"\nCLR "+Environment.Version,AutoSize=true });
            var result=new Label { Text="仅在点击检查时联网，不自动下载或安装。",AutoSize=true,MaximumSize=new Size(420,0) };
            var check=new Button { Text="检查更新",AutoSize=true }; var open=new Button { Text="打开版本页",AutoSize=true };
            updateButton=check; updateResult=result;
            string updatePage=AppInfo.Repository+"/releases";
            check.Click+=async (s,e)=>{ check.Enabled=false; result.Text="正在查询 GitHub…";
                try { var info=await (updateQuery==null?UpdateChecker.Check(updateCancel.Token):updateQuery(updateCancel.Token)); if(IsDisposed || Disposing) return; result.Text=info.Message; updatePage=info.Page; open.Text=info.TagOnly?"查看标签源码":"打开下载页"; }
                catch(OperationCanceledException) { if(!IsDisposed && !Disposing) result.Text="检查已取消或超时，可稍后重试。"; }
                catch(Exception error) { if(!IsDisposed && !Disposing) result.Text=error is System.Net.Http.HttpRequestException?"无法连接 GitHub 或访问受限，请稍后重试。":"版本信息不可用，请打开版本页查看。"; }
                finally { if(!IsDisposed && !Disposing) check.Enabled=true; }
            };
            open.Click+=(s,e)=>OpenPage(updatePage); Row(about,"版本检查",check); Row(about,"检查结果",result); Row(about,"下载与历史",open);
            var sourceButton=new Button { Text="打开项目仓库",AutoSize=true }; sourceButton.Click+=(s,e)=>OpenPage(AppInfo.Repository); Row(about,"源码与反馈",sourceButton);
            Row(about,"许可",new Label { Text="MIT · 原始场景来自 Chase Lean / Desktop Habitats。\n构建包附带本项目、上游及 WebView2 许可。",AutoSize=true });
            FormClosed+=(s,e)=>{ updateCancel.Cancel(); updateCancel.Dispose(); };
        }
        internal bool CheckUpdateForTest() { tabs.SelectedIndex=tabs.TabPages.Count-1; tabs.Update(); updateButton.PerformClick(); return updateResult.Text.Contains("最新稳定标签"); }
        void OpenPage(string url) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) {UseShellExecute=true}); } catch(Exception error) { MessageBox.Show("无法打开浏览器："+error.Message); } }
        internal string ExportDraft() { readDraft(); return SettingsProfile.Export(Result); }
        internal void ImportDraft(string json) { loadDraft(SettingsProfile.Parse(json)); }
        internal void ResetDraft() { loadDraft(new Preferences()); }
        internal void CaptureTabs(string folder) {
            for(int i=0;i<tabs.TabPages.Count;i++) { tabs.SelectedIndex=i; tabs.Update(); Update(); using(var bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(System.IO.Path.Combine(folder,"smoke-settings-"+i+".png")); } }
        }
        internal static bool StartupEnabled() { using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key!=null && key.GetValue("DesktopHabitatsWindows")!=null; }
        internal static void SetStartup(bool enabled) { using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) { if(enabled) key.SetValue("DesktopHabitatsWindows","\""+Application.ExecutablePath+"\""); else key.DeleteValue("DesktopHabitatsWindows",false); } }
    }
    sealed class MonitorLayout : Control {
        public MonitorLayout() { Size=new Size(340,150); DoubleBuffered=true; }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var screens=Screen.AllScreens; Rectangle union=screens.Select(x=>x.Bounds).Aggregate(Rectangle.Union);
            float scale=Math.Min((Width-20f)/union.Width,(Height-20f)/union.Height);
            for(int i=0;i<screens.Length;i++) { var b=screens[i].Bounds; var r=new RectangleF(10+(b.Left-union.Left)*scale,10+(b.Top-union.Top)*scale,b.Width*scale-3,b.Height*scale-3);
                e.Graphics.FillRectangle(screens[i].Primary?Brushes.Teal:Brushes.SlateGray,r); e.Graphics.DrawString((i+1)+(screens[i].Primary?" 主屏":""),Font,Brushes.White,r); }
        }
    }
}
