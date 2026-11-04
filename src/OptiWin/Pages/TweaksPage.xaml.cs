using System.Windows;
using System.Windows.Controls;
using OptiWin.Models;
using OptiWin.Services;

namespace OptiWin.Pages;

public partial class TweaksPage : UserControl, IRefreshable
{
    public TweaksPage()
    {
        InitializeComponent();
        var template = (DataTemplate)Resources["TweakTemplate"];
        foreach (var group in AppState.Tweaks.GroupBy(t => t.Category))
        {
            Sections.Children.Add(new TextBlock { Text = group.Key.ToUpperInvariant(), Style = (Style)FindResource("SectionTitle") });
            Sections.Children.Add(new ItemsControl { ItemsSource = group.ToList(), ItemTemplate = template });
        }
    }

    public async void OnShown() => await Task.Run(() => AppState.Tweaks.ForEach(t => t.Refresh()));

    async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: Tweak tweak } cb) return;
        var apply = cb.IsChecked == true;
        await TweakService.SetAsync(tweak, apply);
        cb.IsChecked = tweak.IsApplied; // resynchronise si l'action a échoué
    }

    async void Recommended_Click(object sender, RoutedEventArgs e)
    {
        RecommendedButton.IsEnabled = false;
        var count = 0;
        foreach (var t in AppState.Tweaks.Where(t => t.Recommended && !t.IsApplied))
            if (await TweakService.SetAsync(t, true)) count++;
        RecommendedButton.IsEnabled = true;
        Log.Write($"{count} optimisation(s) recommandée(s) appliquée(s).");
    }

    async void Restore_Click(object sender, RoutedEventArgs e)
    {
        RestoreButton.IsEnabled = false;
        var ok = await RestorePointService.CreateAsync("OptiWin - point manuel");
        RestoreButton.IsEnabled = true;
        MessageBox.Show(ok ? "Point de restauration créé avec succès."
                           : "Impossible de créer le point de restauration. Vérifiez que la Protection du système est activée.",
            "OptiWin", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
