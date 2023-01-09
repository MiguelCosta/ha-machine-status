namespace HAMachineStatusWorker;

using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HAMachineStatusWorker.Models;

public static class Entities
{
    public static string GetOsVersion()
    {
        return RuntimeInformation.RuntimeIdentifier;
    }

    public static string GetMachineName()
    {
        return Environment.MachineName.ToUpper();
    }

    public static string GetBootTime()
    {
        var timespan = TimeSpan.FromMilliseconds(Environment.TickCount);

        var bootTime = DateTime.UtcNow - timespan;

        var bootTimeFormats = bootTime.GetDateTimeFormats('O');

        return bootTimeFormats.FirstOrDefault()!;
    }

    public static string GetIpAddress(string interfaceName)
    {
        var networkInterface = NetworkInterface
            .GetAllNetworkInterfaces()
            .First(i => i.Name == interfaceName);

        var address = networkInterface.GetIPProperties().UnicastAddresses[0].Address;

        return address.ToString();
    }

    public static string GetCpuTemperature(string adapter)
    {
        var shellResult = ExecuteShell($"sensors {adapter} -A");

        const string pattern = @"\+(?<temp>[0-9]{2}\.[0-9])";

        var regex = new Regex(pattern);

        var match = regex.Matches(shellResult);

        var coreTempSum = match.Select(x => Convert.ToDecimal(x.Groups["temp"].Value)).Sum();

        var result = coreTempSum / match.Count;

        return result.ToString("0.#", CultureInfo.InvariantCulture);
    }

    public static MemoryRam GetMemoryRam()
    {
        var shellResult = ExecuteShell("free --bytes");

        const string pattern = @"Mem:\s+(?<total>[0-9]+)\s+(?<used>[0-9]+)";

        var regex = new Regex(pattern);

        var match = regex.Match(shellResult);

        var total = long.Parse(match.Groups["total"].Value);
        var used = long.Parse(match.Groups["used"].Value);

        var memory = new MemoryRam(total, used);

        return memory;
    }

    public static string GetCpuUse()
    {
        var shellResult = ExecuteShell("mpstat 1 1 -P ALL -o JSON");

        var json = JsonSerializer.Deserialize<Root>(shellResult);

        var dictionaryIdle = json.sysstat.hosts[0].statistics[0].cpuload.ToDictionary(x => x.cpu, x => x.idle);
        var dictionaryUse = dictionaryIdle.ToDictionary(x => x.Key, x => 100 - x.Value);

        return dictionaryUse["all"].ToString("0.##");
    }

    public static string GetCpuModel()
    {
        var shellResult = ExecuteShell($"lscpu");

        const string pattern = @"Model name:\s*(?<model>[A-Za-z\(\)]+[ A-Za-z\(\)0-9\@\.]*)";

        var regex = new Regex(pattern);

        var match = regex.Match(shellResult);

        var model = match.Groups["model"].Value;

        return model;
    }

    private static string ExecuteShell(string command)
    {
        using var proc = new Process();
        proc.StartInfo.FileName = "/bin/sh";
        proc.StartInfo.Arguments = "-c \" " + command + " \"";
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.Start();

        var consoleOutput = proc.StandardOutput.ReadToEnd();

        proc.WaitForExit();

        return consoleOutput;
    }
}
