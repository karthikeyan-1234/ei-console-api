using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Pipeline;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

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

            var mode = (cfg.QueryMode ?? "Reader").Trim();
            if (mode is not ("Reader" or "NonQuery" or "Scalar"))
                return TaskExecutionOutcome.Fail(
                    $"Unknown queryMode '{mode}'. Expected Reader, NonQuery, or Scalar.");

            var connectionString = await _sqlResolver.ResolveAsync(
                conn.AuthProfile, conn.BaseUrl, ct);

            if (string.IsNullOrWhiteSpace(connectionString))
                return TaskExecutionOutcome.Fail(
                    "Could not resolve SQL Server connection string.");

            var timeout = cfg.QueryTimeout ?? task.TimeoutSeconds ?? conn.TimeoutSeconds;

            try
            {
                await using var sqlConn = new SqlConnection(connectionString);
                await sqlConn.OpenAsync(ct);

                await using var cmd = new SqlCommand(cfg.Body!, sqlConn)
                {
                    CommandTimeout = timeout,
                };

                JsonElement output;

                switch (mode)
                {
                    case "NonQuery":
                        {
                            var affected = await cmd.ExecuteNonQueryAsync(ct);
                            output = JsonSerializer.SerializeToElement(
                                new { rowsAffected = affected });

                            _log.LogInformation(
                                "SqlQuery ran NonQuery against connection {ConnId} — {Affected} row(s) affected",
                                connId, affected);
                            break;
                        }
                    case "Scalar":
                        {
                            var value = await cmd.ExecuteScalarAsync(ct);
                            output = JsonSerializer.SerializeToElement(
                                new { value = value is DBNull ? null : value });

                            _log.LogInformation(
                                "SqlQuery ran Scalar against connection {ConnId}",
                                connId);
                            break;
                        }
                    default: // "Reader"
                        {
                            await using var reader = await cmd.ExecuteReaderAsync(ct);

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

                            output = JsonSerializer.SerializeToElement(rows);

                            _log.LogInformation(
                                "SqlQuery returned {Count} row(s) from connection {ConnId}",
                                rows.Count, connId);
                            break;
                        }
                }

                return TaskExecutionOutcome.Ok(output);
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
            public string? QueryMode { get; set; }
        }
    }
}