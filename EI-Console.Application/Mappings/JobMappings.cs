using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Mappings;
    internal static class DateFormats
    {
        public const string DateTimeFull = "dd MMM yyyy HH:mm";
        public const string TimeOnly = "HH:mm:ss";

        public static string Full(this DateTimeOffset? value) =>
            value.HasValue ? value.Value.ToString(DateTimeFull, System.Globalization.CultureInfo.InvariantCulture) : "—";

        public static string Full(this DateTimeOffset value) =>
            value.ToString(DateTimeFull, System.Globalization.CultureInfo.InvariantCulture);

        public static string Time(this DateTimeOffset? value) =>
            value.HasValue ? value.Value.ToString(TimeOnly, System.Globalization.CultureInfo.InvariantCulture) : "—";

        public static string Time(this DateTimeOffset value) =>
            value.ToString(TimeOnly, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static class JobMappings
    {
    public static JobSummaryDto ToSummaryDto(this IntegrationJob j)
    {
        var working = j.Versions
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        var published = j.Versions
            .Where(v => v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        // Task counts come from the working version — that's what the operator
        // is editing, and it matches what the builder displays.
        var tasks = working?.Tasks ?? new List<JobTask>();

        return new JobSummaryDto(
            Id: j.JobId.ToString(),
            Name: j.JobName,
            Slug: j.EndpointSlug,
            TenantId: j.TenantId.ToString(),
            Status: j.Status.ToString(),
            Trigger: j.TriggerType.ToString(),
            Cron: j.Schedule?.CronExpression ?? "—",
            Created: j.CreatedAt.Full(),
            LastRun: "—",
            Next: ComputeNext(j),
            Description: j.Description ?? string.Empty,
            Version: working?.VersionNumber ?? 0,
            PublishedVersion: published?.VersionNumber,
            PublishedAt: published?.PublishedAt?.Full() ?? "—",
            PublishedBy: published?.PublishedByUser?.DisplayName ?? "—",
            TaskCount: tasks.Count(t => t.ParentTaskId is null),
            SubTaskCount: tasks.Count(t => t.TaskRole == Domain.Enums.TaskRole.IteratorSubtask),
            BranchCount: tasks.Count(t => t.TaskType == Domain.Enums.TaskType.Branch),
            IteratorCount: tasks.Count(t => t.Iterator != null));
    }

    public static JobDto ToDto(this IntegrationJob j)
    {
        var working = j.Versions
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        var published = j.Versions
            .Where(v => v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        // Pipeline reflects the working version. If the working version is a
        // draft on top of a published one, the draft is what the operator is
        // editing — returning the published version would hide their changes.
        var rootTasks = working?.Tasks
            .Where(t => t.ParentTaskId is null)
            .OrderBy(t => t.SequenceOrder)
            .ToList() ?? new List<JobTask>();

        return new JobDto(
            Id: j.JobId.ToString(),
            Name: j.JobName,
            Slug: j.EndpointSlug,
            TenantId: j.TenantId.ToString(),
            Status: j.Status.ToString(),
            Trigger: j.TriggerType.ToString(),
            Cron: j.Schedule?.CronExpression ?? "—",
            Created: j.CreatedAt.Full(),
            LastRun: "—",
            Next: ComputeNext(j),
            Description: j.Description ?? string.Empty,
            Version: working?.VersionNumber ?? 0,
            PublishedVersion: published?.VersionNumber,
            PublishedAt: published?.PublishedAt?.Full() ?? "—",
            PublishedBy: published?.PublishedByUser?.DisplayName ?? "—",
            Pipeline: rootTasks.Select(t => t.ToPipelineDto()).ToList());
    }

    public static PipelineTaskDto ToPipelineDto(this JobTask t)
    {
        var cfg = TaskConfigurationSerializer.Deserialize(t.ConfigurationJson);
        var isIterator = t.Iterator is not null;

        var children = t.ChildTasks
            .Where(c => c.TaskRole == Domain.Enums.TaskRole.ForkChild)
            .OrderBy(c => c.SequenceOrder)
            .Select(c => c.ToPipelineDto())
            .ToList();

        var subtasks = t.ChildTasks
            .Where(c => c.TaskRole == Domain.Enums.TaskRole.IteratorSubtask)
            .OrderBy(c => c.SequenceOrder)
            .Select(c => c.ToPipelineDto())
            .ToList();

        return new PipelineTaskDto(
        Id: t.TaskId.ToString(),
        Type: t.TaskType.ToString(),
        Name: t.TaskName,
        SequenceOrder: t.SequenceOrder,
        ConnectionId: t.ConnectionId?.ToString(),
        AuthProfileId: t.AuthProfileId?.ToString(),
        TimeoutSeconds: t.TimeoutSeconds,
        OutputKey: t.OutputKey,
        Url: cfg?.Url,
        Method: cfg?.Method,
        Body: cfg?.Body,
        SampleResponse: cfg?.SampleResponse,
        InputSource: cfg?.InputSource ?? t.Transform?.InputSource,
        Jsonata: cfg?.Jsonata ?? t.Transform?.JsonataExpression,
        UseSampleInput: cfg?.UseSampleInput ?? t.Transform?.UseSampleInput,
        SampleInput: cfg?.SampleInput ?? t.Transform?.SampleInputJson,
        KafkaTopic: cfg?.KafkaTopic,
        Condition: cfg?.Condition ?? t.Branch?.ConditionExpression,
        ExecutionMode: cfg?.ExecutionMode ?? t.Branch?.ExecutionMode.ToString(),
        JoinMode: cfg?.JoinMode,
        JoinThreshold: cfg?.JoinThreshold,
        JoinTimeoutSeconds: cfg?.JoinTimeoutSeconds,
        JoinTimeoutAction: cfg?.JoinTimeoutAction,
        Iterate: isIterator,
        RemotePath: cfg?.RemotePath,
        FileFormat: cfg?.FileFormat,
        DeleteAfterDownload: cfg?.DeleteAfterDownload,
        Children: children.Count > 0 ? children : null,
        Query: cfg?.Query,
        QueryMode: cfg?.QueryMode,
        QueryTimeout: cfg?.QueryTimeout,
        Subtasks: subtasks.Count > 0 ? subtasks : null,
        BatchSize: cfg?.BatchSize,
MaxConcurrency: cfg?.MaxConcurrency,
BodyKind: cfg?.BodyKind,
RawContentType: cfg?.RawContentType,
FormFields: cfg?.FormFields);
    }

    private static string ComputeNext(IntegrationJob j)
        {
            if (j.TriggerType == Domain.Enums.TriggerType.Scheduled && j.Schedule is not null)
                return $"Every {j.Schedule.CronExpression}";
            if (j.TriggerType == Domain.Enums.TriggerType.Webhook)
                return "On-demand";
            return "Manual";
        }
    }
