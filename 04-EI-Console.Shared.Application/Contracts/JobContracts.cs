using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application.Contracts
{
    public sealed record JobSummaryDto(
       string Id,
       string Name,
       string Slug,
       string TenantId,
       string Status,
       string Trigger,
       string Cron,
       string Created,
       string LastRun,
       string Next,
       string Description,
       int Version,
       string PublishedAt,
       string PublishedBy,
       int TaskCount,
       int SubTaskCount,
       int BranchCount,
       int IteratorCount);

    public sealed record JobDto(
        string Id,
        string Name,
        string Slug,
        string TenantId,
        string Status,
        string Trigger,
        string Cron,
        string Created,
        string LastRun,
        string Next,
        string Description,
        int Version,
        string PublishedAt,
        string PublishedBy,
        IReadOnlyList<PipelineTaskDto> Pipeline);

    public sealed record PipelineTaskDto(
        string Id,
        string Type,
        string Name,
        int SequenceOrder,
        string? ConnectionId,
        string? AuthProfileId,
        int? TimeoutSeconds,
        string? OutputKey,
        string? Url,
        string? Method,
        string? Body,
        string? SampleResponse,
        string? InputSource,
        string? Jsonata,
        bool? UseSampleInput,
        string? SampleInput,
        string? KafkaTopic,
        string? Condition,
        string? ExecutionMode,
        string? JoinMode,
        int? JoinThreshold,
        int? JoinTimeoutSeconds,
        string? JoinTimeoutAction,
        bool? Iterate,
            string? RemotePath,
    string? FileFormat,
    bool? DeleteAfterDownload,
        string? Query,
    int? QueryTimeout,
        int? BatchSize,
    int? MaxConcurrency,
        IReadOnlyList<PipelineTaskDto>? Children,
        IReadOnlyList<PipelineTaskDto>? Subtasks);

    public sealed record CreateJobRequest(
        string Name,
        string TenantId,
        string Trigger,
        string? Cron,
        string? Description);

    public sealed record UpdateJobRequest(
        string Name,
        string? Trigger,
        string? Cron,
        string? Description);

    public sealed record SavePipelineRequest(IReadOnlyList<PipelineTaskDto> Tasks);
    public sealed record UpdateJobStatusRequest(string Status);
}
