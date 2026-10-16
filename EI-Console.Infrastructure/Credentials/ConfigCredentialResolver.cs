using EI_Console.Application.Interfaces.Infrastructure;
using EI_Console.Application.Interfaces.Services;
using EI_Console.Domain.Enums;
using EI_Console.Domain.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Credentials
{
    /// <summary>
    /// v0.1 resolver. Reads credentials from IConfiguration by the key stored in
    /// the auth profile's *Ref field. Handles ApiKey, WsSecurityUsernameToken,
    /// and SubscriptionTokenExchange.
    ///
    ///   ApiKey:        ApiKeyRef = "Credentials:auth-3-key" -> value = "{token}"
    ///                  Adds header ApiKeyRef's value to the configured HeaderName.
    ///                  For bearer tokens, set HeaderName=Authorization and the
    ///                  config value to "Bearer {jwt}".
    ///
    ///   WsSecurity:    PasswordRef = "Credentials:auth-4-pass" -> value = "{pass}"
    ///                  Adds Authorization: Basic base64(username:password).
    ///
    ///   SubscriptionTokenExchange:
    ///                  Delegates to ISubscriptionTokenResolver, which POSTs
    ///                  the configured credentials to the vendor's token
    ///                  endpoint and returns a bearer token (plus an optional
    ///                  subscription-key header).
    /// </summary>
    public sealed class ConfigCredentialResolver : ICredentialResolver
    {
        private readonly IConfiguration _config;
        private readonly ISubscriptionTokenResolver _subscriptionTokens;
        private readonly ILogger<ConfigCredentialResolver> _log;

        public ConfigCredentialResolver(
            IConfiguration config,
            ISubscriptionTokenResolver subscriptionTokens,
            ILogger<ConfigCredentialResolver> log)
        {
            _config = config;
            _subscriptionTokens = subscriptionTokens;
            _log = log;
        }

        public async Task<IReadOnlyDictionary<string, string>> ResolveHeadersAsync(
            AuthProfile? profile,
            CancellationToken ct = default)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (profile is null)
                return result;

            switch (profile.AuthType)
            {
                case AuthType.ApiKey:
                    ResolveApiKey(profile, result);
                    break;

                case AuthType.WsSecurityUsernameToken:
                    ResolveBasic(profile, result);
                    break;

                case AuthType.SubscriptionTokenExchange:
                    await ResolveSubscriptionTokenAsync(profile, result, ct);
                    break;

                case AuthType.KeycloakAuthCodeExchange:
                case AuthType.OAuth2ClientCredentials:
                    _log.LogWarning(
                        "Auth type {Type} is not supported in v0.1 — connection will be called unauthenticated.",
                        profile.AuthType);
                    break;

                default:
                    _log.LogWarning(
                        "Auth type {Type} is not supported in v0.1.",
                        profile.AuthType);
                    break;
            }

            return result;
        }

        private void ResolveApiKey(AuthProfile profile, IDictionary<string, string> headers)
        {
            var apiKey = profile.ApiKey;
            if (apiKey is null) return;

            var headerName = string.IsNullOrWhiteSpace(apiKey.HeaderName)
                ? "X-Api-Key"
                : apiKey.HeaderName;

            var value = _config[apiKey.ApiKeyRef];
            if (string.IsNullOrWhiteSpace(value))
            {
                _log.LogWarning("Config key {Key} is missing — API key will not be sent.", apiKey.ApiKeyRef);
                return;
            }

            headers[headerName] = value;
        }

        private void ResolveBasic(AuthProfile profile, IDictionary<string, string> headers)
        {
            var ws = profile.WsSecurity;
            if (ws is null) return;

            var password = _config[ws.PasswordRef];
            if (string.IsNullOrWhiteSpace(password))
            {
                _log.LogWarning("Config key {Key} is missing — Basic auth will not be sent.", ws.PasswordRef);
                return;
            }

            var raw = $"{ws.Username}:{password}";
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            headers["Authorization"] = $"Basic {b64}";
        }

        private async Task ResolveSubscriptionTokenAsync(
            AuthProfile profile,
            IDictionary<string, string> headers,
            CancellationToken ct)
        {
            var token = await _subscriptionTokens.ResolveAsync(profile, ct);
            if (token is null)
            {
                _log.LogWarning(
                    "Subscription token exchange failed for profile {Id}",
                    profile.AuthProfileId);
                return;
            }

            headers["Authorization"] = $"{token.TokenPrefix} {token.AccessToken}";

            if (!string.IsNullOrWhiteSpace(token.SubscriptionHeaderName)
                && !string.IsNullOrWhiteSpace(token.SubscriptionHeaderValue))
            {
                headers[token.SubscriptionHeaderName!] = token.SubscriptionHeaderValue!;
            }
        }
    }
}