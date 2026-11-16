using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HabitatsSetup {
    static class Setup {
        const string Name="DesktopHabitatsWindows";
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopHabitatsWindows";
        static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","DesktopEcosphere");
        static bool testMode;
        static bool cleanupMode;
        [STAThread] static void Main(string[] args) {
            try {
                if(args.Length==2 && args[0]=="--finish-uninstall") {
                    cleanupMode=true;
                    try { System.Diagnostics.Process.GetProcessById(int.Parse(args[1])).WaitForExit(); } catch(ArgumentException) { }
                    Uninstall(); return;
                }
                if(args.Length==2 && args[0]=="--verify-payload") { Extract(args[1]); return; }
                if(args.Length==2 && args[0]=="--integration-test") {
                    Root=Path.GetFullPath(args[1]); if(string.Equals(Root,Path.GetPathRoot(Root),StringComparison.OrdinalIgnoreCase)) throw new Exception("测试目录不能是磁盘根目录。"); Root=Root.TrimEnd(Path.DirectorySeparatorChar); testMode=true;
                    if(Directory.Exists(Root)) throw new Exception("测试目录必须不存在。");
                    Install(); if(!File.Exists(Path.Combine(Root,"DesktopEcosphere.exe")) || !File.Exists(Path.Combine(Root,"Uninstall.exe"))) throw new Exception("安装资源不完整。");
                    File.WriteAllText(Path.Combine(Root,"user-created.txt"),"preserve");
                    Directory.CreateDirectory(Path.Combine(Root,"user-empty"));
                    string testLink=Path.Combine(Root,"test-shortcut.lnk"); Link(testLink);
                    if(!File.Exists(testLink) || !OwnShortcut(testLink)) throw new Exception("快捷方式创建或目标验证失败"); File.Delete(testLink);
                    Uninstall(); if(File.Exists(Path.Combine(Root,"DesktopEcosphere.exe")) || !File.Exists(Path.Combine(Root,"user-created.txt")) || !Directory.Exists(Path.Combine(Root,"user-empty"))) throw new Exception("卸载清理或用户文件保护失败。");
                    File.WriteAllText(Path.Combine(Root,"test-result.txt"),"PASS install, uninstall, preserve user files"); return;
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
#if UNINSTALL
                if(MessageBox.Show("卸载桌面生态缸？个人设置和缓存将保留。","卸载",MessageBoxButtons.OKCancel)!=DialogResult.OK) return;
                RequireClosed();
                string helper=Path.Combine(Path.GetTempPath(),"HabitatsCleanup-"+Guid.NewGuid().ToString("N")+".exe");
                File.Copy(Application.ExecutablePath,helper);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(helper,"--finish-uninstall "+System.Diagnostics.Process.GetCurrentProcess().Id) { UseShellExecute=false,CreateNoWindow=true });
#else
                var form=new Form { Text="桌面生态缸 v0.7 · 安装",ClientSize=new Size(560,250),StartPosition=FormStartPosition.CenterScreen,Font=new Font("Microsoft YaHei UI",10),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false };
                form.Controls.Add(new Label { Text="将为当前用户安装桌面生态缸\n\n安装位置："+Root+"\n\n需要 .NET Framework 4.8 和 WebView2 Runtime。\n安装后创建桌面和开始菜单快捷方式，不自动开启开机启动。",Bounds=new Rectangle(24,24,510,160) });
                var install=new Button { Text="安装",Bounds=new Rectangle(420,195,100,32) }; form.Controls.Add(install);
                install.Click+=(s,e)=>{ try { RequireClosed(); install.Enabled=false; Install(); MessageBox.Show("安装完成，可从桌面快捷方式启动。","桌面生态缸"); form.Close(); } catch(Exception error) { install.Enabled=true; MessageBox.Show(error.Message,"安装失败"); } }; Application.Run(form);
#endif
            } catch(Exception error) { Environment.ExitCode=1; if(args.Length>0) Console.Error.WriteLine(error); else MessageBox.Show(error.Message,"桌面生态缸"); }
        }
        static void RequireClosed() { using(var mutex=new Mutex(false,"Local\\DesktopHabitatsWindows")) { if(!mutex.WaitOne(0)) throw new Exception("请先通过系统托盘退出正在运行的生态缸，再重试。"); mutex.ReleaseMutex(); } }
        static string Safe(string root,string entry) { string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar; string full=Path.GetFullPath(Path.Combine(prefix,entry)); if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new Exception("安装包路径无效。"); return full; }
        static void Extract(string root) {
            Directory.CreateDirectory(root);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")) {
                if(stream==null) throw new Exception("缺少安装资源。");
                using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)) {
                    foreach(var entry in zip.Entries) { string path=Safe(root,entry.FullName); if(entry.Name.Length==0) { Directory.CreateDirectory(path); continue; } Directory.CreateDirectory(Path.GetDirectoryName(path)); entry.ExtractToFile(path,true); }
                    File.WriteAllLines(Path.Combine(root,".habitats-installed"),zip.Entries.Where(x=>x.Name.Length>0).Select(x=>x.FullName));
                }
            }
        }
        static void Install() {
            if(Directory.Exists(Root) && Directory.EnumerateFileSystemEntries(Root).Any() && !File.Exists(Path.Combine(Root,".habitats-installed"))) throw new Exception("安装目录中存在非本安装程序管理的文件，请先移走该目录。");
            Extract(Root);
            if(testMode) return;
            Link(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"桌面生态缸.lnk"));
            Link(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"桌面生态缸.lnk"));
            using(var key=Registry.CurrentUser.CreateSubKey(Key)) { key.SetValue("DisplayName","桌面生态缸"); key.SetValue("DisplayVersion","0.7.0"); key.SetValue("InstallLocation",Root); key.SetValue("UninstallString","\""+Path.Combine(Root,"Uninstall.exe")+"\""); key.SetValue("DisplayIcon",Path.Combine(Root,"DesktopEcosphere.exe")); key.SetValue("NoModify",1); key.SetValue("NoRepair",1); }
        }
        static void Link(string path) { Type type=Type.GetTypeFromProgID("WScript.Shell"); object shell=Activator.CreateInstance(type); object shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path}); var shortcutType=shortcut.GetType(); shortcutType.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{Path.Combine(Root,"DesktopEcosphere.exe")}); shortcutType.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{Root}); shortcutType.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        static bool OwnShortcut(string path) {
            if(!File.Exists(path)) return false;
            Type type=Type.GetTypeFromProgID("WScript.Shell"); object shell=Activator.CreateInstance(type); object shortcut=null;
            try { shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path}); string target=shortcut.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null) as string; return string.Equals(target,Path.Combine(Root,"DesktopEcosphere.exe"),StringComparison.OrdinalIgnoreCase); }
            finally { if(shortcut!=null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        }
        static void Uninstall() {
            if(!testMode && !cleanupMode && !string.Equals(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\'),Root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) throw new Exception("请从安装目录运行卸载程序。");
            string marker=Path.Combine(Root,".habitats-installed"); if(!File.Exists(marker)) throw new Exception("缺少安装文件清单，无法安全卸载。");
            var directories=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string entry in File.ReadAllLines(marker)) { string file=Safe(Root,entry); string parent=Path.GetDirectoryName(file); while(parent!=null && !string.Equals(parent,Root,StringComparison.OrdinalIgnoreCase)) { directories.Add(parent); parent=Path.GetDirectoryName(parent); } if(string.Equals(file,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)) continue; if(File.Exists(file)) File.Delete(file); }
            if(!testMode) {
            using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)) { if(run!=null && (run.GetValue(Name,"").ToString()).Contains(Path.Combine(Root,"DesktopEcosphere.exe"))) run.DeleteValue(Name,false); }
            foreach(var folder in new[]{Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.Programs}) { string link=Path.Combine(Environment.GetFolderPath(folder),"桌面生态缸.lnk"); if(OwnShortcut(link)) File.Delete(link); }
            Registry.CurrentUser.DeleteSubKeyTree(Key,false);
            }
            // Delete only empty application-owned directories. Keep the running uninstaller.
            foreach(string directory in directories.OrderByDescending(x=>x.Length)) if(Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            File.Delete(marker);
            if(!Directory.EnumerateFileSystemEntries(Root).Any()) Directory.Delete(Root);
        }
    }
}
