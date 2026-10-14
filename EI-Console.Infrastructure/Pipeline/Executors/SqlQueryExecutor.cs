using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Pipeline;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Infrastructure.Pipeline.Executors
{
    public sealed class SqlQueryExecutor : ITaskExecutor
    {
        private readonly IConnectionRepository _connections;
        private readonly ISqlConnectionStringResolver _sqlResolver;
        private readonly ILogger<SqlQueryExecutor> _log;

        public SqlQueryExecutor(
            IConnectionRepository connections,
            ISqlConnectionStringResolver sqlResolver,
            ILogger<SqlQueryExecutor> log)
        {
            _connections = connections;
            _sqlResolver = sqlResolver;
            _log = log;
        }

        public TaskType Handles => TaskType.SqlQuery;

        public async Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task, PipelineContext context, CancellationToken ct)
        {
            if (task.ConnectionId is not int connId)
                return TaskExecutionOutcome.Fail("SqlQuery task has no connection.");

            var conn = await _connections.GetWithAuthProfileAsync(connId, ct);
            if (conn is null)
                return TaskExecutionOutcome.Fail($"Connection {connId} not found.");

            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");
            if (string.IsNullOrWhiteSpace(cfg?.Body))
                return TaskExecutionOutcome.Fail("SqlQuery has no query text.");

            // Build the full connection string from base + credentials.
            var connectionString = await _sqlResolver.ResolveAsync(
                conn.AuthProfile, conn.BaseUrl, ct);

            if (string.IsNullOrWhiteSpace(connectionString))
                return TaskExecutionOutcome.Fail(
                    "Could not resolve SQL Server connection string.");

            var timeout = cfg!.QueryTimeout ?? task.TimeoutSeconds ?? conn.TimeoutSeconds;

            try
            {
                await using var sqlConn = new SqlConnection(connectionString);
                await sqlConn.OpenAsync(ct);

                await using var cmd = new SqlCommand(cfg.Body!, sqlConn)
                {
                    CommandTimeout = timeout,
                };

                await using var reader = await cmd.ExecuteReaderAsync(ct);

                // Build a JSON array of row objects.
                var rows = new List<Dictionary<string, object?>>();

                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>(reader.FieldCount);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[name] = value;
                    }
                    rows.Add(row);
                }

                var json = JsonSerializer.Serialize(rows);
                var element = JsonDocument.Parse(json).RootElement.Clone();

                _log.LogInformation(
                    "SqlQuery returned {Count} row(s) from connection {ConnId}",
                    rows.Count, connId);

                return TaskExecutionOutcome.Ok(element);
            }
            catch (SqlException ex)
            {
                return TaskExecutionOutcome.Fail($"SQL error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return TaskExecutionOutcome.Fail($"Unexpected error: {ex.Message}");
            }
        }

        private sealed class TaskConfigLite
        {
            public string? Body { get; set; }
            public int? QueryTimeout { get; set; }
        }
    }
}
