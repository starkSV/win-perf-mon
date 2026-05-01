using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using WinPerfMon.App.ViewModels;
using WinPerfMon.Storage;

namespace WinPerfMon.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinPerfMon", "metrics.db");

        var services = new ServiceCollection();
        services.AddSingleton(new MetricStore(dbPath));
        services.AddSingleton<CpuViewModel>();
        services.AddSingleton<GpuViewModel>();
        services.AddSingleton<NetworkViewModel>();
        services.AddSingleton<StorageViewModel>();
        services.AddSingleton<ProcessesViewModel>();
        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Activate();
    }
}
