using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Pipeline
{
    public sealed class TaskExecutorFactory : ITaskExecutorFactory
    {
        private readonly Dictionary<TaskType, List<ITaskExecutor>> _byType;

        public TaskExecutorFactory(IEnumerable<ITaskExecutor> executors)
        {
            _byType = executors
                .GroupBy(e => e.Handles)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        public ITaskExecutor? Resolve(TaskType type) =>
            _byType.TryGetValue(type, out var list) && list.Count > 0 ? list[0] : null;

        public IReadOnlyList<ITaskExecutor> ResolveAll(TaskType type) =>
            _byType.TryGetValue(type, out var list) ? list : Array.Empty<ITaskExecutor>();

        public ITaskExecutor? ResolveForConnection(TaskType type, Protocol? protocol)
        {
            if (!_byType.TryGetValue(type, out var list) || list.Count == 0) return null;

            // Prefer an executor whose RequiredProtocol matches the connection's.
            var byProtocol = list.FirstOrDefault(e =>
                e.RequiredProtocol is not null && e.RequiredProtocol == protocol);
            if (byProtocol is not null) return byProtocol;

            // Fall back to an executor with no protocol preference.
            var openToAll = list.FirstOrDefault(e => e.RequiredProtocol is null);
            return openToAll ?? list[0];
        }
    }
}
