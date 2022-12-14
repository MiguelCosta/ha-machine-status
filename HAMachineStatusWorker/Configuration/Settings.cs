namespace HAMachineStatusWorker.Configuration;

public sealed class Settings
{
    public MqttSettings MqttSettings { get; set; }

    public int PublishInterval { get; set; }

    public int SensorExpireAfter { get; set; }

    public string LmSensorsAdapterName { get; set; }

    public string NetworkInterfaceName { get; set; }
}
