using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Pipeline;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using System.Text.Json;
using System.Text.RegularExpressions;

namespace EI_Console.Infrastructure.Pipeline.Executors
{
    public sealed class SqlQueryExecutor : ITaskExecutor
    {
        private readonly IConnectionRepository _connections;
        private readonly ISqlConnectionStringResolver _sqlResolver;
        private readonly ILogger<SqlQueryExecutor> _log;

        /// <summary>
        /// Matches SQL Server named parameters: @identifier. Used to discover which
        /// placeholders in a query text should be bound from the pipeline context.
        /// Does not attempt to skip occurrences inside string literals — a limitation
        /// shared with most SQL tooling. Values with @ inside strings should be
        /// parameterized or escaped by the operator.
        /// </summary>
        private static readonly Regex ParameterPattern =
            new(@"@([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled);

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

                // Bind any @name placeholders in the query from the current pipeline item.
                // When iterating, the current item is one array element; when not, it's the
                // whole upstream payload. Names not present in the context are left unbound,
                // which surfaces as a clear "Must declare the scalar variable" error from
                // SQL Server rather than silently inserting null.
                BindParameters(cmd, context.Current);

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


        private static void BindParameters(SqlCommand cmd, JsonElement? current)
        {
            if (current is not { ValueKind: JsonValueKind.Object } obj) return;

            var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match m in ParameterPattern.Matches(cmd.CommandText))
            {
                var name = m.Groups[1].Value;
                if (!bound.Add(name)) continue;   // process each distinct name once

                foreach (var prop in obj.EnumerateObject())
                {
                    if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        cmd.Parameters.AddWithValue("@" + name, JsonToClr(prop.Value));
                        break;
                    }
                }
            }
        }

        private static object JsonToClr(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString() ?? (object)DBNull.Value,
                JsonValueKind.Number when el.TryGetInt64(out var l) => l,
                JsonValueKind.Number => el.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => DBNull.Value,
                JsonValueKind.Object or JsonValueKind.Array => el.GetRawText(),
                _ => DBNull.Value,
            };
        }
        private sealed class TaskConfigLite
        {
            public string? Body { get; set; }
            public int? QueryTimeout { get; set; }
            public string? QueryMode { get; set; }
        }
    }
}