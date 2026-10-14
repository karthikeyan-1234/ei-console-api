using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class TaskTransform
    {
        public int TaskId { get; set; }
        public TaskType TaskType { get; set; } = TaskType.Transform;
        public string JsonataExpression { get; set; } = null!;
        public string? InputSource { get; set; }
        public bool UseSampleInput { get; set; }
        public string? SampleInputJson { get; set; }
        public JobTask Task { get; set; } = null!;
    }
}
