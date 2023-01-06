namespace HAMachineStatusWorker.Models;

public class EntityStates
{
    private readonly Lazy<string> machineId;

    public EntityStates()
    {
        this.machineId = new Lazy<string>(() => $"{MachineName}-{OsVersion}");
    }

    public string MachineName { get; set; }

    public string OsVersion { get; set; }

    public string MachineIdentifier => this.machineId.Value;

    public string BootTime { get; set; }

    public string IpAddress { get; set; }

    public string CpuTemperature { get; set; }

    public string CpuUse { get; set; }

    public string CpuModel { get; set; }

    public MemoryRam MemoryRam { get; set; }
}
