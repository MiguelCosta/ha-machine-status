using System.Text.Json;
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

        await this.CreateSensors();

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

            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("machinestatus/os_version")
                .WithPayload(status.OsVersion)
                .Build();

            await this._mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

            applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("machinestatus/uptime")
                .WithPayload(status.Uptime.ToString())
                .Build();

            await this._mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

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

    private async Task CreateSensors()
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("homeassistant/sensor/MachineStatus/OSVersion/config")
                .WithPayload(GetVersionSensor())
                .Build();

        await this._mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

        applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("homeassistant/sensor/MachineStatus/Uptime/config")
                .WithPayload(GetUptimeSensor())
                .Build();

        await this._mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
    }

    private static string GetVersionSensor()
    {
        var sensor = new
        {
            name = "OS Version",
            state_topic = "machinestatus/os_version",
            icon = "mdi:desktop-classic",
            retain = true,
            unique_id = "66de0f5a-f0f3-4f39-9de6-a122ed0421de",
            device = new
            {
                manufacturer = "MPC, PMF & MM Lda",
                identifiers = new string[] { "ade0b147-b072-4ed0-ad04-8425eade79d8" },
                model = "Machine Status",
                name = "Machine Status",
                sw_version = "1.0.0.0"
            }
        };

        return JsonSerializer.Serialize(sensor);
    }

    private static string GetUptimeSensor()
    {
        var sensor = new
        {
            name = "Uptime",
            state_topic = "machinestatus/uptime",
            icon = "mdi:timer-outline",
            retain = true,
            unique_id = "f9e40c5a-41c3-451a-92c7-e8a49aad701e",
            device = new
            {
                manufacturer = "MPC, PMF & MM Lda",
                identifiers = new string[] { "ade0b147-b072-4ed0-ad04-8425eade79d8" },
                model = "Machine Status",
                name = "Machine Status",
                sw_version = "1.0.0.0"
            }
        };

        return JsonSerializer.Serialize(sensor);
    }
}
