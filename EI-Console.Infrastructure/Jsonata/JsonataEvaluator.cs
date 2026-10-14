using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

using EI_Console.Application.Pipeline;

using Jint;
using Jint.Native;

using Microsoft.Extensions.Logging;

namespace EI_Console.Infrastructure.Jsonata;

/// <summary>
/// JSONata evaluation backed by Jint running the reference jsonata.js
/// implementation. The JS engine is loaded once (singleton); compiled
/// expressions are cached by source string.
///
/// Thread-safety: Jint's Engine is not thread-safe. All engine access —
/// compilation and evaluation — is serialized behind a single lock. This
/// bounds throughput to one evaluation at a time, acceptable for v0.2.
/// </summary>
public sealed class JsonataEvaluator : IJsonataEvaluator
{
    private readonly Engine _engine;
    private readonly JsValue _jsonataFactory;
    private readonly JsValue _jsonParse;
    private readonly JsValue _jsonStringify;
    private readonly ConcurrentDictionary<string, CompiledJsonata> _compiledCache = new();

    private sealed record CompiledJsonata(JsValue Instance, JsValue Evaluate);

    private readonly object _lock = new();
    private readonly ILogger<JsonataEvaluator> _log;

    public JsonataEvaluator(ILogger<JsonataEvaluator> log)
    {
        _log = log;

        _engine = new Engine(options =>
        {
            options.LimitRecursion(128);
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
        });

        // The jsonata.js UMD wrapper assigns to module.exports when it sees a
        // module object. Define a stub module in the global scope before
        // executing the bundle, so it takes that branch.
        _engine.Execute(
            "var module = { exports: {} }; var exports = module.exports;");

        // Cache JSON helpers once so we don't re-evaluate on each call.
        _jsonParse = _engine.Evaluate("JSON.parse");
        _jsonStringify = _engine.Evaluate("JSON.stringify");

        // Load the bundle.
        _engine.Execute(LoadEmbeddedJsonata());

        // Lift the factory out.
        _jsonataFactory = _engine.Evaluate("module.exports");
        if (_jsonataFactory.IsUndefined() || _jsonataFactory.IsNull())
        {
            throw new InvalidOperationException(
                "jsonata.js did not export a factory on module.exports.");
        }

        _log.LogInformation("JSONata engine loaded");
    }

    public Task<JsonElement?> EvaluateAsync(
        string expression, JsonElement? input, CancellationToken ct = default)
    {
        // Fast paths — no need to touch the engine at all.
        if (string.IsNullOrWhiteSpace(expression) || expression.Trim() == "$")
            return Task.FromResult(input);

        return Task.Run(() => EvaluateInternal(expression, input), ct);
    }

    // -----------------------------------------------------------------
    // Internals — all engine access is under _lock.
    // -----------------------------------------------------------------

    private JsonElement? EvaluateInternal(string expression, JsonElement? input)
    {
        try
        {
            lock (_lock)
            {
                var compiled = _compiledCache.GetOrAdd(expression, CompileExpression);

                var inputJs = input is null
                    ? JsValue.Null
                    : _engine.Invoke(_jsonParse, input.Value.GetRawText());

                // jsonata(expr) returns { evaluate, ast, assign, ... }. Invoke
                // the evaluate method with the instance as `this` — jsonata.js's
                // evaluate reads internal state off the object it belongs to.
                var resultJs = _engine.Invoke(
                    compiled.Evaluate,
                    compiled.Instance,
                    inputJs);

                if (resultJs.IsUndefined() || resultJs.IsNull())
                    return null;

                var json = _engine.Invoke(_jsonStringify, resultJs).AsString();

                if (string.IsNullOrEmpty(json) || json == "undefined")
                    return null;

                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.Clone();
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "JSONata evaluation failed for expression: {Expr}", expression);
            return null;
        }
    }

    private CompiledJsonata CompileExpression(string expression)
    {
        var instance = _engine.Invoke(_jsonataFactory, expression);

        if (instance.IsNull() || instance.IsUndefined() || instance.IsObject() == false)
        {
            throw new InvalidOperationException(
                $"jsonata(\"{expression}\") returned a non-object. " +
                "The expression may be syntactically invalid.");
        }

        var evaluateFn = instance.AsObject().Get("evaluate");

        if (evaluateFn.IsUndefined())
        {
            throw new InvalidOperationException(
                "jsonata instance has no evaluate method — unexpected jsonata.js shape.");
        }

        return new CompiledJsonata(instance, evaluateFn);
    }

    private static string LoadEmbeddedJsonata()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(
                "jsonata.min.js", StringComparison.Ordinal));

        if (name is null)
        {
            throw new InvalidOperationException(
                "jsonata.min.js not found as an embedded resource. " +
                "Confirm the .csproj has " +
                "<EmbeddedResource Include=\"Jsonata\\jsonata.min.js\" />.");
        }

        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}