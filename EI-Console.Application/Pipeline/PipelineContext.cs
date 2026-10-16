using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Application.Pipeline
{
    /// <summary>
    /// The pipeline's running state. Holds the current flowing value plus every
    /// task output that was stored under an output key. Executors read from
    /// this and write back to it. The context is created fresh per execution.
    /// </summary>
    public sealed class PipelineContext
    {
        private readonly Dictionary<string, JsonElement> _outputs =
            new(StringComparer.OrdinalIgnoreCase);

        private JsonElement? _current;

        /// <summary>The value flowing between tasks. Starts null.</summary>
        public JsonElement? Current => _current;

        public void SetCurrent(JsonElement? value) => _current = value;

        public void Store(string key, JsonElement value) => _outputs[key] = value;

        public JsonElement? GetStored(string key) =>
            _outputs.TryGetValue(key, out var v) ? v : null;

        /// <summary>
        /// Creates an independent copy of this context. JsonElement values are
        /// immutable and safe to share; the dictionary itself is copied so
        /// writes to the clone don't leak into the parent (or sibling lanes).
        /// </summary>
        public PipelineContext Clone()
        {
            var clone = new PipelineContext { _current = _current };
            foreach (var (k, v) in _outputs) clone._outputs[k] = v;
            return clone;
        }

        /// <summary>
        /// Resolves a path expression against the current value or a stored key.
        /// Accepted forms:
        ///   "$"                → current value
        ///   "$.field"          → current.field
        ///   "KeyName"          → stored output named KeyName
        ///   "KeyName.field"    → stored output's .field
        /// </summary>
        public JsonElement? Resolve(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return _current;
            var trimmed = path.Trim();

            if (trimmed == "$") return _current;

            if (trimmed.StartsWith("$."))
                return Navigate(_current, trimmed[2..]);

            var stored = GetStored(trimmed);
            if (stored.HasValue) return stored;

            var dot = trimmed.IndexOf('.');
            if (dot > 0)
            {
                var head = trimmed[..dot];
                var tail = trimmed[(dot + 1)..];
                var root = GetStored(head);
                if (root.HasValue) return Navigate(root, tail);
            }

            return Navigate(_current, trimmed);
        }

        private static JsonElement? Navigate(JsonElement? source, string path)
        {
            if (!source.HasValue) return null;
            var cursor = source.Value;

            foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (cursor.ValueKind == JsonValueKind.Object &&
                    cursor.TryGetProperty(segment, out var next))
                {
                    cursor = next;
                    continue;
                }
                return null;
            }

            return cursor;
        }

        /// <summary>Serialises the entire context for logging. Best-effort.</summary>
        public string Snapshot()
        {
            var obj = new Dictionary<string, object?>();
            if (_current.HasValue) obj["__current"] = _current.Value;
            foreach (var (k, v) in _outputs) obj[k] = v;
            return JsonSerializer.Serialize(obj);
        }
    }
}