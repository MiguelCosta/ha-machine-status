using HAMachineStatusWorker;
using HAMachineStatusWorker.Configuration;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var settings = hostContext.Configuration.GetSection("Settings").Get<Settings>();

        services.AddSingleton(settings);
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
