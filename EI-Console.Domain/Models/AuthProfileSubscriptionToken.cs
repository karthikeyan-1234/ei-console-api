using EI_Console.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Domain.Models
{
    /// <summary>
    /// Auth profile subtype for token-acquisition flows. The resolver POSTs
    /// the configured credentials to TokenEndpoint and parses the response for
    /// a bearer token, which it caches and returns as an Authorization header.
    ///
    /// Covers the "subscription key + username + password + secret key" family —
    /// Azure APIM and its variants — and generalises to any endpoint that
    /// exchanges credentials for a token.
    /// </summary>
    public class AuthProfileSubscriptionToken
    {
        public int AuthProfileId { get; set; }
        public AuthType AuthType { get; set; } = AuthType.SubscriptionTokenExchange;

        // --- Token endpoint ---
        public string TokenEndpoint { get; set; } = null!;
        public string TokenContentType { get; set; } = "application/json";
        public string GrantType { get; set; } = "password";

        // --- Subscription key: where it goes on the wire, and its value ---
        public string SubscriptionKeyMode { get; set; } = "Header";   // Header | Query | None
        public string? SubscriptionKeyHeader { get; set; }             // e.g. "Ocp-Apim-Subscription-Key"
        public string? SubscriptionKeyQueryParam { get; set; }

        // --- Body credentials: three slots, each with configurable wire name + storage ---
        public string UsernameFieldName { get; set; } = "username";
        public string Username { get; set; } = null!;

        public string PasswordFieldName { get; set; } = "password";
        public string? PasswordSecretRef { get; set; }        // when mode is KeyVault
        public string? InlinePassword { get; set; }           // when mode is Inline

        public string? SecretKeyFieldName { get; set; }       // "secretKey" for this vendor
        public string? SecretKeySecretRef { get; set; }
        public string? InlineSecretKey { get; set; }

        public string? SubscriptionKeySecretRef { get; set; }
        public string? InlineSubscriptionKey { get; set; }

        /// <summary>
        /// Which of {SecretRef, Inline} is populated for every slot on this row.
        /// KeyVault: the three SecretRef fields. Inline: the three Inline fields.
        /// </summary>
        public CredentialStorageMode CredentialStorageMode { get; set; } = CredentialStorageMode.Inline;

        // --- Success + token extraction ---
        public string? SuccessRequiresJsonPath { get; set; } = "$.data";
        public string TokenJsonPath { get; set; } = "$.data.accessToken";
        public string TokenPrefix { get; set; } = "Bearer";

        // --- TTL ---
        public string TtlSource { get; set; } = "JwtExp";     // JwtExp | ResponseField | Fixed
        public string? ExpiresInJsonPath { get; set; }
        public int DefaultTtlSeconds { get; set; } = 7200;

        // --- Escape hatch ---
        public string? AdditionalBodyJson { get; set; }

        public AuthProfile AuthProfile { get; set; } = null!;
    }
}
