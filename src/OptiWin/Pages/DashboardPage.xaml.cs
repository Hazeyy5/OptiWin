using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OptiWin.Services;

namespace OptiWin.Pages;

public partial class DashboardPage : UserControl, IRefreshable
{
    readonly MainWindow _main;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    TextBlock? _uptime;

    public DashboardPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        CpuName.Text = SystemInfoService.CpuName();
        SystemInfoService.CpuUsage(); // première mesure de référence

        var mem = SystemInfoService.Memory();
        AddInfo("Système", SystemInfoService.OsName());
        AddInfo("Nom du PC", Environment.MachineName);
        AddInfo("Processeur", $"{SystemInfoService.CpuName()} — {Environment.ProcessorCount} threads");
        AddInfo("Carte graphique", SystemInfoService.GpuName());
        AddInfo("Mémoire", Format.Bytes((long)mem.Total));
        _uptime = AddInfo("Allumé depuis", SystemInfoService.UptimeText());

        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    public void OnShown()
    {
        DrivesList.ItemsSource = SystemInfoService.Drives();
        _ = RefreshScoreAsync();
        Tick();
    }

    async Task RefreshScoreAsync()
    {
        var tweaks = AppState.Tweaks;
        await Task.Run(() => tweaks.ForEach(t => t.Refresh()));
        var rec = tweaks.Where(t => t.Recommended).ToList();
        var applied = rec.Count(t => t.IsApplied);
        ScoreText.Text = applied == rec.Count
            ? $"✔ Toutes les optimisations recommandées sont actives ({applied}/{rec.Count})."
            : $"Optimisations recommandées actives : {applied}/{rec.Count}";
    }

    TextBlock AddInfo(string label, string value)
    {
        var row = InfoGrid.RowDefinitions.Count;
        InfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var l = new TextBlock { Text = label, Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 5, 0, 5) };
        var v = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5) };
        Grid.SetRow(l, row); Grid.SetRow(v, row); Grid.SetColumn(v, 1);
        InfoGrid.Children.Add(l); InfoGrid.Children.Add(v);
        return v;
    }

    void Tick()
    {
        var cpu = SystemInfoService.CpuUsage();
        CpuPercent.Text = $"{cpu:0} %";
        CpuBar.Value = cpu;

        var (total, avail, load) = SystemInfoService.Memory();
        RamPercent.Text = $"{load} %";
        RamBar.Value = load;
        RamText.Text = $"{Format.Bytes((long)(total - avail))} utilisés sur {Format.Bytes((long)total)}";

        if (_uptime != null) _uptime.Text = SystemInfoService.UptimeText();
    }

    async void OneClick_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "OptiWin va :\n\n" +
            "  1. Créer un point de restauration système\n" +
            "  2. Nettoyer les fichiers temporaires et caches\n" +
            "  3. Appliquer les optimisations recommandées\n\n" +
            "Tout est réversible (page Optimisations ou point de restauration). Continuer ?",
            "Optimisation en 1 clic", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        OneClickButton.IsEnabled = false;
        OneClickProgress.Visibility = Visibility.Visible;
        try
        {
            if (!await RestorePointService.CreateAsync())
            {
                var go = MessageBox.Show(
                    "Le point de restauration n'a pas pu être créé (la protection du système est peut-être désactivée).\n\nContinuer quand même ?",
                    "OptiWin", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (go != MessageBoxResult.Yes) return;
            }

            Log.Write("Nettoyage en cours…");
            long freed = 0;
            foreach (var c in AppState.Cleanup.Where(c => c.IsSelected))
            {
                Log.Write($"Nettoyage : {c.Name}…");
                freed += await Task.Run(() => { try { return c.Clean(); } catch { return 0L; } });
                c.Size = -1;
            }

            int count = 0;
            foreach (var t in AppState.Tweaks.Where(t => t.Recommended))
            {
                await Task.Run(t.Refresh);
                if (t.IsApplied) continue;
                if (await TweakService.SetAsync(t, true)) count++;
            }

            await RefreshScoreAsync();
            DrivesList.ItemsSource = SystemInfoService.Drives();
            Log.Write($"Terminé : {Format.Bytes(freed)} libérés, {count} optimisation(s) appliquée(s).");
            MessageBox.Show(
                $"Optimisation terminée !\n\n• Espace libéré : {Format.Bytes(freed)}\n• Optimisations appliquées : {count}\n\n" +
                "Un redémarrage est conseillé pour que tous les changements prennent effet.",
                "OptiWin", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            OneClickButton.IsEnabled = true;
            OneClickProgress.Visibility = Visibility.Collapsed;
        }
    }
}
