using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class JobVersion
    {
        public int VersionId { get; set; }
        public int JobId { get; set; }
        public int VersionNumber { get; set; }
        public string PipelineJson { get; set; } = null!;
        public int PipelineSchemaVersion { get; set; }
        public bool IsPublished { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public int? PublishedByUserId { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public IntegrationJob Job { get; set; } = null!;
        public User? PublishedByUser { get; set; }
        public User? CreatedByUser { get; set; }
        public ICollection<JobTask> Tasks { get; set; } = new List<JobTask>();
        public ICollection<JobExecution> Executions { get; set; } = new List<JobExecution>();
    }
}
