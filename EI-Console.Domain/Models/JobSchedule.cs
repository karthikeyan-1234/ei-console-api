using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class JobSchedule
    {
        public int JobId { get; set; }
        public string CronExpression { get; set; } = null!;
        public string Timezone { get; set; } = "UTC";
        public IntegrationJob Job { get; set; } = null!;
    }
}
