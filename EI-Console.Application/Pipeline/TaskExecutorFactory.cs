using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Pipeline
{
    public sealed class TaskExecutorFactory : ITaskExecutorFactory
    {
        private readonly Dictionary<TaskType, ITaskExecutor> _byType;

        public TaskExecutorFactory(IEnumerable<ITaskExecutor> executors)
        {
            _byType = executors.ToDictionary(e => e.Handles);
        }

        public ITaskExecutor? Resolve(TaskType type) =>
            _byType.TryGetValue(type, out var e) ? e : null;
    }
}
