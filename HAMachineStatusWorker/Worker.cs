using MQTTnet;
using MQTTnet.Client;

namespace HAMachineStatusWorker;

public class Worker : BackgroundService
{
    private static string MQTTUsername = "usernamedev";
    private static string MQTTPassword = "passworddev";
    private static string MQTTServerIP = "192.168.31.127";
    private static int MQTTServerPort = 1883;

    private readonly ILogger<Worker> _logger;

    private IMqttClient _mqttClient;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectClientAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            var status = new
            {
                OsVersion = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                Uptime = GetUptime()
            };

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            _logger.LogInformation("OS Version: {time}", status.OsVersion);
            _logger.LogInformation("Uptime: {time}", status.Uptime);
            await Task.Delay(5000, stoppingToken);
        }

        _mqttClient.Dispose();
    }

    private double GetUptime()
    {
        // nao esta bem ainda
        var duration = TimeSpan.FromTicks(System.Environment.TickCount);
        return duration.TotalMinutes;
    }

    private async Task ConnectClientAsync()
    {
        var mqttFactory = new MqttFactory();

        _mqttClient = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(MQTTServerIP)
            .WithCredentials(MQTTUsername, MQTTPassword)
            .Build();

        try
        {
            using (var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                await _mqttClient.ConnectAsync(mqttClientOptions, timeoutToken.Token);
                _logger.LogInformation("MQTT CONNECT");
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "MQTT ERROR");
        }
    }
}
