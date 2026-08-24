using System.Text;
using System.Text.Json;
using CyberSopHarness.Core;

internal static class Program
{
    private static int _passed;

    public static async Task<int> Main()
    {
        await Run("tokenizer preserves quoted empty values and comments", TestTokenizer);
        await Run("renderer removes terminal escapes and honors plain mode", TestRendererSanitization);
        await Run("verb registry suggests fixed commands", TestVerbSuggestions);
        await Run("history redacts secrets and enforces retention", TestHistoryRedaction);
        await Run("repl stops on emergency severity", TestEmergencyStopsRepl);
        await Run("repl converts input interruption to emergency stop", TestInterruptEmergency);
        await Run("json rendering emits one clean document", TestJsonRendering);
        Console.WriteLine($"command_desk_tests=passed count={_passed}");
        return 0;
    }

    private static async Task Run(string name, Func<Task> test)
    {
        await test();
        _passed++;
        Console.WriteLine($"PASS {name}");
    }

    private static Task TestTokenizer()
    {
        var tokenize = CommandDeskTokenizer.TryTokenize("model serve \"local model\" '' # bearer secret-token", 1024, out var tokens);
        Assert(tokenize.ExitCode == 0, "valid tokenizer input failed");
        Assert(tokens.Tokens.SequenceEqual(new[] { "model", "serve", "local model", "" }), "quoted tokenizer output was wrong");
        Assert(tokens.Comment == "bearer secret-token", "comment was not captured");
        var rejected = CommandDeskTokenizer.TryTokenize("doctor\a", 1024, out _);
        Assert(rejected.ExitCode == 2, "control character was accepted");
        return Task.CompletedTask;
    }

    private static Task TestRendererSanitization()
    {
        var sanitized = CommandDeskRenderer.Sanitize("\x1b]0;evil\x07target");
        Assert(!sanitized.Contains('\x1b') && !sanitized.Contains('\x07'), "renderer retained escape controls");
        var writer = new StringWriter();
        var renderer = new CommandDeskRenderer(new(false, false, false, 110, false));
        renderer.WriteResult(writer, CommandDeskResult.Success("safe"), "doctor");
        Assert(!writer.ToString().Contains('\x1b'), "plain renderer emitted ANSI");
        return Task.CompletedTask;
    }

    private static Task TestVerbSuggestions()
    {
        Assert(CommandDeskVerbRegistry.Default.Suggest("en", 8).Contains("engagement"), "engagement completion missing");
        Assert(CommandDeskVerbRegistry.Default.Suggest("proposal ", 8).SequenceEqual(new[] { "validate", "submit" }), "proposal subcommands were wrong");
        Assert(CommandDeskVerbRegistry.Default.NearestVerb("engagemnt") == "engagement", "nearest verb suggestion failed");
        Assert(CommandDeskVerbRegistry.Default.NearestVerb("definitely-not-a-verb") is null, "unknown verb produced a false suggestion");
        return Task.CompletedTask;
    }

    private static async Task TestHistoryRedaction()
    {
        using var temp = new TempDirectory();
        var store = new CommandDeskHistoryStore();
        var invocation = new CommandDeskInvocation(
            "model pin api_key=super-secret provider=local",
            "model",
            new[] { "pin" },
            DateTimeOffset.UnixEpoch);
        for (var index = 0; index < 12; index++) await store.AppendAsync(temp.Path, "engagement-a", invocation, 10, CancellationToken.None);
        var path = CommandDeskHistoryStore.HistoryPath(temp.Path, "engagement-a");
        var lines = await File.ReadAllLinesAsync(path);
        Assert(lines.Length == 10, $"history retention kept {lines.Length} entries");
        Assert(!lines.Contains("super-secret"), "history retained an API key");
        Assert(lines.All(line => line.Contains("[REDACTED]", StringComparison.Ordinal)), "history redaction marker was missing");
    }

    private static async Task TestEmergencyStopsRepl()
    {
        using var temp = new TempDirectory();
        var reader = new QueueReader("doctor", "emergency", "doctor");
        var executed = new List<string>();
        var handler = new DelegateCommandDeskHandler((invocation, state, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            executed.Add(invocation.Verb);
            var result = invocation.Verb == "emergency"
                ? new CommandDeskExecution(new(1, CommandDeskSeverity.Critical, "emergency stop engaged", Array.Empty<string>()))
                : new CommandDeskExecution(CommandDeskResult.Success("executed"));
            return Task.FromResult(result);
        });
        var output = new StringWriter();
        var repl = new CommandDeskRepl(
            new CommandDeskRenderer(new(false, false, false, 110, false)),
            CommandDeskVerbRegistry.Default,
            handler,
            new CommandDeskReplOptions(PersistHistory: false),
            temp.Path);
        var state = NewState();
        await repl.RunAsync(output, new StringWriter(), reader, state, CancellationToken.None);
        Assert(executed.SequenceEqual(new[] { "doctor", "emergency" }), "REPL continued after critical emergency result");
        Assert(output.ToString().Contains("[EMERGENCY]", StringComparison.Ordinal), "critical result was not labeled");
    }

    private static async Task TestInterruptEmergency()
    {
        using var root = new CancellationTokenSource();
        var readerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new CancellingReader(root.Token, readerStarted);
        var handler = new DelegateCommandDeskHandler((_, _, _) => Task.FromResult(new CommandDeskExecution(CommandDeskResult.Success("unused"))));
        var output = new StringWriter();
        var repl = new CommandDeskRepl(
            new CommandDeskRenderer(new(true, true, false, 110, false)),
            CommandDeskVerbRegistry.Default,
            handler,
            new CommandDeskReplOptions(PersistHistory: false));
        var run = repl.RunAsync(output, new StringWriter(), reader, NewState(), root.Token);
        await readerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        root.Cancel();
        var exitCode = await run.WaitAsync(TimeSpan.FromSeconds(1));
        Assert(exitCode == 1, "interrupt did not produce critical exit code");
        Assert(output.ToString().Contains("interrupt converted to emergency stop", StringComparison.Ordinal), "interrupt message was missing");
    }

    private static Task TestJsonRendering()
    {
        var writer = new StringWriter();
        var renderer = new CommandDeskRenderer(new(false, false, true, 110, true));
        renderer.WriteResult(writer, CommandDeskResult.UsageError("unknown", "detail"), "unknown");
        var payload = JsonDocument.Parse(writer.ToString());
        Assert(payload.RootElement.GetProperty("exit_code").GetInt32() == 2, "JSON exit code was wrong");
        Assert(!writer.ToString().Contains('\x1b'), "JSON renderer emitted ANSI");
        return Task.CompletedTask;
    }

    private static CommandDeskState NewState() => new(
        "operator",
        "csh",
        "fixture-engagement",
        "scope-fixture",
        "R1",
        "local/lfm2.5",
        0,
        "nominal",
        false,
        DateTimeOffset.UnixEpoch);

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("command desk assertion failed: " + message);
    }

    private sealed class QueueReader(params string[] lines) : ICommandDeskInputReader
    {
        private readonly Queue<string> _lines = new(lines);

        public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(_lines.Count > 0 ? _lines.Dequeue() : null);
        }
    }

    private sealed class CancellingReader(CancellationToken token, TaskCompletionSource<bool> started) : ICommandDeskInputReader
    {
        public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            started.TrySetResult(true);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token);
            return null;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csh-desk-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
