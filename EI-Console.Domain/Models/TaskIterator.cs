using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class TaskIterator
    {
        public int TaskId { get; set; }
        public TaskType TaskType { get; set; } = TaskType.Transform;
        public int BatchSize { get; set; }
        public int MaxConcurrency { get; set; }
        public JobTask Task { get; set; } = null!;
    }
}
