using System.Text.Json;
using HAMachineStatusWorker.Models;

namespace HAMachineStatusWorker.Tests
{
    [TestClass]
    public class CpuUseTests
    {
        [TestMethod]
        public void FromJson()
        {
            var input = @"
{""sysstat"": {
        ""hosts"": [
                {
                        ""nodename"": ""mediaserver"",
                        ""sysname"": ""Linux"",
                        ""release"": ""5.15.0-57-generic"",
                        ""machine"": ""x86_64"",
                        ""number-of-cpus"": 4,
                        ""date"": ""01/06/2023"",
                        ""statistics"": [
                                {
                                        ""timestamp"": ""01:16:48 PM"",
                                        ""cpu-load"": [
                                                {""cpu"": ""all"", ""usr"": 20.02, ""nice"": 0.01, ""sys"": 2.70, ""iowait"": 0.16, ""irq"": 0.00, ""soft"": 1.74, ""steal"": 0.00, ""guest"": 0.00, ""gnice"": 0.00, ""idle"": 75.37},
                                                {""cpu"": ""0"", ""usr"": 21.11, ""nice"": 0.00, ""sys"": 2.42, ""iowait"": 0.14, ""irq"": 0.00, ""soft"": 0.21, ""steal"": 0.00, ""guest"": 0.00, ""gnice"": 0.00, ""idle"": 76.12},
                                                {""cpu"": ""1"", ""usr"": 17.53, ""nice"": 0.00, ""sys"": 3.21, ""iowait"": 0.17, ""irq"": 0.00, ""soft"": 6.51, ""steal"": 0.00, ""guest"": 0.00, ""gnice"": 0.00, ""idle"": 72.58},
                                                {""cpu"": ""2"", ""usr"": 20.46, ""nice"": 0.01, ""sys"": 2.67, ""iowait"": 0.18, ""irq"": 0.00, ""soft"": 0.24, ""steal"": 0.00, ""guest"": 0.00, ""gnice"": 0.00, ""idle"": 76.46},
                                                {""cpu"": ""3"", ""usr"": 20.88, ""nice"": 0.02, ""sys"": 2.53, ""iowait"": 0.14, ""irq"": 0.00, ""soft"": 0.21, ""steal"": 0.00, ""guest"": 0.00, ""gnice"": 0.00, ""idle"": 76.22}
                                        ]
                                }
                        ]
                }
        ]
}}
";
            var json = JsonSerializer.Deserialize<Root>(input);

            var dictionaryIdle = json.sysstat.hosts[0].statistics[0].cpuload.ToDictionary(x => x.cpu, x => x.idle);
            var dictionaryUse = dictionaryIdle.ToDictionary(x => x.Key, x => 100 - x.Value);

            var result = dictionaryUse["all"].ToString("0.##");
            Assert.AreEqual("24.63", result);
        }
    }
}