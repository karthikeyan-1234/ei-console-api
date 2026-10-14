using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Application.Pipeline
{
    /// <summary>
    /// Evaluates a JSONata expression against a JSON input. v0.1 ships a
    /// pass-through implementation that only handles trivial expressions.
    /// Replace with a full evaluator when the JSONata package situation
    /// stabilises.
    /// </summary>
    public interface IJsonataEvaluator
    {
        Task<JsonElement?> EvaluateAsync(
            string expression,
            JsonElement? input,
            CancellationToken ct);
    }
}
