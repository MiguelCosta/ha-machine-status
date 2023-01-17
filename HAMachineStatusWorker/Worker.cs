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

    private readonly EntityStates _states;

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
            CpuUse = "0",
            CpuModel = "",
            IpAddress = "0",
            MemoryRam = new MemoryRam(1, 1)
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Init();

        while (!stoppingToken.IsCancellationRequested)
        {
            // Update required entities here
            _states.CpuTemperature = Entities.GetCpuTemperature(_settings.LmSensorsAdapterName);
            _states.CpuUse = Entities.GetCpuUse();
            _states.CpuModel = Entities.GetCpuModel();
            _states.IpAddress = Entities.GetIpAddress(_settings.NetworkInterfaceName);
            _states.MemoryRam = Entities.GetMemoryRam();

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            var sensors = new Dictionary<string, string>
            {
                ["os_version"] = _states.OsVersion,
                ["boot_time"] = _states.BootTime,
                ["cpu_temperature"] = _states.CpuTemperature,
                ["cpu_use"] = _states.CpuUse,
                ["cpu_model"] = _states.CpuModel,
                ["ip_address"] = _states.IpAddress,
                ["memory_ram_totalbytes"] = _states.MemoryRam.TotalBytes.ToString(),
                ["memory_ram_usedbytes"] = _states.MemoryRam.UsedBytes.ToString(),
                ["memory_ram_totalgigabytes"] = _states.MemoryRam.TotalGigabytes.ToString("0.##"),
                ["memory_ram_usedgigabytes"] = _states.MemoryRam.UsedGigabytes.ToString("0.##"),
                ["memory_ram_percentageused"] = _states.MemoryRam.PercentageUsed.ToString("0.##")
            };

            var statesMessages = sensors.Select(x => BuildMessage(_states.MachineName, x.Key, x.Value)).ToList();

            await PublishToMqttAsync(statesMessages);

            await Task.Delay(_settings.PublishInterval * 1000, stoppingToken);
        }

        _mqttClient.Dispose();
    }

    private static MqttApplicationMessage BuildMessage(string machineName, string key, string value)
    {
        return new MqttApplicationMessageBuilder()
            .WithTopic($"machinestatus/{machineName}/{key}")
            .WithPayload(value)
            .Build();
    }

    private async Task Init()
    {
        try
        {
            await ConnectClientAsync();

            await CreateSensors();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Init error");
        }
    }

    private async Task ConnectClientAsync()
    {
        var mqttFactory = new MqttFactory();

        _mqttClient = mqttFactory.CreateMqttClient();

        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.MqttSettings.MQTTServerIP, _settings.MqttSettings.MQTTServerPort)
            .WithCredentials(_settings.MqttSettings.MQTTUsername, _settings.MqttSettings.MQTTPassword)
            .Build();

        _mqttClient.DisconnectedAsync += async args =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30));

            using var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _mqttClient.ConnectAsync(mqttClientOptions, timeoutToken.Token);
        };

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
        var sensors = new Dictionary<string, string>
        {
            ["OSVersion"] = VersionSensor(),
            ["BootTime"] = BootTimeSensor(),
            ["CpuTemperature"] = CpuTemperatureSensor(),
            ["CpuUse"] = CpuUseSensor(),
            ["CpuModel"] = CpuModelSensor(),
            ["IpAddress"] = IpAddressSensor(),
            ["MemoryRamTotalBytes"] = CreateSensor("MemoryRamTotalBytes", "Memory Ram Total Bytes", "memory_ram_totalbytes", "mdi:memory", "B"),
            ["MemoryRamUsedBytes"] = CreateSensor("MemoryRamUsedBytes", "Memory Ram Used Bytes", "memory_ram_usedbytes", "mdi:memory", "B"),
            ["MemoryRamTotalGigaBytes"] = CreateSensor("MemoryRamTotalGigaBytes", "Memory Ram Total GigaBytes", "memory_ram_totalgigabytes", "mdi:memory", "GB"),
            ["MemoryRamUsedGigaBytes"] = CreateSensor("MemoryRamUsedGigaBytes", "Memory Ram Used GigaBytes", "memory_ram_usedgigabytes", "mdi:memory", "GB"),
            ["MemoryRamPercentageUsed"] = CreateSensor("MemoryRamPercentageUsed", "Memory Ram Presentage Used", "memory_ram_percentageused", "mdi:memory", "%")
        };

        var sensorsMessages = sensors
            .Select(x => new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/MachineStatus{_states.MachineName}/{x.Key}/config")
                .WithPayload(x.Value)
                .Build())
            .ToList();

        await PublishToMqttAsync(sensorsMessages);
    }

    private async Task PublishToMqttAsync(IEnumerable<MqttApplicationMessage> messages)
    {
        try
        {
            foreach (var m in messages)
            {
                var result = await _mqttClient.PublishAsync(m, CancellationToken.None);
                if (!result.IsSuccess)
                {
                    _logger.LogError("PublishError: " + result.ReasonString);
                    await _mqttClient.ReconnectAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PublishError");
            await _mqttClient.ReconnectAsync();
        }
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

    private string CpuUseSensor()
    {
        var sensor = new
        {
            name = "CPU Use",
            state_topic = $"machinestatus/{_states.MachineName}/cpu_use",
            icon = "mdi:cpu-64-bit",
            retain = true,
            unique_id = $"{_states.MachineName}-{nameof(CpuUseSensor)}",
            object_id = $"{_states.MachineName}-{nameof(CpuUseSensor)}",
            expire_after = _settings.SensorExpireAfter,
            unit_of_measurement = "%",
            device = CommonDevice()
        };

        return JsonSerializer.Serialize(sensor);
    }

    private string CpuModelSensor()
    {
        var sensor = new
        {
            name = "CPU Model",
            state_topic = $"machinestatus/{_states.MachineName}/cpu_model",
            icon = "mdi:cpu-64-bit",
            retain = true,
            unique_id = $"{_states.MachineName}-{nameof(CpuModelSensor)}",
            object_id = $"{_states.MachineName}-{nameof(CpuModelSensor)}",
            expire_after = _settings.SensorExpireAfter,
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

    private string CreateSensor(string key, string name, string topicSuffix, string icon, string unit = "")
    {
        var sensor = new
        {
            name = name,
            state_topic = $"machinestatus/{_states.MachineName}/{topicSuffix}",
            icon = icon,
            retain = true,
            unique_id = $"{_states.MachineName}-{key}",
            object_id = $"{_states.MachineName}-{key}",
            expire_after = _settings.SensorExpireAfter,
            unit_of_measurement = unit,
            device = CommonDevice()
        };

        return JsonSerializer.Serialize(sensor);
    }

    private object CommonDevice()
    {
        return new
        {
            manufacturer = "MPC, PMF & MM Lda",
            identifiers = new string[] { _states.MachineIdentifier },
            model = $"Machine Status ({_states.MachineName})",
            name = _states.MachineName,
            sw_version = "1.0.0.1"
        };
    }
}
