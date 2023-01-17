namespace HAMachineStatusWorker.Models
{
    public class MemoryRam
    {
        public MemoryRam(long totalBytes, long usedBytes)
        {
            TotalBytes = totalBytes;
            UsedBytes = usedBytes;
        }

        public long TotalBytes { get; set; }

        public double TotalGigabytes { get { return (double)TotalBytes / 1000000000; } }

        public long UsedBytes { get; set; }

        public double UsedGigabytes { get { return (double)UsedBytes / 1000000000; } }

        public double PercentageUsed { get { return (double)(UsedBytes * 100) / TotalBytes; } }
    }
}
