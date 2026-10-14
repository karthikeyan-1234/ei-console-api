using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class JobTask
    {
        public int TaskId { get; set; }
        public int VersionId { get; set; }
        public int? ParentTaskId { get; set; }
        public TaskRole TaskRole { get; set; } = TaskRole.Root;
        public TaskType TaskType { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public int SequenceOrder { get; set; }
        public int? ConnectionId { get; set; }
        public int? AuthProfileId { get; set; }
        public int? TimeoutSeconds { get; set; }
        public string? OutputKey { get; set; }

        // NEW — type-specific configuration as JSON. Holds the fields that don't
        // have their own columns: Url, Method, Body, SampleResponse, Jsonata,
        // InputSource, KafkaTopic, Condition, JoinMode, etc.
        public string? ConfigurationJson { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        // Navigation
        public JobVersion Version { get; set; } = null!;
        public JobTask? ParentTask { get; set; }
        public ICollection<JobTask> ChildTasks { get; set; } = new List<JobTask>();
        public Connection? Connection { get; set; }
        public AuthProfile? AuthProfile { get; set; }

        // Optional one-to-one subtype navigations (unused in v0.1 save, kept for
        // future projection population)
        public TaskTransform? Transform { get; set; }
        public TaskBranch? Branch { get; set; }
        public TaskIterator? Iterator { get; set; }
        public TaskSqlQuery? SqlQuery { get; set; }
    }
}
