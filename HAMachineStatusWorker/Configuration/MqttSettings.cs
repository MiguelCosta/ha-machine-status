namespace HAMachineStatusWorker.Configuration;

public sealed class MqttSettings
{
    public string MQTTServerIP { get; set; }
    
    public int MQTTServerPort { get; set; }
    
    public string MQTTUsername { get; set; }
    
    public string MQTTPassword { get; set; }
}
