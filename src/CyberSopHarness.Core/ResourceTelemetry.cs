using System.Diagnostics;

namespace CyberSopHarness.Core;

public sealed record ResourceSample(
    DateTimeOffset Timestamp,
    long WorkingSetBytes,
    long VramUsedBytes,
    long VramTotalBytes,
    double GpuUtilizationPercent);

public static class ResourceTelemetry
{
    public static async Task<ResourceSample?> SampleAsync(int? processId, CancellationToken cancellationToken)
    {
        long workingSet = 0;
        if (processId is int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.HasExited) workingSet = process.WorkingSet64;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
        var vram = await QueryVramAsync(cancellationToken);
        return new ResourceSample(DateTimeOffset.UtcNow, workingSet, vram?.VramUsedBytes ?? 0, vram?.VramTotalBytes ?? 0, vram?.GpuUtilizationPercent ?? 0);
    }

    private static async Task<(long VramUsedBytes, long VramTotalBytes, double GpuUtilizationPercent)?> QueryVramAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("--query-gpu=memory.used,memory.total,utilization.gpu");
            startInfo.ArgumentList.Add("--format=csv,noheader,nounits");
            using var process = Process.Start(startInfo);
            if (process is null) return null;
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var parts = output.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3) return null;
            if (!long.TryParse(parts[0], out var usedMiB) || !long.TryParse(parts[1], out var totalMiB)) return null;
            _ = double.TryParse(parts[2], out var utilization);
            return (usedMiB * 1024L * 1024L, totalMiB * 1024L * 1024L, utilization);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return null;
        }
    }
}