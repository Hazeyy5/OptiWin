using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;
using OptiWin.Models;

namespace OptiWin.Services;

/// <summary>État partagé entre les pages.</summary>
public static class AppState
{
    public static readonly List<Tweak> Tweaks = TweakService.CreateTweaks();
    public static readonly List<CleanupCategory> Cleanup = CleanupService.CreateCategories();
}

public static class RestorePointService
{
    /// <summary>Crée un point de restauration système (nécessite les droits administrateur).</summary>
    public static async Task<bool> CreateAsync(string description = "OptiWin - avant optimisation")
    {
        Log.Write("Création d'un point de restauration…");
        return await Task.Run(() =>
        {
            // Windows limite à un point de restauration par 24 h par défaut : on lève cette limite.
            Reg.Apply(new RegValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore",
                "SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord, null));

            var drive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var script = $"Enable-ComputerRestore -Drive '{drive}' -ErrorAction SilentlyContinue; " +
                         $"Checkpoint-Computer -Description '{description}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop";
            var r = ProcessHelper.Run("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"", 300_000);
            var ok = r.ExitCode == 0;
            Log.Write(ok ? "Point de restauration créé." : $"Point de restauration impossible : {r.Output.Trim()}");
            return ok;
        });
    }
}

public static class UpdateService
{
    // ⚠️ À adapter si le dépôt GitHub change de nom ou de propriétaire.
    public const string RepoOwner = "Hazeyy5";
    public const string RepoName = "OptiWin";
    public static string RepoUrl => $"https://github.com/{RepoOwner}/{RepoName}";

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public static string CurrentVersionText => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    /// <summary>Renvoie (version, url) si une version plus récente est publiée sur GitHub.</summary>
    public static async Task<(string Version, string Url)?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("OptiWin-UpdateCheck");
            var json = await http.GetStringAsync($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var url = doc.RootElement.GetProperty("html_url").GetString() ?? RepoUrl;
            if (Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
            {
                var cur = CurrentVersion;
                if (latest > new Version(cur.Major, cur.Minor, Math.Max(cur.Build, 0))) return (tag, url);
            }
        }
        catch { /* hors ligne ou dépôt introuvable : on ignore */ }
        return null;
    }
}
