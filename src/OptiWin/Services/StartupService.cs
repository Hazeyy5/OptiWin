using Microsoft.Win32;
using OptiWin.Models;

namespace OptiWin.Services;

/// <summary>
/// Gère les programmes au démarrage exactement comme le Gestionnaire des tâches :
/// via les clés "StartupApproved", sans jamais supprimer l'entrée d'origine (100 % réversible).
/// </summary>
public static class StartupService
{
    const string Run = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    const string Approved = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    public static List<StartupItem> List()
    {
        var items = new List<StartupItem>();
        FromRegistry(items, RegistryHive.CurrentUser, RegistryView.Registry64, "Utilisateur", Approved + "Run");
        FromRegistry(items, RegistryHive.LocalMachine, RegistryView.Registry64, "Tous les utilisateurs", Approved + "Run");
        FromRegistry(items, RegistryHive.LocalMachine, RegistryView.Registry32, "Tous les utilisateurs (32 bits)", Approved + "Run32");
        FromFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, "Dossier Démarrage");
        FromFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, "Dossier Démarrage (commun)");
        return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static void FromRegistry(List<StartupItem> items, RegistryHive hive, RegistryView view, string location, string approvedPath)
    {
        try
        {
            using var b = RegistryKey.OpenBaseKey(hive, view);
            using var k = b.OpenSubKey(Run);
            if (k == null) return;
            foreach (var name in k.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                items.Add(new StartupItem
                {
                    Name = name,
                    Command = k.GetValue(name)?.ToString() ?? "",
                    Location = location,
                    ApprovedHive = hive,
                    ApprovedPath = approvedPath,
                    IsEnabled = IsApproved(hive, approvedPath, name),
                });
            }
        }
        catch (Exception ex) { Log.Write($"Lecture du démarrage impossible ({location}) : {ex.Message}"); }
    }

    static void FromFolder(List<StartupItem> items, string folder, RegistryHive hive, string location)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var file in Directory.GetFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
            items.Add(new StartupItem
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Command = file,
                Location = location,
                ApprovedHive = hive,
                ApprovedPath = Approved + "StartupFolder",
                IsEnabled = IsApproved(hive, Approved + "StartupFolder", fileName),
            });
        }
    }

    static string ValueName(StartupItem item) =>
        item.ApprovedPath.EndsWith("StartupFolder") ? Path.GetFileName(item.Command) : item.Name;

    static bool IsApproved(RegistryHive hive, string path, string valueName)
    {
        // Absent = activé. Premier octet impair (0x03, 0x07...) = désactivé.
        return Reg.Get(hive, path, valueName) is not byte[] data || data.Length == 0 || data[0] % 2 == 0;
    }

    public static void SetEnabled(StartupItem item, bool enabled)
    {
        var data = new byte[12];
        data[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled) BitConverter.GetBytes(DateTime.Now.ToFileTimeUtc()).CopyTo(data, 4);

        using var b = RegistryKey.OpenBaseKey(item.ApprovedHive, RegistryView.Registry64);
        using var k = b.CreateSubKey(item.ApprovedPath, true);
        k.SetValue(ValueName(item), data, RegistryValueKind.Binary);
        item.IsEnabled = enabled;
        Log.Write($"Démarrage : « {item.Name} » {(enabled ? "activé" : "désactivé")}");
    }
}
