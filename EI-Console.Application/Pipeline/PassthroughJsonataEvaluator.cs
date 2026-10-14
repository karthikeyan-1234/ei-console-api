using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Application.Pipeline
{
    /// <summary>
    /// v0.1 JSONata evaluator. Handles a small subset:
    ///   "$"                → identity
    ///   "$.field.path"     → dotted path access
    /// Anything else logs a warning and returns the input unchanged.
    /// Replace with a full JSONata library in a follow-up.
    /// </summary>
    public sealed class PassthroughJsonataEvaluator : IJsonataEvaluator
    {
        private readonly ILogger<PassthroughJsonataEvaluator> _log;

        public PassthroughJsonataEvaluator(ILogger<PassthroughJsonataEvaluator> log) => _log = log;

        public Task<JsonElement?> EvaluateAsync(
            string expression, JsonElement? input, CancellationToken ct)
        {
            if (!input.HasValue) return Task.FromResult<JsonElement?>(null);

            var expr = expression.Trim();

            if (expr == "$") return Task.FromResult(input);

            if (expr.StartsWith("$."))
            {
                var path = expr[2..];
                var cursor = input.Value;

                foreach (var seg in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (cursor.ValueKind == JsonValueKind.Object &&
                        cursor.TryGetProperty(seg, out var next))
                    {
                        cursor = next;
                        continue;
                    }
                    return Task.FromResult<JsonElement?>(null);
                }
                return Task.FromResult<JsonElement?>(cursor);
            }

            _log.LogWarning(
                "JSONata expression '{Expr}' is not supported by the v0.1 evaluator. Input passed through unchanged.",
                expression);

            return Task.FromResult(input);
        }
    }
}
