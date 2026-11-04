using Microsoft.Win32;
using OptiWin.Models;
using static Microsoft.Win32.RegistryHive;
using static Microsoft.Win32.RegistryValueKind;

namespace OptiWin.Services;

public static class TweakService
{
    const string Perf = "Performances";
    const string Gaming = "Jeux";
    const string Privacy = "Confidentialité";
    const string Interface = "Interface & réactivité";
    const string Storage = "Stockage & énergie";

    const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    const string Multimedia = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    const string GamesTask = Multimedia + @"\Tasks\Games";
    const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    public static List<Tweak> CreateTweaks() => new()
    {
        // ---------------- Performances ----------------
        new Tweak
        {
            Name = "Mode d'alimentation « Performances élevées »",
            Description = "Le processeur reste à pleine fréquence au lieu d'économiser l'énergie. Idéal sur PC fixe (sur portable, la batterie dure moins).",
            Category = Perf, Recommended = true,
            Check = () => ProcessHelper.Run("powercfg", "/getactivescheme").Output.Contains(HighPerfGuid, StringComparison.OrdinalIgnoreCase),
            ApplyAction = () =>
            {
                var r = ProcessHelper.Run("powercfg", $"/setactive {HighPerfGuid}");
                if (r.ExitCode != 0)
                {
                    // Certains PC n'exposent pas le plan : on le recrée depuis le modèle Windows
                    ProcessHelper.Run("powercfg", $"-duplicatescheme {HighPerfGuid} {HighPerfGuid}");
                    ProcessHelper.Run("powercfg", $"/setactive {HighPerfGuid}");
                }
            },
            RevertAction = () => ProcessHelper.Run("powercfg", $"/setactive {BalancedGuid}"),
        },
        Registry("Applications plus prioritaires (réactivité système)",
            "Réserve moins de temps processeur aux tâches de fond et supprime la limitation du débit réseau pendant le multimédia.",
            Perf, true, false,
            new RegValue(LocalMachine, Multimedia, "SystemResponsiveness", 10, DWord, 20),
            new RegValue(LocalMachine, Multimedia, "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), DWord, 10)),
        Registry("Désactiver les applications en arrière-plan",
            "Empêche les applications du Microsoft Store de tourner en arrière-plan quand elles sont fermées.",
            Perf, true, false,
            new RegValue(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, DWord, null),
            new RegValue(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, DWord, null)),
        Registry("Planification GPU accélérée par le matériel",
            "Laisse la carte graphique gérer sa propre mémoire : moins de latence (cartes récentes, Windows 10 2004+).",
            Perf, false, true,
            new RegValue(LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, DWord, 1)),

        // ---------------- Jeux ----------------
        Registry("Activer le Mode Jeu",
            "Windows donne la priorité au jeu en cours et suspend les mises à jour pendant la partie.",
            Gaming, true, false,
            new RegValue(CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, DWord, null),
            new RegValue(CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, DWord, null)),
        Registry("Désactiver l'enregistrement Xbox Game DVR",
            "Arrête l'enregistrement vidéo permanent en arrière-plan qui coûte des FPS.",
            Gaming, true, false,
            new RegValue(CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0, DWord, 1),
            new RegValue(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, DWord, null)),
        Registry("Priorité maximale aux jeux",
            "Donne aux jeux une priorité CPU et GPU plus élevée dans le planificateur multimédia de Windows.",
            Gaming, true, false,
            new RegValue(LocalMachine, GamesTask, "GPU Priority", 8, DWord, 8),
            new RegValue(LocalMachine, GamesTask, "Priority", 6, DWord, 2),
            new RegValue(LocalMachine, GamesTask, "Scheduling Category", "High", RegistryValueKind.String, "Medium"),
            new RegValue(LocalMachine, GamesTask, "SFIO Priority", "High", RegistryValueKind.String, "Normal")),
        new Tweak
        {
            Name = "Désactiver l'accélération de la souris",
            Description = "Mouvements de souris 1:1, plus précis en jeu (désactive « Améliorer la précision du pointeur »).",
            Category = Gaming, Recommended = false,
            Check = () => Equals(Reg.Get(CurrentUser, @"Control Panel\Mouse", "MouseSpeed"), "0"),
            ApplyAction = () => Native.SystemParametersInfo(Native.SPI_SETMOUSE, 0, new[] { 0, 0, 0 }, Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE),
            RevertAction = () => Native.SystemParametersInfo(Native.SPI_SETMOUSE, 0, new[] { 6, 10, 1 }, Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE),
        },

        // ---------------- Interface ----------------
        new Tweak
        {
            Name = "Menus plus rapides",
            Description = "Réduit le délai d'ouverture des menus et sous-menus de 400 ms à 100 ms.",
            Category = Interface, Recommended = true,
            Check = () => Equals(Reg.Get(CurrentUser, @"Control Panel\Desktop", "MenuShowDelay"), "100"),
            ApplyAction = () => Native.SystemParametersInfo(Native.SPI_SETMENUSHOWDELAY, 100, IntPtr.Zero, Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE),
            RevertAction = () => Native.SystemParametersInfo(Native.SPI_SETMENUSHOWDELAY, 400, IntPtr.Zero, Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE),
        },
        Registry("Réduire les animations",
            "Désactive les animations de fenêtres et de la barre des tâches. Effet après déconnexion.",
            Interface, false, true,
            new RegValue(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3, DWord, 0),
            new RegValue(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, DWord, 1),
            new RegValue(CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", RegistryValueKind.String, "1")),
        Registry("Désactiver les Widgets",
            "Retire le panneau Widgets (actualités, météo) qui tourne en permanence en arrière-plan.",
            Interface, true, false,
            new RegValue(LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0, DWord, null)),
        Registry("Désactiver la recherche Bing du menu Démarrer",
            "La recherche Windows ne cherche plus sur Internet : résultats locaux plus rapides.",
            Interface, true, true,
            new RegValue(CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, DWord, null),
            new RegValue(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, DWord, null)),

        // ---------------- Confidentialité ----------------
        new Tweak
        {
            Name = "Réduire la télémétrie Windows",
            Description = "Limite les données de diagnostic envoyées à Microsoft et arrête le service DiagTrack.",
            Category = Privacy, Recommended = true,
            Check = () => Equals(Reg.Get(LocalMachine, @"SYSTEM\CurrentControlSet\Services\DiagTrack", "Start"), 4),
            ApplyAction = () =>
            {
                Reg.Apply(new RegValue(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, DWord, null));
                ProcessHelper.Run("sc.exe", "stop DiagTrack");
                ProcessHelper.Run("sc.exe", "config DiagTrack start= disabled");
            },
            RevertAction = () =>
            {
                Reg.Restore(new RegValue(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, DWord, null));
                ProcessHelper.Run("sc.exe", "config DiagTrack start= auto");
                ProcessHelper.Run("sc.exe", "start DiagTrack");
            },
        },
        Registry("Désactiver l'identifiant publicitaire",
            "Les applications ne peuvent plus vous suivre pour afficher des publicités ciblées.",
            Privacy, true, false,
            new RegValue(CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, DWord, 1)),
        Registry("Bloquer les suggestions et applis installées sans demander",
            "Supprime les « suggestions » du menu Démarrer et l'installation automatique d'applis sponsorisées.",
            Privacy, true, false,
            new RegValue(CurrentUser, Cdm, "SystemPaneSuggestionsEnabled", 0, DWord, 1),
            new RegValue(CurrentUser, Cdm, "SilentInstalledAppsEnabled", 0, DWord, 1),
            new RegValue(CurrentUser, Cdm, "SoftLandingEnabled", 0, DWord, 1),
            new RegValue(CurrentUser, Cdm, "SubscribedContent-338388Enabled", 0, DWord, 1),
            new RegValue(CurrentUser, Cdm, "SubscribedContent-338389Enabled", 0, DWord, 1),
            new RegValue(CurrentUser, Cdm, "SubscribedContent-353694Enabled", 0, DWord, 1)),
        Registry("Désactiver l'historique d'activité",
            "Windows n'enregistre plus la chronologie des applications et fichiers ouverts.",
            Privacy, true, false,
            new RegValue(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, DWord, null),
            new RegValue(LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0, DWord, null)),

        // ---------------- Stockage & énergie ----------------
        new Tweak
        {
            Name = "Désactiver la mise en veille prolongée",
            Description = "Supprime le fichier hiberfil.sys (souvent plusieurs Go). Désactive aussi le « démarrage rapide ».",
            Category = Storage, Recommended = false,
            Check = () => Equals(Reg.Get(LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled"), 0),
            ApplyAction = () => ProcessHelper.Run("powercfg", "/hibernate off"),
            RevertAction = () => ProcessHelper.Run("powercfg", "/hibernate on"),
        },
        new Tweak
        {
            Name = "Activer TRIM pour les SSD",
            Description = "Garantit que Windows informe vos SSD des blocs libérés (performances durables).",
            Category = Storage, Recommended = true,
            Check = () => ProcessHelper.Run("fsutil", "behavior query DisableDeleteNotify").Output.Contains("NTFS DisableDeleteNotify = 0"),
            ApplyAction = () => ProcessHelper.Run("fsutil", "behavior set DisableDeleteNotify 0"),
            RevertAction = () => { /* Désactiver TRIM n'est jamais souhaitable : on ne fait rien */ },
        },
        Registry("Désactiver Stockage réservé",
            "Récupère ~7 Go réservés par Windows pour les mises à jour (effet à la prochaine mise à jour).",
            Storage, false, false,
            new RegValue(LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves", 0, DWord, 1)),
    };

    static Tweak Registry(string name, string desc, string category, bool recommended, bool restart, params RegValue[] values) => new()
    {
        Name = name,
        Description = desc,
        Category = category,
        Recommended = recommended,
        RequiresRestart = restart,
        Check = () => values.All(Reg.Matches),
        ApplyAction = () => { foreach (var v in values) Reg.Apply(v); },
        RevertAction = () => { foreach (var v in values) Reg.Restore(v); },
    };

    public static async Task<bool> SetAsync(Tweak t, bool apply)
    {
        t.IsBusy = true;
        try
        {
            await Task.Run(() => { if (apply) t.ApplyAction(); else t.RevertAction(); });
            Log.Write($"{(apply ? "Appliqué" : "Annulé")} : {t.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"Échec « {t.Name} » : {ex.Message}");
            return false;
        }
        finally
        {
            await Task.Run(t.Refresh);
            t.IsBusy = false;
        }
    }
}
