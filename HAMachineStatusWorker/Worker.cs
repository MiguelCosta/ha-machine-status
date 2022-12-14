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

    private EntityStates _states;

    public Worker(ILogger<Worker> logger, Settings settings)
    {
        _logger = logger;

        _settings = settings;

        _states = new EntityStates
        {
            MachineName = Entities.GetMachineName(),
            OsVersion = Entities.GetOsVersion(),
            BootTime = Entities.GetBootTime(),
            CpuTemperature = "0",
            IpAddress = "0"
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectClientAsync();

        await CreateSensors();

        while (!stoppingToken.IsCancellationRequested)
        {
            // Update required entities here
            _states.CpuTemperature = Entities.GetCpuTemperature(_settings.LmSensorsAdapterName);
            _states.IpAddress = Entities.GetIpAddress(_settings.NetworkInterfaceName);

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            var statesMessages = new List<MqttApplicationMessage>
            {
                new MqttApplicationMessageBuilder()
                    .WithTopic($"machinestatus/{_states.MachineName}/os_version")
                    .WithPayload(_states.OsVersion)
                    .Build(),

                new MqttApplicationMessageBuilder()
                    .WithTopic($"machinestatus/{_states.MachineName}/boot_time")
                    .WithPayload(_states.BootTime)
                    .Build(),

                new MqttApplicationMessageBuilder()
                    .WithTopic($"machinestatus/{_states.MachineName}/cpu_temperature")
                    .WithPayload(_states.CpuTemperature)
                    .Build(),

                new MqttApplicationMessageBuilder()
                    .WithTopic($"machinestatus/{_states.MachineName}/ip_address")
                    .WithPayload(_states.IpAddress)
                    .Build()
            };

            await PublishToMqttAsync(statesMessages);

            await Task.Delay(_settings.PublishInterval * 1000, stoppingToken);
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

    private async Task CreateSensors()
    {
        var sensorsMessages = new List<MqttApplicationMessage>
        {
            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{_states.MachineName}/OSVersion/config")
                .WithPayload(VersionSensor())
                .Build(),

            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{_states.MachineName}/BootTime/config")
                .WithPayload(BootTimeSensor())
                .Build(),

            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{_states.MachineName}/CpuTemperature/config")
                .WithPayload(CpuTemperatureSensor())
                .Build(),

            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{_states.MachineName}/IpAddress/config")
                .WithPayload(IpAddressSensor())
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

    private string VersionSensor()
    {
        var sensor = new
        {
            name = "OS Version",
            state_topic = $"machinestatus/{_states.MachineName}/os_version",
            icon = "mdi:desktop-classic",
            retain = true,
            unique_id = $"{_states.MachineName}-{nameof(VersionSensor)}",
            object_id = $"{_states.MachineName}-{nameof(VersionSensor)}",
            expire_after = _settings.SensorExpireAfter,
            device = CommonDevice()
        };

        return JsonSerializer.Serialize(sensor);
    }

    private string BootTimeSensor()
    {
        var sensor = new
        {
            name = "Boot Time",
            state_topic = $"machinestatus/{_states.MachineName}/boot_time",
            icon = "mdi:timer-outline",
            retain = true,
            unique_id = $"{_states.MachineName}-{nameof(BootTimeSensor)}",
            object_id = $"{_states.MachineName}-{nameof(BootTimeSensor)}",
            device_class = "timestamp",
            expire_after = _settings.SensorExpireAfter,
            device = CommonDevice()
        };

        return JsonSerializer.Serialize(sensor);
    }

    private string CpuTemperatureSensor()
    {
        var sensor = new
        {
            name = "CPU Temperature",
            state_topic = $"machinestatus/{_states.MachineName}/cpu_temperature",
            icon = "mdi:thermometer",
            retain = true,
            unique_id = $"{_states.MachineName}-{nameof(CpuTemperatureSensor)}",
            object_id = $"{_states.MachineName}-{nameof(CpuTemperatureSensor)}",
            device_class = "temperature",
            expire_after = _settings.SensorExpireAfter,
            unit_of_measurement = "ºC",
            device = CommonDevice()
        };

        return JsonSerializer.Serialize(sensor);
    }

    private string IpAddressSensor()
    {
        var sensor = new
        {
            name = "IP Address",
            state_topic = $"machinestatus/{_states.MachineName}/ip_address",
            icon = "mdi:ip-network",
            retain = true,
            unique_id = $"{_states.MachineName}-{nameof(IpAddressSensor)}",
            object_id = $"{_states.MachineName}-{nameof(IpAddressSensor)}",
            expire_after = _settings.SensorExpireAfter,
            device = CommonDevice()
        };

        return JsonSerializer.Serialize(sensor);
    }

    private object CommonDevice()
    {
        return new
        {
            manufacturer = "MPC, PMF & MM Lda",
            identifiers = new string[] {_states.MachineIdentifier},
            model = $"Machine Status ({_states.MachineName})",
            name = _states.MachineName,
            sw_version = "1.0.0.1"
        };
    }
}
