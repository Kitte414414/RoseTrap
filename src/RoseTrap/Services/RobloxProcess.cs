using System.Diagnostics;

namespace RoseTrap.Services;

public static class RobloxProcess
{
    public static bool IsRunning()
    {
        var procs = Process.GetProcessesByName("RobloxPlayerBeta");
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }
}
