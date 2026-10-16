using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application.Contracts.Jobs
{
    /// <summary>
    /// One HTTP header on an ApiPull, ApiPush, or Notify task. Values are
    /// templated at execution time: any {{...}} placeholder is resolved
    /// against the pipeline context before the request is sent.
    ///
    /// Placeholder syntax matches PipelineContext.Resolve:
    ///   {{storeKey}}            → stored output named storeKey
    ///   {{$.path}}              → current value, navigated
    ///   {{storeKey.dotted.path}} → stored output, then navigated
    ///   "Bearer {{token}}"      → literal text with substitutions
    /// </summary>
    public sealed record HeaderDto(string Key, string Value);
}
