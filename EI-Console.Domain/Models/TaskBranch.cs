using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class TaskBranch
    {
        public int TaskId { get; set; }
        public TaskType TaskType { get; set; } = TaskType.Branch;
        public string? ConditionExpression { get; set; }
        public BranchExecutionMode ExecutionMode { get; set; }
        public JobTask Task { get; set; } = null!;
    }
}
