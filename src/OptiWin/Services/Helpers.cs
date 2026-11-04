using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace OptiWin.Services;

/// <summary>Journal global affiché dans la barre d'état.</summary>
public static class Log
{
    public static event Action<string>? MessageLogged;
    public static readonly List<string> History = new();

    public static void Write(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (History) History.Add(line);
        MessageLogged?.Invoke(message);
    }
}

public static class ProcessHelper
{
    /// <summary>Lance un programme en arrière-plan (fenêtre cachée) et renvoie sa sortie.</summary>
    public static (int ExitCode, string Output) Run(string file, string args, int timeoutMs = 120_000)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(true); } catch { /* ignoré */ }
            return (-1, "Délai dépassé");
        }
        return (p.ExitCode, stdout.Result + stderr.Result);
    }

    /// <summary>Ouvre une console visible qui exécute la commande (pour les outils longs : SFC, DISM...).</summary>
    public static void RunInConsole(string command)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", $"/k \"{command}\"") { UseShellExecute = true });
    }

    public static void Open(string target, string args = "")
    {
        Process.Start(new ProcessStartInfo(target, args) { UseShellExecute = true });
    }
}

/// <summary>Une valeur de registre avec sa valeur "optimisée" et sa valeur d'origine (null = supprimer la valeur).</summary>
public record RegValue(RegistryHive Hive, string Path, string Name, object Value, RegistryValueKind Kind, object? Default);

public static class Reg
{
    static RegistryKey Base(RegistryHive hive) => RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

    public static object? Get(RegistryHive hive, string path, string name)
    {
        using var b = Base(hive);
        using var k = b.OpenSubKey(path);
        return k?.GetValue(name);
    }

    public static bool Matches(RegValue v) => Equals(Get(v.Hive, v.Path, v.Name), v.Value);

    public static void Apply(RegValue v)
    {
        using var b = Base(v.Hive);
        using var k = b.CreateSubKey(v.Path, true);
        k.SetValue(v.Name, v.Value, v.Kind);
    }

    public static void Restore(RegValue v)
    {
        using var b = Base(v.Hive);
        if (v.Default is null)
        {
            using var k = b.OpenSubKey(v.Path, true);
            k?.DeleteValue(v.Name, false);
        }
        else
        {
            using var k = b.CreateSubKey(v.Path, true);
            k.SetValue(v.Name, v.Default, v.Kind);
        }
    }
}

public static class Format
{
    public static string Bytes(long bytes)
    {
        string[] units = { "o", "Ko", "Mo", "Go", "To" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{v:0} {units[i]}" : $"{v:0.#} {units[i]}";
    }
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int[] pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    public const uint SPI_SETMOUSE = 0x0004;
    public const uint SPI_SETMENUSHOWDELAY = 0x006B;
    public const uint SPIF_UPDATEINIFILE = 0x01, SPIF_SENDCHANGE = 0x02;
}
