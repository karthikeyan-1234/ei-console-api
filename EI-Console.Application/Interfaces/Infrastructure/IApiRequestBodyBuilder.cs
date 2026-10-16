using _04_EI_Console.Shared.Application.Contracts;

using EI_Console.Application.Pipeline;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    /// <summary>
    /// Builds the HttpContent for an outbound API call based on the operator's
    /// chosen body kind. Handles JSON, Raw, URL-encoded, and Multipart form-data
    /// with file parts sourced from remote URLs.
    /// </summary>
    public interface IApiRequestBodyBuilder
    {
        /// <summary>
        /// Returns null when there is no body (BodyKind = "None").
        /// The caller assigns the result to HttpRequestMessage.Content and is
        /// responsible for disposing it after the send completes.
        /// </summary>
        Task<HttpContent?> BuildAsync(
            string? bodyKind,
            string? bodyJsonata,
            string? rawContentType,
            IReadOnlyList<FormFieldDto>? formFields,
            PipelineContext context,
            CancellationToken ct);
    }
}
