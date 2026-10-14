using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Application.Pipeline
{
    /// <summary>
    /// What an executor returns after a task runs. Success carries the output
    /// payload that flows to the next task; failure carries an error message
    /// and the output summary for the task log.
    /// </summary>
    public sealed record TaskExecutionOutcome(
        bool Success,
        JsonElement? Output,
        string? ErrorMessage = null)
    {
        public static TaskExecutionOutcome Ok(JsonElement? output) =>
            new(true, output);

        public static TaskExecutionOutcome Fail(string message) =>
            new(false, null, message);
    }
}
