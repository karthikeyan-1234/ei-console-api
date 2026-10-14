using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Pipeline;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Extensions.Logging;

using Renci.SshNet;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Infrastructure.Pipeline.Executors
{
    public sealed class SftpDownloadExecutor : ITaskExecutor
    {
        private readonly IConnectionRepository _connections;
        private readonly ISftpCredentialResolver _credentials;
        private readonly IFileContentParser _parser;
        private readonly ILogger<SftpDownloadExecutor> _log;

        public SftpDownloadExecutor(
            IConnectionRepository connections,
            ISftpCredentialResolver credentials,
            IFileContentParser parser,
            ILogger<SftpDownloadExecutor> log)
        {
            _connections = connections;
            _credentials = credentials;
            _parser = parser;
            _log = log;
        }

        public TaskType Handles => TaskType.FtpDownload;
        public Protocol? RequiredProtocol => Protocol.Sftp;


        public async Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task, PipelineContext context, CancellationToken ct)
        {
            if (task.ConnectionId is not int connId)
                return TaskExecutionOutcome.Fail("FtpDownload task has no connection.");

            var conn = await _connections.GetWithAuthProfileAsync(connId, ct);
            if (conn is null)
                return TaskExecutionOutcome.Fail($"Connection {connId} not found.");

            if (conn.Protocol != Protocol.Sftp)
                return TaskExecutionOutcome.Fail(
                    $"Connection '{conn.Name}' is not an SFTP connection (protocol = {conn.Protocol}).");

            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");
            if (string.IsNullOrWhiteSpace(cfg?.RemotePath))
                return TaskExecutionOutcome.Fail("FtpDownload has no remote path.");

            var credentials = await _credentials.ResolveAsync(conn.AuthProfile, ct);
            if (credentials is null)
                return TaskExecutionOutcome.Fail("Could not resolve SFTP credentials.");

            var host = conn.BaseUrl;
            var port = conn.Port ?? 22;
            var timeout = task.TimeoutSeconds ?? conn.TimeoutSeconds;

            try
            {
                // Build the private key from the PEM string.
                PrivateKeyFile privateKey;
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(credentials.PrivateKey)))
                {
                    privateKey = string.IsNullOrEmpty(credentials.Passphrase)
                        ? new PrivateKeyFile(ms)
                        : new PrivateKeyFile(ms, credentials.Passphrase);
                }

                var authMethod = new PrivateKeyAuthenticationMethod(credentials.Username, privateKey);
                var connectionInfo = new ConnectionInfo(host, port, credentials.Username, authMethod)
                {
                    Timeout = TimeSpan.FromSeconds(timeout),
                };

                using var client = new SftpClient(connectionInfo);
                client.OperationTimeout = TimeSpan.FromSeconds(timeout);

                await Task.Run(() => client.Connect(), ct);

                if (!client.Exists(cfg!.RemotePath!))
                {
                    client.Disconnect();
                    return TaskExecutionOutcome.Fail(
                        $"Remote file '{cfg.RemotePath}' does not exist.");
                }

                byte[] fileBytes;
                using (var ms = new MemoryStream())
                {
                    await Task.Run(() => client.DownloadFile(cfg.RemotePath!, ms), ct);
                    fileBytes = ms.ToArray();
                }

                var content = Encoding.UTF8.GetString(fileBytes);

                _log.LogInformation(
                    "SftpDownload fetched {Bytes} bytes from sftp://{Host}:{Port}{Path}",
                    content.Length, host, port, cfg.RemotePath);

                if (cfg.DeleteAfterDownload == true)
                {
                    try
                    {
                        await Task.Run(() => client.DeleteFile(cfg.RemotePath!), ct);
                        _log.LogInformation("Deleted remote file {Path}", cfg.RemotePath);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Could not delete remote file {Path}", cfg.RemotePath);
                    }
                }

                client.Disconnect();

                var parsed = _parser.Parse(content, cfg.FileFormat ?? "Auto", cfg.RemotePath!);
                return TaskExecutionOutcome.Ok(parsed);
            }
            catch (Exception ex)
            {
                return TaskExecutionOutcome.Fail($"SFTP error: {ex.Message}");
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
