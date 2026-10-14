using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Pipeline;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using FluentFTP;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Infrastructure.Pipeline.Executors
{
    public sealed class FtpDownloadExecutor : ITaskExecutor
    {
        private readonly IConnectionRepository _connections;
        private readonly IFtpCredentialResolver _credentials;
        private readonly IFileContentParser _parser;
        private readonly ILogger<FtpDownloadExecutor> _log;

        public FtpDownloadExecutor(
            IConnectionRepository connections,
            IFtpCredentialResolver credentials,
            IFileContentParser parser,
            ILogger<FtpDownloadExecutor> log)
        {
            _connections = connections;
            _credentials = credentials;
            _parser = parser;
            _log = log;
        }

        public TaskType Handles => TaskType.FtpDownload;
        public Protocol? RequiredProtocol => Protocol.Ftp;


        public async Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task, PipelineContext context, CancellationToken ct)
        {
            if (task.ConnectionId is not int connId)
                return TaskExecutionOutcome.Fail("FtpDownload task has no connection.");

            var conn = await _connections.GetWithAuthProfileAsync(connId, ct);
            if (conn is null)
                return TaskExecutionOutcome.Fail($"Connection {connId} not found.");

            if (conn.Protocol != Protocol.Ftp)
                return TaskExecutionOutcome.Fail(
                    $"Connection '{conn.Name}' is not an FTP connection (protocol = {conn.Protocol}).");

            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");
            if (string.IsNullOrWhiteSpace(cfg?.RemotePath))
                return TaskExecutionOutcome.Fail("FtpDownload has no remote path.");

            var credentials = await _credentials.ResolveAsync(conn.AuthProfile, ct);
            if (credentials is null)
                return TaskExecutionOutcome.Fail("Could not resolve FTP credentials.");

            var host = conn.BaseUrl;
            var port = conn.Port ?? 21;
            var timeout = task.TimeoutSeconds ?? conn.TimeoutSeconds;

            try
            {
                using var client = new AsyncFtpClient(
                    host, credentials.Username, credentials.Password, port);
                client.Config.ConnectTimeout = timeout * 1000;
                client.Config.ReadTimeout = timeout * 1000;
                client.Config.DataConnectionConnectTimeout = timeout * 1000;

                await client.Connect(ct);

                if (!await client.FileExists(cfg!.RemotePath!, ct))
                {
                    await client.Disconnect(ct);
                    return TaskExecutionOutcome.Fail(
                        $"Remote file '{cfg.RemotePath}' does not exist.");
                }

                // Download to memory.
                await using var stream = new MemoryStream();
                var ok = await client.DownloadStream(stream, cfg.RemotePath!, token: ct);

                if (!ok)
                {
                    await client.Disconnect(ct);
                    return TaskExecutionOutcome.Fail("FTP download failed.");
                }

                var content = Encoding.UTF8.GetString(stream.ToArray());

                _log.LogInformation(
                    "FtpDownload fetched {Bytes} bytes from ftp://{Host}:{Port}{Path}",
                    content.Length, host, port, cfg.RemotePath);

                // Optional delete-after-download.
                if (cfg.DeleteAfterDownload == true)
                {
                    try
                    {
                        await client.DeleteFile(cfg.RemotePath!, ct);
                        _log.LogInformation("Deleted remote file {Path}", cfg.RemotePath);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Could not delete remote file {Path}", cfg.RemotePath);
                    }
                }

                await client.Disconnect(ct);

                // Parse according to the format hint.
                var parsed = _parser.Parse(content, cfg.FileFormat ?? "Auto", cfg.RemotePath!);
                return TaskExecutionOutcome.Ok(parsed);
            }
            catch (Exception ex)
            {
                return TaskExecutionOutcome.Fail($"FTP error: {ex.Message}");
            }
        }

        private sealed class TaskConfigLite
        {
            public string? RemotePath { get; set; }
            public string? FileFormat { get; set; }
            public bool? DeleteAfterDownload { get; set; }
        }
    }
}
