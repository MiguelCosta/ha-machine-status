namespace HAMachineStatusWorker;

using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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

        return result.ToString(CultureInfo.InvariantCulture);
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
