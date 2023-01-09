using System.Text.Json.Serialization;

namespace HAMachineStatusWorker.Models
{
    public class CpuLoad
    {
        public string cpu { get; set; }
        public double usr { get; set; }
        public double nice { get; set; }
        public double sys { get; set; }
        public double iowait { get; set; }
        public double irq { get; set; }
        public double soft { get; set; }
        public double steal { get; set; }
        public double guest { get; set; }
        public double gnice { get; set; }
        public double idle { get; set; }
    }

    public class Host
    {
        public string nodename { get; set; }
        public string sysname { get; set; }
        public string release { get; set; }
        public string machine { get; set; }

        [JsonPropertyName("number-of-cpus")]
        public int numberofcpus { get; set; }
        public string date { get; set; }
        public List<Statistic> statistics { get; set; }
    }

    public class Root
    {
        public Sysstat sysstat { get; set; }
    }

    public class Statistic
    {
        public string timestamp { get; set; }

        [JsonPropertyName("cpu-load")]
        public List<CpuLoad> cpuload { get; set; }
    }

    public class Sysstat
    {
        public List<Host> hosts { get; set; }
    }
}
