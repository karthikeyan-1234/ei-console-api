using System.Text.Json;

namespace EI_Console.Application.Pipeline
{
    /// <summary>
    /// What an executor returns after a task runs. Success carries the output
    /// payload that flows to the next task; failure carries an error message.
    /// HttpStatusCode is populated by the HTTP-based executors and threaded
    /// into scatter_gather_items by the runner.
    /// </summary>
    public sealed record TaskExecutionOutcome(
        bool Success,
        JsonElement? Output,
        string? ErrorMessage = null,
        int? HttpStatusCode = null)
    {
        public static TaskExecutionOutcome Ok(JsonElement? output, int? httpStatusCode = null) =>
            new(true, output, null, httpStatusCode);

        public static TaskExecutionOutcome Fail(string message, int? httpStatusCode = null) =>
            new(false, null, message, httpStatusCode);
    }
}