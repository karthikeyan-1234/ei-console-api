using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Pipeline
{
    public sealed class RunJobCommandHandler
    {
        private readonly PipelineRunner _runner;
        private readonly ILogger<RunJobCommandHandler> _log;

        public RunJobCommandHandler(PipelineRunner runner, ILogger<RunJobCommandHandler> log)
        {
            _runner = runner;
            _log = log;
        }

        public async Task Handle(RunJobCommand command, CancellationToken ct)
        {
            _log.LogInformation("RunJobCommand received for execution {ExecutionId}", command.ExecutionId);
            await _runner.RunAsync(command.ExecutionId, command.Payload, ct);
        }
    }
}
