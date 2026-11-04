using System.Windows;
using System.Windows.Controls;
using OptiWin.Models;
using OptiWin.Services;

namespace OptiWin.Pages;

public partial class StartupPage : UserControl, IRefreshable
{
    public StartupPage() => InitializeComponent();

    public void OnShown() => Reload();

    void Reload()
    {
        var items = StartupService.List();
        List.ItemsSource = items;
        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Log.Write($"{items.Count} programme(s) au démarrage, dont {items.Count(i => i.IsEnabled)} actif(s).");
    }

    void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: StartupItem item } cb) return;
        try
        {
            StartupService.SetEnabled(item, cb.IsChecked == true);
        }
        catch (Exception ex)
        {
            Log.Write($"Impossible de modifier « {item.Name} » : {ex.Message}");
        }
        cb.IsChecked = item.IsEnabled;
    }
}
