using System.Windows;
using System.Windows.Controls;
using OptiWin.Pages;
using OptiWin.Services;

namespace OptiWin;

public partial class MainWindow : Window
{
    readonly Dictionary<string, Func<UserControl>> _factories;
    readonly Dictionary<string, UserControl> _pages = new();
    string? _updateUrl;

    public IEnumerable<string> PageNames => _factories.Keys;

    public MainWindow()
    {
        InitializeComponent();
        _factories = new()
        {
            ["Dashboard"] = () => new DashboardPage(this),
            ["Cleanup"] = () => new CleanupPage(),
            ["Tweaks"] = () => new TweaksPage(),
            ["Startup"] = () => new StartupPage(),
            ["Tools"] = () => new ToolsPage(),
        };

        VersionText.Text = UpdateService.CurrentVersionText;
        Log.MessageLogged += msg => Dispatcher.BeginInvoke(() => StatusText.Text = msg);
        NavDashboard.IsChecked = true;
        Loaded += async (_, _) => await CheckUpdateAsync();
    }

    public void Navigate(string page)
    {
        var nav = (RadioButton)FindName("Nav" + page);
        if (nav.IsChecked == true) Show(page); else nav.IsChecked = true;
    }

    void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && _factories != null) Show(rb.Name["Nav".Length..]);
    }

    void Show(string page)
    {
        if (!_pages.TryGetValue(page, out var control))
            _pages[page] = control = _factories[page]();
        Host.Content = control;
        if (control is IRefreshable r) r.OnShown();
    }

    async Task CheckUpdateAsync()
    {
        var update = await UpdateService.CheckAsync();
        if (update is { } u)
        {
            _updateUrl = u.Url;
            UpdateText.Text = $"La version {u.Version} est disponible.";
            UpdateBanner.Visibility = Visibility.Visible;
        }
    }

    void Update_Click(object sender, RoutedEventArgs e) => ProcessHelper.Open(_updateUrl ?? UpdateService.RepoUrl + "/releases/latest");
    void Github_Click(object sender, RoutedEventArgs e) => ProcessHelper.Open(UpdateService.RepoUrl);
}

public interface IRefreshable
{
    void OnShown();
}
