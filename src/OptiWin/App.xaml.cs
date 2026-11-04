using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OptiWin.Services;

namespace OptiWin;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write("Erreur : " + args.Exception.Message);
            MessageBox.Show(args.Exception.Message, "OptiWin — erreur inattendue", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // Mode développeur : --snapshot <dossier> enregistre une capture de chaque page puis quitte.
        var i = Array.IndexOf(e.Args, "--snapshot");
        if (i >= 0 && i + 1 < e.Args.Length) _ = SnapshotAsync(window, e.Args[i + 1]);
    }

    static async Task SnapshotAsync(MainWindow window, string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var page in window.PageNames)
        {
            window.Navigate(page);
            await Task.Delay(2500);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var root = (FrameworkElement)window.Content;
            var dpi = VisualTreeHelper.GetDpi(root);
            var bmp = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY),
                dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bmp.Render(root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            await using var fs = File.Create(Path.Combine(dir, page + ".png"));
            enc.Save(fs);
        }
        window.Close();
    }
}
