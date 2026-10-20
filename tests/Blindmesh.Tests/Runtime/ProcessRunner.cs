using System.Diagnostics;

namespace Blindmesh.Tests.Runtime;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string All => StandardOutput + Environment.NewLine + StandardError;
}

/// <summary>Runs a native command and captures its real output.</summary>
public static class ProcessRunner
{
    /// <summary>
    /// The grace period for the output of a child that has already exited. A
    /// child that leaves a grandchild behind keeps the write end of the pipe
    /// open, so the end of the output may never arrive.
    /// </summary>
    private static readonly TimeSpan OutputGracePeriod = TimeSpan.FromSeconds(30);

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

        process.EnableRaisingEvents = true;

        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => exited.TrySetResult();

        process.Start();

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(10));

        await exited.Task.WaitAsync(cancellation.Token);

        // The exit of the child does not end the pipe when a grandchild keeps
        // it open, so the read of the output is bounded and the answer holds
        // the output that was read.
        await Task.WhenAny(Task.WhenAll(standardOutput, standardError), Task.Delay(OutputGracePeriod));

        return new ProcessResult(
            process.ExitCode,
            standardOutput.IsCompletedSuccessfully ? standardOutput.Result : string.Empty,
            standardError.IsCompletedSuccessfully ? standardError.Result : string.Empty);
    }
}
