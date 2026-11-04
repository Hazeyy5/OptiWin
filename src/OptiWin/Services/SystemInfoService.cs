using Microsoft.Win32;

namespace OptiWin.Services;

public record DriveStat(string Name, string Label, long Total, long Free)
{
    public long Used => Total - Free;
    public double Percent => Total == 0 ? 0 : Used * 100.0 / Total;
    public string Summary => $"{Format.Bytes(Used)} utilisés sur {Format.Bytes(Total)} — {Format.Bytes(Free)} libres";
}

public static class SystemInfoService
{
    public static string OsName()
    {
        const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        var name = Reg.Get(RegistryHive.LocalMachine, key, "ProductName") as string ?? "Windows";
        var display = Reg.Get(RegistryHive.LocalMachine, key, "DisplayVersion") as string ?? "";
        var build = Environment.OSVersion.Version.Build;
        // Windows 11 indique toujours "Windows 10" dans ProductName
        if (build >= 22000) name = name.Replace("Windows 10", "Windows 11");
        return $"{name} {display} (build {build})".Trim();
    }

    public static string CpuName() =>
        (Reg.Get(RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") as string)?.Trim()
        ?? "Processeur inconnu";

    public static string GpuName()
    {
        const string cls = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        try
        {
            using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var k = b.OpenSubKey(cls);
            if (k == null) return "Inconnu";
            var names = new List<string>();
            foreach (var sub in k.GetSubKeyNames())
            {
                if (!sub.All(char.IsDigit)) continue;
                using var s = k.OpenSubKey(sub);
                if (s?.GetValue("DriverDesc") is string d && !names.Contains(d) && !d.Contains("Basic Display")) names.Add(d);
            }
            return names.Count == 0 ? "Inconnu" : string.Join(" + ", names);
        }
        catch { return "Inconnu"; }
    }

    public static (ulong Total, ulong Available, uint LoadPercent) Memory()
    {
        var m = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        Native.GlobalMemoryStatusEx(ref m);
        return (m.ullTotalPhys, m.ullAvailPhys, m.dwMemoryLoad);
    }

    public static TimeSpan Uptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public static string UptimeText()
    {
        var u = Uptime();
        return u.TotalDays >= 1 ? $"{(int)u.TotalDays} j {u.Hours} h {u.Minutes} min" : $"{u.Hours} h {u.Minutes} min";
    }

    public static List<DriveStat> Drives() =>
        DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => new DriveStat(d.Name.TrimEnd('\\'), string.IsNullOrEmpty(d.VolumeLabel) ? "Disque local" : d.VolumeLabel, d.TotalSize, d.TotalFreeSpace))
            .ToList();

    // --- Utilisation CPU (différence entre deux mesures de GetSystemTimes) ---
    static long _lastIdle, _lastTotal;

    public static double CpuUsage()
    {
        if (!Native.GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var total = kernel + user; // kernel inclut idle
        var dIdle = idle - _lastIdle;
        var dTotal = total - _lastTotal;
        _lastIdle = idle; _lastTotal = total;
        if (dTotal <= 0) return 0;
        return Math.Clamp((1.0 - (double)dIdle / dTotal) * 100.0, 0, 100);
    }
}
