using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;

namespace HAMachineStatusWorker;

public class Worker : BackgroundService
{
    private static string MQTTUsername = "mqtt-remotesys";
    private static string MQTTPassword = "mqtt";
    private static string MQTTServerIP = "192.168.1.132";
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
                BootTime = GetBootTime()
            };

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            _logger.LogInformation("OS Version: {time}", status.OsVersion);
            _logger.LogInformation("BootTime: {time}", status.BootTime);

            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("machinestatus/os_version")
                .WithPayload(status.OsVersion)
                .Build();

            await this._mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

            applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic("machinestatus/boot_time")
                .WithPayload(status.BootTime)
                .Build();

            await this._mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

            await Task.Delay(5000, stoppingToken);
        }

        _mqttClient.Dispose();
    }
    
    private static string GetMachineName()
    {
        return System.Environment.MachineName;
    }

    private string GetBootTime()
    {
        var timespan = TimeSpan.FromMilliseconds(Environment.TickCount);

        var bootTime = DateTime.UtcNow - timespan;
        
        var bootTimeFormats = bootTime.GetDateTimeFormats('O');
        
        return bootTimeFormats.FirstOrDefault()!;
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
                .WithTopic("homeassistant/sensor/MachineStatus/BootTime/config")
                .WithPayload(GetBootTimeSensor())
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
            unique_id = "7b46561b-bc5e-44c8-abdd-eeebcefcf8f9",
            device = new
            {
                manufacturer = "MPC, PMF & MM Lda",
                identifiers = new string[] { "04c5d0de-0d89-44d2-b608-7cfe2e111790" },
                model = $"Machine Status ({GetMachineName()})",
                name = "Machine Status",
                sw_version = "1.0.0.0"
            }
        };

        return JsonSerializer.Serialize(sensor);
    }

    private static string GetBootTimeSensor()
    {
        var sensor = new
        {
            name = "Boot Time",
            state_topic = "machinestatus/boot_time",
            icon = "mdi:timer-outline",
            retain = true,
            unique_id = "9b6be5ec-b5fd-4fc8-883f-14521d7c34db",
            device_class = "timestamp",
            device = new
            {
                manufacturer = "MPC, PMF & MM Lda",
                identifiers = new string[] { "04c5d0de-0d89-44d2-b608-7cfe2e111790" },
                model = $"Machine Status ({GetMachineName()})",
                name = "Machine Status",
                sw_version = "1.0.0.0"
            }
        };

        return JsonSerializer.Serialize(sensor);
    }
}
