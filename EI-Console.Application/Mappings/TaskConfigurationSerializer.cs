using _04_EI_Console.Shared.Application.Contracts;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EI_Console.Application.Mappings
{
    /// <summary>
    /// Serialises the type-specific fields of a PipelineTaskDto into the
    /// configuration_json column, and back. The shape must remain forward-
    /// compatible: fields added here in future versions should not break
    /// parsing of stored payloads, so Deserialize is permissive (unknown
    /// fields are ignored, missing fields are null).
    /// </summary>
    internal static class TaskConfigurationSerializer
    {

        private static readonly JsonSerializerOptions Options = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        public static string? Serialize(PipelineTaskDto dto)
        {
            var payload = new TaskConfig
            {
                Url = dto.Url,
                Method = dto.Method,
                Body = dto.Body,
                SampleResponse = dto.SampleResponse,
                InputSource = dto.InputSource,
                Jsonata = dto.Jsonata,
                UseSampleInput = dto.UseSampleInput,
                SampleInput = dto.SampleInput,
                KafkaTopic = dto.KafkaTopic,
                Condition = dto.Condition,
                ExecutionMode = dto.ExecutionMode,
                JoinMode = dto.JoinMode,
                JoinThreshold = dto.JoinThreshold,
                JoinTimeoutSeconds = dto.JoinTimeoutSeconds,
                JoinTimeoutAction = dto.JoinTimeoutAction,
                RemotePath = dto.RemotePath,
                FileFormat = dto.FileFormat,
                DeleteAfterDownload = dto.DeleteAfterDownload,
                Query = dto.Query,
                QueryTimeout = dto.QueryTimeout,
                BatchSize = dto.BatchSize,
                MaxConcurrency = dto.MaxConcurrency,
            };

            var json = JsonSerializer.Serialize(payload, Options);
            return json == "{}" ? null : json;
        }

        public static TaskConfig? Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<TaskConfig>(json, Options);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        internal sealed class TaskConfig
        {
            public string? Url { get; set; }
            public string? Method { get; set; }
            public string? Body { get; set; }
            public string? SampleResponse { get; set; }
            public string? InputSource { get; set; }
            public string? Jsonata { get; set; }
            public bool? UseSampleInput { get; set; }
            public string? SampleInput { get; set; }
            public string? KafkaTopic { get; set; }
            public string? Condition { get; set; }
            public string? ExecutionMode { get; set; }
            public string? JoinMode { get; set; }
            public int? JoinThreshold { get; set; }
            public int? JoinTimeoutSeconds { get; set; }
            public string? JoinTimeoutAction { get; set; }

            // FtpDownload
            public string? RemotePath { get; set; }
            public string? FileFormat { get; set; }        // "Auto" | "Json" | "Csv" | "Text"
            public bool? DeleteAfterDownload { get; set; }

            public string? Query { get; set; }
            public int? QueryTimeout { get; set; }

            public int? BatchSize { get; set; }
            public int? MaxConcurrency { get; set; }

        }
    }
}
