using System.Windows;
using System.Windows.Controls;
using OptiWin.Services;

namespace OptiWin.Pages;

public partial class CleanupPage : UserControl, IRefreshable
{
    bool _scannedOnce;

    public CleanupPage()
    {
        InitializeComponent();
        List.ItemsSource = AppState.Cleanup;
    }

    public void OnShown()
    {
        if (!_scannedOnce) _ = ScanAsync();
    }

    void UpdateTotal()
    {
        var sel = AppState.Cleanup.Where(c => c.IsSelected && c.Size >= 0).Sum(c => c.Size);
        TotalText.Text = Format.Bytes(sel);
    }

    void SetBusy(bool busy)
    {
        ScanButton.IsEnabled = CleanButton.IsEnabled = !busy;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    async Task ScanAsync()
    {
        _scannedOnce = true;
        SetBusy(true);
        TotalText.Text = "Analyse…";
        Log.Write("Analyse des fichiers inutiles…");
        foreach (var c in AppState.Cleanup)
            c.Size = await Task.Run(() => { try { return c.Measure(); } catch { return 0L; } });
        UpdateTotal();
        Log.Write($"Analyse terminée : {TotalText.Text} récupérables.");
        SetBusy(false);
    }

    async void Scan_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    void Selection_Changed(object sender, RoutedEventArgs e) => UpdateTotal();

    async void Clean_Click(object sender, RoutedEventArgs e)
    {
        var selected = AppState.Cleanup.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0) return;

        if (selected.Any(c => c.Name == "Corbeille") &&
            MessageBox.Show("La corbeille va être vidée définitivement. Continuer ?", "OptiWin",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        SetBusy(true);
        long freed = 0;
        foreach (var c in selected)
        {
            Log.Write($"Nettoyage : {c.Name}…");
            freed += await Task.Run(() => { try { return c.Clean(); } catch { return 0L; } });
        }
        Log.Write($"Nettoyage terminé : {Format.Bytes(freed)} libérés.");
        SetBusy(false);
        await ScanAsync();
        MessageBox.Show($"{Format.Bytes(freed)} libérés.\n\nCertains fichiers en cours d'utilisation ont pu être ignorés : c'est normal.",
            "Nettoyage terminé", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
