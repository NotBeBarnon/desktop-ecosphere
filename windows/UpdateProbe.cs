using System;
using System.Threading;
namespace DesktopEcosphere {
    static class UpdateProbe {
        static void Main() {
            try { var info=UpdateChecker.Check(CancellationToken.None).GetAwaiter().GetResult(); Console.WriteLine(info.Tag+" | "+info.Message+" | "+info.Page); }
            catch(Exception error) { Console.WriteLine("CHECK_UNAVAILABLE: "+error.Message); Environment.ExitCode=1; }
        }
    }
}
