using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Pipeline;

/// <summary>
/// Enqueued by the manual trigger endpoint, the scheduler, or a webhook.
/// Wolverine delivers it to the handler that runs the pipeline.
/// </summary>
public sealed record RunJobCommand(Guid ExecutionId, string? Payload = null);