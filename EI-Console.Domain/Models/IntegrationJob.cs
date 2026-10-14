using EI_Console.Domain.Enums;

using Microsoft.VisualBasic;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class IntegrationJob
    {
        public int JobId { get; set; }
        public int TenantId { get; set; }
        public string JobName { get; set; } = null!;
        public string EndpointSlug { get; set; } = null!;
        public string? Description { get; set; }
        public TriggerType TriggerType { get; set; }
        public JobStatus Status { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public Tenant Tenant { get; set; } = null!;
        public User? CreatedByUser { get; set; }
        public JobSchedule? Schedule { get; set; }
        public ICollection<JobVersion> Versions { get; set; } = new List<JobVersion>();
        public ICollection<JobExecution> Executions { get; set; } = new List<JobExecution>();
        public ICollection<SyncWatermark> Watermarks { get; set; } = new List<SyncWatermark>();
    }
}
