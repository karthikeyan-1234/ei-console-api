using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class SyncWatermark
    {
        public int WatermarkId { get; set; }
        public int JobId { get; set; }
        public string EntityName { get; set; } = null!;
        public WatermarkType WatermarkType { get; set; }
        public string WatermarkValue { get; set; } = null!;
        public DateTimeOffset UpdatedAt { get; set; }

        public IntegrationJob Job { get; set; } = null!;
    }
}
