using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Blindmesh.Application.Configuration;
using Blindmesh.Application.Operations;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Blindmesh.Infrastructure.Operations;

/// <summary>
/// The isolated environment of one restore rehearsal. The environment dumps the
/// live database with the real client of the host, creates the isolated
/// database and restores the dump into it.
///
/// The password of the database never reaches the arguments of a process: it is
/// handed over in the environment of the process, so it never shows up in the
/// list of the running processes and never reaches a log.
/// </summary>
public sealed class PostgresRestoreEnvironment : IRestoreRehearsalEnvironment
{
    private readonly string dumpBinary;
    private readonly string clientBinary;
    private readonly string restoreBinary;
    private readonly string host;
    private readonly int port;
    private readonly string username;
    private readonly string password;
    private readonly string sourceDatabase;
    private readonly string workingDirectory;

    public PostgresRestoreEnvironment(IOptions<BlindmeshRuntimeOptions> options)
    {
        var connectionString = options.Value.Database.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Blindmesh:Database:ConnectionString is required. The restore rehearsal cannot run without it.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        dumpBinary = options.Value.Operations.PgDumpPath;
        clientBinary = options.Value.Operations.PsqlPath;
        restoreBinary = options.Value.Operations.PgRestorePath;
        host = builder.Host ?? string.Empty;
        port = builder.Port;
        username = builder.Username ?? string.Empty;
        password = builder.Password ?? string.Empty;
        sourceDatabase = builder.Database ?? string.Empty;
        workingDirectory = Path.Combine(Path.GetTempPath(), "blindmesh-restore");
    }

    /// <summary>
    /// Dumps the live database, creates the isolated database and restores the
    /// dump into it. The name of the isolated database is returned.
    /// </summary>
    public async Task<string> PrepareAsync(CancellationToken cancellationToken)
    {
        var databaseName = BuildDatabaseName();

        Directory.CreateDirectory(workingDirectory);

        var dumpPath = Path.Combine(workingDirectory, $"{databaseName}.dump");

        try
        {
            await RunAsync(
                dumpBinary,
                [
                    "--format=custom",
                    "--no-owner",
                    "--no-privileges",
                    $"--file={dumpPath}",
                    $"--host={host}",
                    $"--port={port.ToString(CultureInfo.InvariantCulture)}",
                    $"--username={username}",
                    $"--dbname={sourceDatabase}",
                ],
                cancellationToken);

            await RunAsync(
                clientBinary,
                [
                    $"--host={host}",
                    $"--port={port.ToString(CultureInfo.InvariantCulture)}",
                    $"--username={username}",
                    "--dbname=postgres",
                    "--no-psqlrc",
                    "--quiet",
                    "-c",
                    $"CREATE DATABASE \"{databaseName}\";",
                ],
                cancellationToken);

            await RunAsync(
                restoreBinary,
                [
                    "--no-owner",
                    "--no-privileges",
                    "--exit-on-error",
                    $"--host={host}",
                    $"--port={port.ToString(CultureInfo.InvariantCulture)}",
                    $"--username={username}",
                    $"--dbname={databaseName}",
                    dumpPath,
                ],
                cancellationToken);

            return databaseName;
        }
        catch
        {
            // The isolated database is dropped again when the restore fails, so
            // a failed rehearsal never leaves a database behind.
            await DropQuietlyAsync(databaseName);

            throw;
        }
    }

    /// <summary>A reader for the isolated database.</summary>
    public IRehearsalDatabase OpenDatabase(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Username = username,
            Password = password,
            Database = databaseName,
        };

        return new RehearsalDatabase(databaseName, builder.ConnectionString);
    }

    /// <summary>Drops the isolated database and the dump of the rehearsal.</summary>
    public async Task DropAsync(string databaseName, CancellationToken cancellationToken)
    {
        await RunAsync(
            clientBinary,
            [
                $"--host={host}",
                $"--port={port.ToString(CultureInfo.InvariantCulture)}",
                $"--username={username}",
                "--dbname=postgres",
                "--no-psqlrc",
                "--quiet",
                "-c",
                $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);",
            ],
            cancellationToken);

        DeleteDumpQuietly(databaseName);
    }

    /// <summary>
    /// Removes the dump of the rehearsal. The dump may belong to another user
    /// of the host, so a file that may not be removed is left behind.
    /// </summary>
    private void DeleteDumpQuietly(string databaseName)
    {
        try
        {
            var dumpPath = Path.Combine(workingDirectory, $"{databaseName}.dump");

            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
        catch (Exception)
        {
            // The dump of a finished rehearsal is never used again.
        }
    }

    private async Task DropQuietlyAsync(string databaseName)
    {
        try
        {
            await DropAsync(databaseName, CancellationToken.None);
        }
        catch (Exception)
        {
            // The drop of a failed rehearsal may not hide the reason of the
            // failure. The operator finds the leftover database in the catalog.
        }
    }

    /// <summary>
    /// Runs one client of the host. The password of the database is handed over
    /// in the environment of the process, never in its arguments.
    /// </summary>
    private async Task RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrEmpty(password))
        {
            startInfo.Environment["PGPASSWORD"] = password;
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The client {fileName} of the host could not be started. The restore rehearsal needs the client of PostgreSQL.",
                exception);
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        await standardOutput;
        await standardError;

        if (process.ExitCode != 0)
        {
            // The output of the client may hold a path of the host. It stays in
            // the log of the server and it never reaches the answer of the API.
            throw new InvalidOperationException(
                $"The client {fileName} of the host failed with the code {process.ExitCode}.");
        }
    }

    /// <summary>
    /// The name of the isolated database of one rehearsal. It holds no part of
    /// the identity of a request.
    /// </summary>
    private static string BuildDatabaseName()
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();

        return $"bm_rehearsal_{suffix}";
    }
}
