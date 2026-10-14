using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    public class TaskSqlQuery
    {
        public int TaskId { get; set; }
        public TaskType TaskType { get; set; } = TaskType.SqlQuery;
        public string QueryText { get; set; } = null!;
        public int QueryTimeoutSeconds { get; set; }
        public JobTask Task { get; set; } = null!;
    }
}
