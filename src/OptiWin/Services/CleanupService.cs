using OptiWin.Models;

namespace OptiWin.Services;

public static class CleanupService
{
    static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";
    static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static List<CleanupCategory> CreateCategories() => new()
    {
        Folder("Fichiers temporaires (utilisateur)", "Fichiers laissés par les applications dans %TEMP%.", true,
            () => new[] { Path.GetTempPath() }),

        Folder("Fichiers temporaires (Windows)", "Contenu de C:\\Windows\\Temp.", true,
            () => new[] { Path.Combine(WinDir, "Temp") }),

        new CleanupCategory
        {
            Name = "Cache de Windows Update",
            Description = "Mises à jour déjà installées et téléchargées. Peut libérer plusieurs Go.",
            IsSelected = true,
            Measure = () => SizeOf(Path.Combine(WinDir, "SoftwareDistribution", "Download")),
            Clean = () =>
            {
                ProcessHelper.Run("net", "stop wuauserv /y");
                ProcessHelper.Run("net", "stop bits /y");
                try { return DeleteContents(Path.Combine(WinDir, "SoftwareDistribution", "Download")); }
                finally
                {
                    ProcessHelper.Run("net", "start bits");
                    ProcessHelper.Run("net", "start wuauserv");
                }
            },
        },

        Folder("Optimisation de la distribution", "Cache de partage des mises à jour entre PC.", true,
            () => new[] { Path.Combine(WinDir, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache") }),

        Folder("Rapports d'erreurs Windows", "Rapports de plantage et fichiers de diagnostic.", true,
            () => new[] { Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportArchive"), Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportQueue"), Path.Combine(LocalAppData, "CrashDumps") }),

        Folder("Cache des shaders DirectX", "Recréé automatiquement, mais les jeux saccadent un peu le temps de le reconstruire. À vider en cas de bug graphique ou de manque de place.", false,
            () => new[] { Path.Combine(LocalAppData, "D3DSCache"), Path.Combine(LocalAppData, @"NVIDIA\DXCache"), Path.Combine(LocalAppData, @"AMD\DxCache") }),

        new CleanupCategory
        {
            Name = "Cache des miniatures",
            Description = "Aperçus d'images de l'Explorateur. Recréé automatiquement.",
            IsSelected = true,
            Measure = () => SizeOf(Path.Combine(LocalAppData, @"Microsoft\Windows\Explorer"), "thumbcache_*.db"),
            Clean = () => DeleteContents(Path.Combine(LocalAppData, @"Microsoft\Windows\Explorer"), "thumbcache_*.db"),
        },

        Folder("Journaux Windows", "Anciens fichiers journaux du système (CBS, DISM...).", false,
            () => new[] { Path.Combine(WinDir, @"Logs\CBS"), Path.Combine(WinDir, @"Logs\DISM") }),

        Folder("Cache des navigateurs", "Chrome, Edge, Brave, Opera, Firefox. Fermez vos navigateurs avant. Vos mots de passe et historique sont conservés.", false,
            BrowserCacheFolders),

        new CleanupCategory
        {
            Name = "Corbeille",
            Description = "Supprime définitivement le contenu de la corbeille.",
            IsSelected = false,
            Measure = () =>
            {
                var info = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.SHQUERYRBINFO>() };
                return Native.SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
            },
            Clean = () =>
            {
                var info = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.SHQUERYRBINFO>() };
                Native.SHQueryRecycleBin(null, ref info);
                Native.SHEmptyRecycleBin(IntPtr.Zero, null, 0x7); // sans confirmation, sans progression, sans son
                return info.i64Size;
            },
        },
    };

    static CleanupCategory Folder(string name, string desc, bool selected, Func<IEnumerable<string>> folders) => new()
    {
        Name = name,
        Description = desc,
        IsSelected = selected,
        Measure = () => folders().Sum(f => SizeOf(f)),
        Clean = () => folders().Sum(f => DeleteContents(f)),
    };

    static IEnumerable<string> BrowserCacheFolders()
    {
        var chromiumRoots = new[]
        {
            @"Google\Chrome\User Data",
            @"Microsoft\Edge\User Data",
            @"BraveSoftware\Brave-Browser\User Data",
            @"Vivaldi\User Data",
        };
        foreach (var root in chromiumRoots.Select(r => Path.Combine(LocalAppData, r)).Where(Directory.Exists))
        {
            IEnumerable<string> profiles;
            try { profiles = Directory.GetDirectories(root); } catch { continue; }
            foreach (var profile in profiles)
                foreach (var sub in new[] { @"Cache", @"Code Cache", @"GPUCache", @"Service Worker\CacheStorage" })
                    yield return Path.Combine(profile, sub);
        }

        var opera = Path.Combine(LocalAppData, @"Opera Software");
        if (Directory.Exists(opera))
            foreach (var p in Directory.GetDirectories(opera))
            {
                yield return Path.Combine(p, "Cache");
                yield return Path.Combine(p, "Code Cache");
            }

        var firefox = Path.Combine(LocalAppData, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefox))
            foreach (var p in Directory.GetDirectories(firefox))
                yield return Path.Combine(p, "cache2");
    }

    // --- Utilitaires de parcours tolérants aux erreurs (fichiers verrouillés, accès refusé...) ---

    static readonly EnumerationOptions Opts = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint, // ne jamais suivre les liens/jonctions
    };

    static readonly EnumerationOptions TopOnly = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public static long SizeOf(string dir, string pattern = "*")
    {
        if (!Directory.Exists(dir)) return 0;
        long total = 0;
        try
        {
            foreach (var f in new DirectoryInfo(dir).EnumerateFiles(pattern, pattern == "*" ? Opts : TopOnly))
                try { total += f.Length; } catch { }
        }
        catch { }
        return total;
    }

    /// <summary>Supprime le contenu d'un dossier (jamais le dossier lui-même). Renvoie le nombre d'octets libérés.</summary>
    public static long DeleteContents(string dir, string pattern = "*")
    {
        if (!Directory.Exists(dir)) return 0;
        long freed = 0;
        var root = new DirectoryInfo(dir);

        try
        {
            foreach (var f in root.EnumerateFiles(pattern, pattern == "*" ? Opts : TopOnly))
            {
                try
                {
                    var len = f.Length;
                    if (f.IsReadOnly) f.IsReadOnly = false;
                    f.Delete();
                    freed += len;
                }
                catch { /* fichier en cours d'utilisation : ignoré */ }
            }
        }
        catch { }

        if (pattern == "*")
        {
            try
            {
                // Supprime les sous-dossiers vides, du plus profond au moins profond
                foreach (var d in root.EnumerateDirectories("*", Opts).OrderByDescending(d => d.FullName.Length))
                    try { if (!d.EnumerateFileSystemInfos().Any()) d.Delete(); } catch { }
            }
            catch { }
        }
        return freed;
    }
}
