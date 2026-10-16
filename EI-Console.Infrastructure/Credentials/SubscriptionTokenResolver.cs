using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Infrastructure.Credentials
{
    /// <summary>
    /// Acquires bearer tokens from a token endpoint by POSTing configured
    /// credentials and extracting the token from the response. Caches tokens
    /// in memory keyed by profile id and profile UpdatedAt — profile edits
    /// invalidate naturally because UpdatedAt changes.
    /// </summary>
    public sealed class SubscriptionTokenResolver : ISubscriptionTokenResolver
    {
        private const string HttpClientName = "SubscriptionTokenExchange";

        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SubscriptionTokenResolver> _log;

        public SubscriptionTokenResolver(
            IHttpClientFactory httpFactory,
            IConfiguration config,
            IMemoryCache cache,
            ILogger<SubscriptionTokenResolver> log)
        {
            _httpFactory = httpFactory;
            _config = config;
            _cache = cache;
            _log = log;
        }

        public async Task<ResolvedSubscriptionToken?> ResolveAsync(
            AuthProfile? profile, CancellationToken ct = default)
        {
            if (profile is null) return null;

            var st = profile.SubscriptionToken;
            if (st is null)
            {
                _log.LogWarning(
                    "Auth profile {Id} has no SubscriptionToken subtype loaded",
                    profile.AuthProfileId);
                return null;
            }

            var cacheKey = BuildCacheKey(profile);
            if (_cache.TryGetValue(cacheKey, out ResolvedSubscriptionToken? cached) && cached is not null)
            {
                _log.LogDebug("Subscription token cache hit for profile {Id}", profile.AuthProfileId);
                return cached;
            }

            try
            {
                var token = await AcquireTokenAsync(profile, st, ct);
                if (token is null) return null;

                CacheToken(cacheKey, token);
                return token;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Subscription token acquisition failed for profile {Id}",
                    profile.AuthProfileId);
                return null;
            }
        }

        // -----------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------

        private async Task<ResolvedSubscriptionToken?> AcquireTokenAsync(
            AuthProfile profile, AuthProfileSubscriptionToken st, CancellationToken ct)
        {
            var subscriptionKey = ResolveSecret(st, st.SubscriptionKeySecretRef, st.InlineSubscriptionKey);
            var password = ResolveSecret(st, st.PasswordSecretRef, st.InlinePassword);
            var secretKey = ResolveSecret(st, st.SecretKeySecretRef, st.InlineSecretKey);

            if (string.IsNullOrWhiteSpace(password))
            {
                _log.LogWarning(
                    "Profile {Id}: password not resolved (mode {Mode})",
                    profile.AuthProfileId, st.CredentialStorageMode);
                return null;
            }

            var headerMode = string.Equals(st.SubscriptionKeyMode, "Header", StringComparison.OrdinalIgnoreCase);
            var queryMode = string.Equals(st.SubscriptionKeyMode, "Query", StringComparison.OrdinalIgnoreCase);

            if (headerMode && string.IsNullOrWhiteSpace(subscriptionKey))
            {
                _log.LogWarning(
                    "Profile {Id}: subscription key required (Header mode) but not resolved",
                    profile.AuthProfileId);
                return null;
            }

            // --- Build URI, appending the key as a query param if configured ---
            var uri = st.TokenEndpoint;
            if (queryMode
                && !string.IsNullOrWhiteSpace(subscriptionKey)
                && !string.IsNullOrWhiteSpace(st.SubscriptionKeyQueryParam))
            {
                var sep = uri.Contains('?') ? '&' : '?';
                uri = $"{uri}{sep}{Uri.EscapeDataString(st.SubscriptionKeyQueryParam!)}={Uri.EscapeDataString(subscriptionKey!)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, uri);

            if (headerMode && !string.IsNullOrWhiteSpace(subscriptionKey))
            {
                var headerName = string.IsNullOrWhiteSpace(st.SubscriptionKeyHeader)
                    ? "Ocp-Apim-Subscription-Key"
                    : st.SubscriptionKeyHeader!;
                request.Headers.TryAddWithoutValidation(headerName, subscriptionKey);
            }

            // --- Body ---
            var bodyPairs = BuildBodyPairs(st, password!, secretKey);

            if (string.Equals(st.TokenContentType, "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase))
            {
                request.Content = new FormUrlEncodedContent(
                    bodyPairs.Select(p => new KeyValuePair<string, string>(
                        p.Key, p.Value?.ToString() ?? string.Empty)));
            }
            else
            {
                var json = JsonSerializer.Serialize(bodyPairs);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            // --- Send ---
            var client = _httpFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Token endpoint returned {Status} for profile {Id}. Body: {Body}",
                    (int)response.StatusCode, profile.AuthProfileId, Truncate(body, 500));
                return null;
            }

            // --- Parse ---
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(body);
                root = doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                _log.LogWarning(ex,
                    "Token response not JSON for profile {Id}. Body: {Body}",
                    profile.AuthProfileId, Truncate(body, 500));
                return null;
            }

            // --- Success gate: reject "200 with User Not Found" shapes ---
            if (!string.IsNullOrWhiteSpace(st.SuccessRequiresJsonPath))
            {
                var gate = ExtractJsonPath(root, st.SuccessRequiresJsonPath!);
                if (gate is null || gate.Value.ValueKind == JsonValueKind.Null)
                {
                    _log.LogWarning(
                        "Profile {Id}: success path '{Path}' was null. Body: {Body}",
                        profile.AuthProfileId, st.SuccessRequiresJsonPath, Truncate(body, 500));
                    return null;
                }
            }

            // --- Token extraction ---
            var tokenEl = ExtractJsonPath(root, st.TokenJsonPath);
            if (tokenEl is null || tokenEl.Value.ValueKind != JsonValueKind.String)
            {
                _log.LogWarning(
                    "Profile {Id}: no token at '{Path}'. Body: {Body}",
                    profile.AuthProfileId, st.TokenJsonPath, Truncate(body, 500));
                return null;
            }

            var token = tokenEl.Value.GetString();
            if (string.IsNullOrWhiteSpace(token)) return null;

            var expiresAt = ComputeExpiry(st, token!, root);

            _log.LogInformation(
                "Token acquired for profile {Id}, expires {ExpiresAt:u}",
                profile.AuthProfileId, expiresAt);

            return new ResolvedSubscriptionToken(
                AccessToken: token!,
                TokenPrefix: string.IsNullOrWhiteSpace(st.TokenPrefix) ? "Bearer" : st.TokenPrefix,
                ExpiresAt: expiresAt,
                SubscriptionHeaderName: headerMode
                    ? (string.IsNullOrWhiteSpace(st.SubscriptionKeyHeader)
                        ? "Ocp-Apim-Subscription-Key"
                        : st.SubscriptionKeyHeader)
                    : null,
                SubscriptionHeaderValue: headerMode ? subscriptionKey : null,
                RawResponseJson: body);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private string? ResolveSecret(
            AuthProfileSubscriptionToken st, string? secretRef, string? inline)
        {
            return st.CredentialStorageMode switch
            {
                CredentialStorageMode.Inline => inline,
                CredentialStorageMode.KeyVault => string.IsNullOrWhiteSpace(secretRef)
                    ? null
                    : _config[secretRef],
                _ => inline,
            };
        }

        private Dictionary<string, object?> BuildBodyPairs(
            AuthProfileSubscriptionToken st, string password, string? secretKey)
        {
            var body = new Dictionary<string, object?>
            {
                [st.UsernameFieldName] = st.Username,
                [st.PasswordFieldName] = password,
            };

            if (!string.IsNullOrWhiteSpace(st.SecretKeyFieldName)
                && !string.IsNullOrWhiteSpace(secretKey))
            {
                body[st.SecretKeyFieldName!] = secretKey;
            }

            if (!string.IsNullOrWhiteSpace(st.AdditionalBodyJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(st.AdditionalBodyJson!);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                            body[prop.Name] = prop.Value.Clone();
                    }
                }
                catch (JsonException ex)
                {
                    _log.LogWarning(ex, "AdditionalBodyJson could not be parsed — ignored");
                }
            }

            return body;
        }

        private static DateTimeOffset ComputeExpiry(
            AuthProfileSubscriptionToken st, string token, JsonElement root)
        {
            var now = DateTimeOffset.UtcNow;

            if (string.Equals(st.TtlSource, "JwtExp", StringComparison.OrdinalIgnoreCase))
            {
                var jwt = TryReadJwtExpiry(token);
                if (jwt is not null) return jwt.Value;
            }

            if (string.Equals(st.TtlSource, "ResponseField", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(st.ExpiresInJsonPath))
            {
                var el = ExtractJsonPath(root, st.ExpiresInJsonPath!);
                if (el is not null
                    && el.Value.ValueKind == JsonValueKind.Number
                    && el.Value.TryGetInt32(out var seconds)
                    && seconds > 0)
                {
                    return now.AddSeconds(seconds);
                }
            }

            return now.AddSeconds(Math.Max(30, st.DefaultTtlSeconds));
        }

        private static DateTimeOffset? TryReadJwtExpiry(string token)
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return null;

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var bytes = Convert.FromBase64String(payload);
                using var doc = JsonDocument.Parse(bytes);

                if (!doc.RootElement.TryGetProperty("exp", out var exp)) return null;
                if (exp.ValueKind != JsonValueKind.Number) return null;

                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Minimal JSONPath: supports "$", "$.a", "$.a.b", "$.items[0]",
        /// "$.items[0].name". No wildcards, filters, or recursive descent.
        /// Sufficient for the token response shapes we need.
        /// </summary>
        private static JsonElement? ExtractJsonPath(JsonElement root, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (path == "$") return root;

            var p = path.Trim();
            if (!p.StartsWith('$')) return null;
            p = p[1..];
            if (p.StartsWith('.')) p = p[1..];

            var current = root;

            foreach (var segment in SplitPath(p))
            {
                if (segment.StartsWith('[') && segment.EndsWith(']'))
                {
                    if (!int.TryParse(segment[1..^1], out var idx)) return null;
                    if (current.ValueKind != JsonValueKind.Array) return null;
                    if (idx < 0 || idx >= current.GetArrayLength()) return null;
                    current = current[idx];
                }
                else
                {
                    if (current.ValueKind != JsonValueKind.Object) return null;
                    if (!current.TryGetProperty(segment, out var child)) return null;
                    current = child;
                }
            }

            return current;
        }

        private static IEnumerable<string> SplitPath(string path)
        {
            var start = 0;
            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] == '.' && i > start)
                {
                    yield return path[start..i];
                    start = i + 1;
                }
                else if (path[i] == '[')
                {
                    if (i > start) yield return path[start..i];
                    var close = path.IndexOf(']', i);
                    if (close < 0) break;
                    yield return path[i..(close + 1)];
                    start = close + 1;
                    i = close;
                }
            }
            if (start < path.Length) yield return path[start..];
        }

        private static string BuildCacheKey(AuthProfile profile)
        {
            // UpdatedAt changes whenever the profile is edited — invalidates cache.
            var revision = profile.UpdatedAt?.Ticks ?? 0;
            return $"subscription-token:{profile.AuthProfileId}:{revision}";
        }

        private void CacheToken(string cacheKey, ResolvedSubscriptionToken token)
        {
            var remaining = token.ExpiresAt - DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30);
            if (remaining < TimeSpan.FromSeconds(5)) return;   // near-expiry, don't cache
            _cache.Set(cacheKey, token, remaining);
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max] + "…";
    }
}
