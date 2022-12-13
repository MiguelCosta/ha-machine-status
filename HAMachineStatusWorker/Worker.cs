namespace HAMachineStatusWorker;

using System.Text.Json;
using HAMachineStatusWorker.Configuration;
using HAMachineStatusWorker.Models;
using MQTTnet;
using MQTTnet.Client;

public class Worker : BackgroundService
{
    private IMqttClient _mqttClient;

    private readonly ILogger<Worker> _logger;

    private readonly Settings _settings;

    public Worker(ILogger<Worker> logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Entities that don't require constant
        // updates can be set outside the loop
        var states = new EntityStates
        {
            MachineName = Entities.GetMachineName(),
            OsVersion = Entities.GetOsVersion(),
            BootTime = Entities.GetBootTime(),
            CpuTemperature = "0",
            IpAddress = "0"
        };

        await ConnectClientAsync();

        await CreateSensors(states);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Update required entities here
            states.CpuTemperature = Entities.GetCpuTemperature(_settings.LmSensorsAdapterName);
            states.IpAddress = Entities.GetIpAddress(_settings.NetworkInterfaceName);

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            var statesMessages = new List<MqttApplicationMessage>
            {
                new MqttApplicationMessageBuilder()
                    .WithTopic($"machinestatus/{states.MachineName}/os_version")
                    .WithPayload(states.OsVersion)
                    .Build(),

                new MqttApplicationMessageBuilder()
                    .WithTopic($"machinestatus/{states.MachineName}/boot_time")
                    .WithPayload(states.BootTime)
                    .Build(),

                new MqttApplicationMessageBuilder()
                    .WithTopic($"machinestatus/{states.MachineName}/cpu_temperature")
                    .WithPayload(states.CpuTemperature)
                    .Build(),

                new MqttApplicationMessageBuilder()
                    .WithTopic($"machinestatus/{states.MachineName}/ip_address")
                    .WithPayload(states.IpAddress)
                    .Build()
            };

            await PublishToMqttAsync(statesMessages);

            await Task.Delay(_settings.PublishInterval, stoppingToken);
        }

        _mqttClient.Dispose();
    }

    private async Task ConnectClientAsync()
    {
        var mqttFactory = new MqttFactory();

        _mqttClient = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.MqttSettings.MQTTServerIP, _settings.MqttSettings.MQTTServerPort)
            .WithCredentials(_settings.MqttSettings.MQTTUsername, _settings.MqttSettings.MQTTPassword)
            .Build();

        try
        {
            using var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _mqttClient.ConnectAsync(mqttClientOptions, timeoutToken.Token);
            _logger.LogInformation("MQTT CONNECT");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "MQTT ERROR");
        }
    }

    private async Task CreateSensors(EntityStates states)
    {
        var sensorsMessages = new List<MqttApplicationMessage>
        {
            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{states.MachineName}/OSVersion/config")
                .WithPayload(VersionSensor(states))
                .Build(),

            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{states.MachineName}/BootTime/config")
                .WithPayload(BootTimeSensor(states))
                .Build(),

            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{states.MachineName}/CpuTemperature/config")
                .WithPayload(CpuTemperatureSensor(states))
                .Build(),

            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{states.MachineName}/IpAddress/config")
                .WithPayload(IpAddressSensor(states))
                .Build()
        };

        await PublishToMqttAsync(sensorsMessages);
    }

    private async Task PublishToMqttAsync(IEnumerable<MqttApplicationMessage> messages)
    {
        var messagesTasks =
            messages.Select(m => _mqttClient.PublishAsync(m, CancellationToken.None));

        await Task.WhenAll(messagesTasks);
    }

    private static string VersionSensor(EntityStates states)
    {
        var sensor = new
        {
            name = "OS Version",
            state_topic = $"machinestatus/{states.MachineName}/os_version",
            icon = "mdi:desktop-classic",
            retain = true,
            unique_id = $"{states.MachineName}-{nameof(VersionSensor)}",
            object_id = $"{states.MachineName}-{nameof(VersionSensor)}",
            expire_after = 120,
            device = CommonDevice(states)
        };

        return JsonSerializer.Serialize(sensor);
    }

    private static string BootTimeSensor(EntityStates states)
    {
        var sensor = new
        {
            name = "Boot Time",
            state_topic = $"machinestatus/{states.MachineName}/boot_time",
            icon = "mdi:timer-outline",
            retain = true,
            unique_id = $"{states.MachineName}-{nameof(BootTimeSensor)}",
            object_id = $"{states.MachineName}-{nameof(BootTimeSensor)}",
            device_class = "timestamp",
            expire_after = 120,
            device = CommonDevice(states)
        };

        return JsonSerializer.Serialize(sensor);
    }

    private static string CpuTemperatureSensor(EntityStates states)
    {
        var sensor = new
        {
            name = "CPU Temperature",
            state_topic = $"machinestatus/{states.MachineName}/cpu_temperature",
            icon = "mdi:thermometer",
            retain = true,
            unique_id = $"{states.MachineName}-{nameof(CpuTemperatureSensor)}",
            object_id = $"{states.MachineName}-{nameof(CpuTemperatureSensor)}",
            device_class = "temperature",
            expire_after = 120,
            unit_of_measurement = "ºC",
            device = CommonDevice(states)
        };

        return JsonSerializer.Serialize(sensor);
    }

    private static string IpAddressSensor(EntityStates states)
    {
        var sensor = new
        {
            name = "IP Address",
            state_topic = $"machinestatus/{states.MachineName}/ip_address",
            icon = "mdi:ip-network",
            retain = true,
            unique_id = $"{states.MachineName}-{nameof(IpAddressSensor)}",
            object_id = $"{states.MachineName}-{nameof(IpAddressSensor)}",
            expire_after = 120,
            device = CommonDevice(states)
        };

        return JsonSerializer.Serialize(sensor);
    }

    private static object CommonDevice(EntityStates states)
    {
        return new
        {
            manufacturer = "MPC, PMF & MM Lda",
            identifiers = new string[] {states.MachineIdentifier},
            model = $"Machine Status ({states.MachineName})",
            name = states.MachineName,
            sw_version = "1.0.0.1"
        };
    }
}
