using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Services
{
    public interface IExecutionServiceBridge
    {
        Task<Guid> TriggerAsync(
            int jobId,
            string triggerSource,
            string? idempotencyKey,
            string? payload,
            CancellationToken ct = default);
    }
}
