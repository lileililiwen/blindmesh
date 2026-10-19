using System.Diagnostics;
using System.Text;

namespace Blindmesh.Tests.Runtime;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string All => StandardOutput + Environment.NewLine + StandardError;
}

/// <summary>Runs a native command and captures its real output.</summary>
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IDictionary<string, string?>? environment = null,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var variable in environment)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, data) => standardOutput.AppendLine(data.Data);
        process.ErrorDataReceived += (_, data) => standardError.AppendLine(data.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(10));
        await process.WaitForExitAsync(cancellation.Token);

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }
}
