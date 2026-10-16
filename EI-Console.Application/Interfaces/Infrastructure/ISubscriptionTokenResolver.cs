using EI_Console.Domain.Models;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    /// <summary>
    /// A token acquired from a vendor's authorize endpoint. Carries the bearer
    /// token, plus the subscription-key header that must accompany every call
    /// to the same provider (some vendors require both, not just one).
    /// </summary>
    public sealed record ResolvedSubscriptionToken(
        string AccessToken,
        string TokenPrefix,
        DateTimeOffset ExpiresAt,
        string? SubscriptionHeaderName,
        string? SubscriptionHeaderValue,
        string RawResponseJson);

    public interface ISubscriptionTokenResolver
    {
        /// <summary>
        /// Acquires (or returns from cache) a bearer token for the given
        /// SubscriptionTokenExchange auth profile. Returns null on any failure;
        /// failures are logged with the vendor's response body.
        /// </summary>
        Task<ResolvedSubscriptionToken?> ResolveAsync(
            AuthProfile? profile, CancellationToken ct = default);
    }
}
