using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Application.Pipeline.Executors
{
    public sealed class TransformExecutor : ITaskExecutor
    {
        private readonly IJsonataEvaluator _jsonata;

        public TransformExecutor(IJsonataEvaluator jsonata) => _jsonata = jsonata;

        public TaskType Handles => TaskType.Transform;

        public async Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task, PipelineContext context, CancellationToken ct)
        {
            var cfg = JsonSerializer.Deserialize<TaskConfigLite>(task.ConfigurationJson ?? "{}");

            // Resolve the input source: either a stored key, a path, or the current value.
            JsonElement? input;
            if (!string.IsNullOrWhiteSpace(cfg?.InputSource))
                input = context.Resolve(cfg!.InputSource);
            else
                input = context.Current;

            if (input is null)
                return TaskExecutionOutcome.Fail("Transform has no input value.");

            // Evaluate the JSONata expression against the input.
            if (!string.IsNullOrWhiteSpace(cfg?.Jsonata))
            {
                var result = await _jsonata.EvaluateAsync(cfg!.Jsonata!, input, ct);
                return result is null
                    ? TaskExecutionOutcome.Fail("JSONata evaluation produced null.")
                    : TaskExecutionOutcome.Ok(result);
            }

            // No expression — pass the input through unchanged.
            return TaskExecutionOutcome.Ok(input);
        }

        private sealed class TaskConfigLite
        {
            public string? InputSource { get; set; }
            public string? Jsonata { get; set; }
        }
    }
}
