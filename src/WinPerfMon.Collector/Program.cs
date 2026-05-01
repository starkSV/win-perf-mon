using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WinPerfMon.Collector;
using WinPerfMon.Storage;

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "WinPerfMon", "metrics.db");

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "WinPerfMon.Collector")
    .ConfigureServices(services =>
    {
        services.AddSingleton(new MetricStore(dbPath));
        services.AddHostedService<CollectorWorker>();
    })
    .Build();

await host.RunAsync();
