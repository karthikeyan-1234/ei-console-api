using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Pipeline
{
    public interface ITaskExecutor
    {
        TaskType Handles { get; }

        Task<TaskExecutionOutcome> ExecuteAsync(
            JobTask task,
            PipelineContext context,
            CancellationToken ct);
    }

    /// <summary>
    /// The runner resolves executors through this factory. If a task's type has
    /// no registered executor, the runner fails the task with a clear message
    /// rather than throwing.
    /// </summary>
    public interface ITaskExecutorFactory
    {
        ITaskExecutor? Resolve(TaskType type);
    }

}
