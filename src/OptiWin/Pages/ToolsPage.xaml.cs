using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OptiWin.Services;

namespace OptiWin.Pages;

public partial class ToolsPage : UserControl
{
    public ToolsPage()
    {
        InitializeComponent();

        Add("", "Vider le cache DNS", "Corrige les problèmes d'accès à certains sites et accélère la résolution des noms.", "Exécuter", async () =>
        {
            var r = await Task.Run(() => ProcessHelper.Run("ipconfig", "/flushdns"));
            Log.Write(r.ExitCode == 0 ? "Cache DNS vidé." : "Échec du vidage DNS.");
        });

        Add("", "Réinitialiser le réseau", "Réinitialise Winsock et la pile TCP/IP. À utiliser en cas de problème de connexion. Redémarrage requis.", "Réinitialiser", async () =>
        {
            if (!Confirm("Réinitialiser la pile réseau ? Un redémarrage sera nécessaire.")) return;
            await Task.Run(() =>
            {
                ProcessHelper.Run("netsh", "winsock reset");
                ProcessHelper.Run("netsh", "int ip reset");
                ProcessHelper.Run("ipconfig", "/flushdns");
            });
            Log.Write("Réseau réinitialisé. Redémarrez le PC.");
            MessageBox.Show("Réseau réinitialisé. Redémarrez le PC pour terminer.", "OptiWin");
        });

        Add("", "Vérifier les fichiers système", "Lance SFC pour détecter et réparer les fichiers Windows corrompus (10-20 min).", "Lancer", () =>
        {
            ProcessHelper.RunInConsole("sfc /scannow");
            return Task.CompletedTask;
        });

        Add("", "Réparer l'image Windows", "Lance DISM RestoreHealth pour réparer le magasin de composants Windows (nécessite Internet).", "Lancer", () =>
        {
            ProcessHelper.RunInConsole("DISM /Online /Cleanup-Image /RestoreHealth");
            return Task.CompletedTask;
        });

        Add("", "Optimiser les disques", "TRIM pour les SSD, défragmentation pour les disques durs. Sans risque.", "Optimiser", () =>
        {
            ProcessHelper.RunInConsole("defrag /C /O /U /V");
            return Task.CompletedTask;
        });

        Add("", "Plan « Performances ultimes »", "Ajoute et active le plan d'alimentation caché « Performances ultimes » de Windows (PC fixes).", "Activer", async () =>
        {
            var guid = await Task.Run(() =>
            {
                var r = ProcessHelper.Run("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
                var m = System.Text.RegularExpressions.Regex.Match(r.Output, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                if (m.Success) ProcessHelper.Run("powercfg", $"/setactive {m.Value}");
                return m.Success ? m.Value : null;
            });
            Log.Write(guid != null ? "Plan « Performances ultimes » activé." : "Plan « Performances ultimes » indisponible sur ce PC.");
        });

        Add("", "Point de restauration", "Crée un point de restauration manuel avant de faire des modifications.", "Créer", async () =>
        {
            var ok = await RestorePointService.CreateAsync("OptiWin - point manuel");
            MessageBox.Show(ok ? "Point de restauration créé." : "Échec : vérifiez que la Protection du système est activée.", "OptiWin");
        });

        Add("", "Restaurer le système", "Ouvre l'outil Windows pour revenir à un point de restauration précédent.", "Ouvrir", () =>
        {
            ProcessHelper.Open("rstrui.exe");
            return Task.CompletedTask;
        });

        Add("", "Nettoyage de disque Windows", "Ouvre l'outil de nettoyage intégré, avec les fichiers système.", "Ouvrir", () =>
        {
            ProcessHelper.Open("cleanmgr.exe");
            return Task.CompletedTask;
        });

        Add("", "Gestionnaire des tâches", "Surveillez les processus et l'utilisation des ressources en détail.", "Ouvrir", () =>
        {
            ProcessHelper.Open("taskmgr.exe");
            return Task.CompletedTask;
        });
    }

    static bool Confirm(string text) =>
        MessageBox.Show(text, "OptiWin", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    void Add(string icon, string title, string description, string action, Func<Task> run)
    {
        var button = new Button { Content = action, Style = (Style)FindResource("SecondaryButton"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0) };
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await run(); }
            catch (Exception ex) { Log.Write($"{title} : {ex.Message}"); }
            finally { button.IsEnabled = true; }
        };

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            Text = icon, FontFamily = (FontFamily)FindResource("IconFont"), FontSize = 18,
            Foreground = (Brush)FindResource("AccentBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
        });
        header.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center });

        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(button, Dock.Bottom);
        body.Children.Add(header);
        body.Children.Add(button);
        body.Children.Add(new TextBlock { Text = description, Style = (Style)FindResource("Muted"), Margin = new Thickness(0, 8, 0, 0) });

        var card = new Border { Style = (Style)FindResource("Card"), Margin = new Thickness(0, 0, 14, 14), Child = body };
        ToolsGrid.Children.Add(card);
    }
}
