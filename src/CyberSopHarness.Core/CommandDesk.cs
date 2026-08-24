using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CyberSopHarness.Core;

public enum CommandDeskColorProfile
{
    Plain,
    Ansi,
    TrueColor
}

public enum CommandDeskSeverity
{
    Info,
    Success,
    Warning,
    Error,
    Critical
}

public sealed record CommandDeskState(
    string OperatorName,
    string ControllerId,
    string EngagementLabel,
    string ScopeRef,
    string RiskClass,
    string ProviderModel,
    int PendingApprovals,
    string ResourceHealth,
    bool EmergencyStopped,
    DateTimeOffset Timestamp);

public sealed record CommandDeskResult(
    int ExitCode,
    CommandDeskSeverity Severity,
    string Message,
    IReadOnlyList<string> Details)
{
    public static CommandDeskResult Success(string message, params string[] details) =>
        new(0, CommandDeskSeverity.Success, message, details);

    public static CommandDeskResult Info(string message, params string[] details) =>
        new(0, CommandDeskSeverity.Info, message, details);

    public static CommandDeskResult Warning(string message, params string[] details) =>
        new(0, CommandDeskSeverity.Warning, message, details);

    public static CommandDeskResult UsageError(string message, params string[] details) =>
        new(2, CommandDeskSeverity.Error, message, details);

    public static CommandDeskResult Failure(string message, params string[] details) =>
        new(1, CommandDeskSeverity.Error, message, details);

    public static CommandDeskResult Critical(string message, params string[] details) =>
        new(1, CommandDeskSeverity.Critical, message, details);
}

public sealed record CommandDeskInvocation(
    string RawInput,
    string Verb,
    IReadOnlyList<string> Arguments,
    DateTimeOffset Timestamp);

public sealed record CommandDeskExecution(CommandDeskResult Result, CommandDeskState? State = null);

public interface ICommandDeskHandler
{
    Task<CommandDeskExecution> ExecuteAsync(
        CommandDeskInvocation invocation,
        CommandDeskState state,
        CancellationToken cancellationToken);
}

public sealed class DelegateCommandDeskHandler : ICommandDeskHandler
{
    private readonly Func<CommandDeskInvocation, CommandDeskState, CancellationToken, Task<CommandDeskExecution>> _handler;

    public DelegateCommandDeskHandler(Func<CommandDeskInvocation, CommandDeskState, CancellationToken, Task<CommandDeskExecution>> handler) =>
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    public Task<CommandDeskExecution> ExecuteAsync(
        CommandDeskInvocation invocation,
        CommandDeskState state,
        CancellationToken cancellationToken) =>
        _handler(invocation, state, cancellationToken);
}

public sealed record CommandDeskRenderOptions(
    bool NoColor,
    bool Compact,
    bool JsonOutput,
    int Width,
    bool OutputIsTerminal)
{
    public static CommandDeskRenderOptions FromEnvironment(string[] args, bool outputIsTerminal)
    {
        var noColor = args.Contains("--no-color", StringComparer.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_COLOR"));
        var widthIndex = Array.IndexOf(args, "--width");
        var width = widthIndex >= 0 && widthIndex + 1 < args.Length && int.TryParse(args[widthIndex + 1], out var parsedWidth)
            ? parsedWidth
            : 110;
        return new(
            noColor,
            args.Contains("--compact", StringComparer.OrdinalIgnoreCase),
            args.Contains("--json", StringComparer.OrdinalIgnoreCase),
            width,
            outputIsTerminal);
    }
}

public sealed class CommandDeskRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    private const string Escape = "\x1b";
    private readonly CommandDeskColorProfile _profile;
    private readonly bool _jsonOutput;
    private readonly bool _outputIsTerminal;

    public CommandDeskRenderer(CommandDeskRenderOptions options)
    {
        _jsonOutput = options.JsonOutput;
        _outputIsTerminal = options.OutputIsTerminal;
        if (options.NoColor || options.JsonOutput || !options.OutputIsTerminal)
        {
            _profile = CommandDeskColorProfile.Plain;
        }
        else if (string.Equals(Environment.GetEnvironmentVariable("COLORTERM"), "truecolor", StringComparison.OrdinalIgnoreCase))
        {
            _profile = CommandDeskColorProfile.TrueColor;
        }
        else
        {
            _profile = CommandDeskColorProfile.Ansi;
        }
    }

    public bool IsJsonMode => _jsonOutput;

    public void WriteBanner(TextWriter writer, CommandDeskState state)
    {
        writer.WriteLine(Color("Cyber Command Desk — governed mode", ForegroundGray));
        writer.WriteLine(Color(Sanitize($"operator={state.OperatorName} controller={state.ControllerId}"), ForegroundGray));
    }

    public void WritePrompt(TextWriter writer, CommandDeskState state)
    {
        var time = state.Timestamp.ToLocalTime().ToString("HH:mm-dd/MM", CultureInfo.InvariantCulture);
        var context = TruncateByRune($"{state.EngagementLabel}/{state.ScopeRef}", Math.Max(12, WidthForPrompt - 34));
        if (state.EmergencyStopped) context = "EMERGENCY-STOP";
        var suffix = state.EmergencyStopped ? " [STOPPED]" : "";
        writer.Write(Color("┌[", ForegroundRed));
        writer.Write(Color(TruncateByRune(state.ControllerId, 24), ForegroundCyan));
        writer.Write(Color("]─[", ForegroundRed));
        writer.Write(Color(time + suffix, ForegroundYellow));
        writer.Write(Color("]─[", ForegroundRed));
        writer.Write(Color(context, ForegroundMagenta));
        writer.Write(Color("]", ForegroundRed));
        writer.WriteLine();
        writer.Write(Color("└╼", ForegroundRed));
        writer.Write(Color(state.OperatorName, ForegroundGreen));
        writer.Write(Color("❯ ", ForegroundYellow));
    }

    public void WriteResult(TextWriter writer, CommandDeskResult result, string? verb)
    {
        if (_jsonOutput)
        {
            writer.WriteLine(JsonSerializer.Serialize(
                new
                {
                    exit_code = result.ExitCode,
                    severity = result.Severity.ToString().ToLowerInvariant(),
                    verb,
                    message = result.Message,
                    details = result.Details
                },
                JsonOptions));
            return;
        }
        if (!_jsonOutput && _outputIsTerminal && verb is not null)
        {
            SetTitle(writer, verb);
        }
        if (result.Severity == CommandDeskSeverity.Critical)
        {
            writer.WriteLine(Color("[EMERGENCY] " + result.Message, ForegroundIntenseRed));
        }
        else if (result.ExitCode == 0 && result.Severity == CommandDeskSeverity.Success)
        {
            writer.WriteLine(Color("[OK] " + result.Message, ForegroundIntenseGreen));
        }
        else if (result.ExitCode == 0)
        {
            writer.WriteLine(Color("[INFO] " + result.Message, ForegroundCyan));
        }
        else if (result.ExitCode == 2)
        {
            writer.WriteLine(Color("[USAGE] " + result.Message, ForegroundIntenseYellow));
        }
        else
        {
            writer.WriteLine(Color("[ERROR] " + result.Message, ForegroundIntenseRed));
        }
        foreach (var detail in result.Details)
        {
            writer.WriteLine(Color("  " + Sanitize(detail), ForegroundGray));
        }
    }

    public static void SetTitle(TextWriter writer, string verb)
    {
        var safeVerb = Sanitize(TruncateByRune(verb, 48)).Replace('\n', ' ');
        writer.Write(Escape + "]0;" + safeVerb + " - Cyber Command Desk\a");
    }

    public static string Sanitize(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value == '\t' || rune.Value == '\n' || (!Rune.IsControl(rune) && rune.Value != Escape.First()))
            {
                builder.Append(rune.ToString());
            }
            else if (!Rune.IsControl(rune))
            {
                builder.Append(rune.ToString());
            }
        }
        return builder.ToString();
    }

    public static string TruncateByRune(string value, int maxLength)
    {
        if (maxLength < 1) throw new ArgumentOutOfRangeException(nameof(maxLength));
        var runes = value.EnumerateRunes().ToArray();
        if (runes.Length <= maxLength) return value;
        return string.Concat(runes.Take(Math.Max(0, maxLength - 1)).Select(rune => rune.ToString())) + "…";
    }

    private int WidthForPrompt => 110;

    private string Color(string value, string ansiColor) => _profile == CommandDeskColorProfile.Plain ? Sanitize(value) : ansiColor + Sanitize(value) + Reset;

    private const string Reset = Escape + "[0m";
    private const string ForegroundRed = Escape + "[31m";
    private const string ForegroundGreen = Escape + "[32m";
    private const string ForegroundYellow = Escape + "[33m";
    private const string ForegroundBlue = Escape + "[34m";
    private const string ForegroundMagenta = Escape + "[35m";
    private const string ForegroundCyan = Escape + "[36m";
    private const string ForegroundGray = Escape + "[90m";
    private const string ForegroundIntenseRed = Escape + "[91m";
    private const string ForegroundIntenseGreen = Escape + "[92m";
    private const string ForegroundIntenseYellow = Escape + "[93m";
}

public sealed record CommandDeskTokenization(IReadOnlyList<string> Tokens, string Comment);

public static class CommandDeskTokenizer
{
    public static CommandDeskResult TryTokenize(
        string input,
        int maxInputLength,
        out CommandDeskTokenization tokenization)
    {
        tokenization = new(Array.Empty<string>(), string.Empty);
        if (input.Length > maxInputLength) return CommandDeskResult.UsageError($"input exceeds {maxInputLength} characters; rejected as possible paste");
        var tokens = new List<string>();
        var builder = new StringBuilder();
        char? quote = null;
        var hadToken = false;
        var comment = new StringBuilder();
        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (quote is null && (char.IsControl(character) && character is not '\t'))
            {
                return CommandDeskResult.UsageError("control characters are not allowed in desk input");
            }
            if (quote == '"' && character == '\\' && index + 1 < input.Length)
            {
                builder.Append(input[++index]);
                continue;
            }
            if (quote.HasValue)
            {
                if (character == quote)
                {
                    quote = null;
                }
                else
                {
                    builder.Append(character);
                }
                continue;
            }
            if (character is '"' or '\'')
            {
                quote = character;
                hadToken = true;
                continue;
            }
            if (character == '#' && builder.Length == 0)
            {
                comment.Append(input[(index + 1)..].Trim());
                break;
            }
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 || hadToken)
                {
                    tokens.Add(builder.ToString());
                    builder.Clear();
                    hadToken = false;
                }
                continue;
            }
            builder.Append(character);
        }
        if (quote.HasValue) return CommandDeskResult.UsageError("unterminated quoted string");
        if (builder.Length > 0 || hadToken) tokens.Add(builder.ToString());
        if (tokens.Count == 0 && comment.Length > 0) return CommandDeskResult.Info("comment ignored");
        tokenization = new(tokens, comment.ToString());
        return CommandDeskResult.Success("tokenized");
    }
}

public sealed record CommandDeskVerb(string Name, string Summary, IReadOnlyList<string> Subcommands);

public interface ICommandDeskVerbRegistry
{
    IReadOnlyList<CommandDeskVerb> Verbs { get; }
    IReadOnlyList<string> Suggest(string input, int limit);
    string? NearestVerb(string verb);
}

public sealed class CommandDeskVerbRegistry : ICommandDeskVerbRegistry
{
    private readonly IReadOnlyList<CommandDeskVerb> _verbs;
    private readonly Func<string, IReadOnlyList<string>>? _dynamicSuggestions;

    public CommandDeskVerbRegistry(IReadOnlyList<CommandDeskVerb> verbs, Func<string, IReadOnlyList<string>>? dynamicSuggestions = null)
    {
        if (verbs.Count == 0) throw new ArgumentException("verb registry cannot be empty", nameof(verbs));
        _verbs = verbs.OrderBy(verb => verb.Name, StringComparer.Ordinal).ToArray();
        _dynamicSuggestions = dynamicSuggestions;
    }

    public static CommandDeskVerbRegistry Default { get; } = new(
        new[]
        {
            new CommandDeskVerb("action", "inspect governed action state", new[] { "status", "cancel" }),
            new CommandDeskVerb("doctor", "run preflight checks", Array.Empty<string>()),
            new CommandDeskVerb("engagement", "inspect authorization state", new[] { "validate", "show" }),
            new CommandDeskVerb("evidence", "inspect evidence integrity and export", new[] { "list", "verify", "export" }),
            new CommandDeskVerb("exit", "leave the command desk", Array.Empty<string>()),
            new CommandDeskVerb("help", "show desk help", Array.Empty<string>()),
            new CommandDeskVerb("model", "inspect pinned model/runtime", new[] { "pin", "serve", "status" }),
            new CommandDeskVerb("proposal", "parse and submit a proposal", new[] { "validate", "submit" }),
            new CommandDeskVerb("report", "build an evidence-backed report", new[] { "build" }),
            new CommandDeskVerb("status", "show provider and endpoint selection", Array.Empty<string>()),
            new CommandDeskVerb("emergency", "stop all governed work", new[] { "stop", "status" })
        });

    public IReadOnlyList<CommandDeskVerb> Verbs => _verbs;

    public IReadOnlyList<string> Suggest(string input, int limit)
    {
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var requestingNextToken = input.Length > 0 && char.IsWhiteSpace(input[^1]);
        var results = new List<string>();
        if (parts.Length <= 1 && !requestingNextToken)
        {
            var prefix = parts.Length == 0 ? string.Empty : parts[0];
            results.AddRange(_verbs.Select(verb => verb.Name).Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        }
        else
        {
            var verb = _verbs.FirstOrDefault(item => item.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
            if (verb is not null)
            {
                var prefix = requestingNextToken || parts.Length < 2 ? string.Empty : parts[^1];
                results.AddRange(verb.Subcommands.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
            }
        }
        if (_dynamicSuggestions is not null && parts.Length >= 2)
        {
            results.AddRange(_dynamicSuggestions(input).Where(item => !results.Contains(item, StringComparer.Ordinal)));
        }
        return results.Take(limit).ToArray();
    }

    public string? NearestVerb(string verb)
    {
        foreach (var candidate in _verbs)
        {
            var distance = LevenshteinDistance(verb.ToLowerInvariant(), candidate.Name.ToLowerInvariant());
            if (distance <= Math.Min(2, candidate.Name.Length / 2)) return candidate.Name;
        }
        return null;
    }

    private static int LevenshteinDistance(string first, string second)
    {
        var previous = new int[second.Length + 1];
        var current = new int[second.Length + 1];
        for (var column = 0; column <= second.Length; column++) previous[column] = column;
        for (var row = 0; row < first.Length; row++)
        {
            current[0] = row + 1;
            for (var column = 0; column < second.Length; column++)
            {
                var substitution = previous[column] + (first[row] == second[column] ? 0 : 1);
                current[column + 1] = Math.Min(previous[column + 1] + 1, Math.Min(current[column] + 1, substitution));
            }
            (previous, current) = (current, previous);
        }
        return previous[second.Length];
    }
}

public sealed record CommandDeskHistoryEntry(DateTimeOffset Timestamp, string EngagementRef, string Command);

public sealed class CommandDeskHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly Regex SecretAssignment = new(
        @"(?i)(api[_-]?key|access[_-]?token|refresh[_-]?token|secret|password|passwd|pwd|authorization)(\s*[=:]\s*)([""']?)[^\s""']+\3",
        RegexOptions.Compiled);
    private static readonly Regex BearerToken = new(@"(?i)bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.Compiled);

    public static string Redact(string command)
    {
        var redacted = BearerToken.Replace(command, "bearer [REDACTED]");
        redacted = SecretAssignment.Replace(redacted, "$1$2$3[REDACTED]$3");
        if (redacted.Contains("BEGIN", StringComparison.Ordinal) && redacted.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            redacted = "[REDACTED_PRIVATE_KEY]";
        }
        return redacted;
    }

    public async Task AppendAsync(
        string directory,
        string engagementLabel,
        CommandDeskInvocation invocation,
        int maxEntries,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = HistoryPath(directory, engagementLabel);
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            var entry = JsonSerializer.Serialize(
                new CommandDeskHistoryEntry(invocation.Timestamp, engagementLabel, Redact(invocation.RawInput)),
                JsonOptions);
            var existingLines = File.Exists(path) ? await File.ReadAllLinesAsync(path, cancellationToken) : Array.Empty<string>();
            var retained = existingLines.Length >= maxEntries
                ? existingLines[^Math.Max(1, maxEntries - 1)..]
                : existingLines;
            await File.WriteAllLinesAsync(path, retained.Append(entry), cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static string HistoryPath(string directory, string engagementLabel) =>
        Path.Combine(directory, "command-history-" + Canonicalization.Sha256Hex(engagementLabel)[..16] + ".jsonl");
}

public interface ICommandDeskInputReader
{
    ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken);
}

public sealed class TextReaderCommandDeskInputReader : ICommandDeskInputReader
{
    private readonly TextReader _reader;

    public TextReaderCommandDeskInputReader(TextReader reader) => _reader = reader;

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
#if NET7_0_OR_GREATER
        return await _reader.ReadLineAsync(cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(_reader.ReadLine, cancellationToken);
#endif
    }
}

public sealed class ConsoleCommandDeskInputReader : ICommandDeskInputReader
{
    public ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<string?>(Console.ReadLine());
    }
}

public sealed record CommandDeskReplOptions(
    int MaxInputLength = 8192,
    int MaxHistoryEntries = 10000,
    bool PersistHistory = true,
    int CompletionLimit = 8)
{ }

public sealed class CommandDeskRepl
{
    private readonly CommandDeskRenderer _renderer;
    private readonly ICommandDeskVerbRegistry _registry;
    private readonly ICommandDeskHandler _handler;
    private readonly CommandDeskReplOptions _options;
    private readonly string? _historyDirectory;

    public CommandDeskRepl(
        CommandDeskRenderer renderer,
        ICommandDeskVerbRegistry registry,
        ICommandDeskHandler handler,
        CommandDeskReplOptions? options = null,
        string? historyDirectory = null)
    {
        _renderer = renderer;
        _registry = registry;
        _handler = handler;
        _options = options ?? new();
        _historyDirectory = historyDirectory;
    }

    public async Task<int> RunAsync(
        TextWriter output,
        TextWriter error,
        ICommandDeskInputReader reader,
        CommandDeskState initialState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(reader);
        var state = initialState;
        if (!_renderer.IsJsonMode) _renderer.WriteBanner(output, state);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        while (!state.EmergencyStopped)
        {
            if (!_renderer.IsJsonMode) _renderer.WritePrompt(output, state);
            string? line;
            try
            {
                line = await reader.ReadLineAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                state = state with { EmergencyStopped = true };
                var interrupted = CommandDeskResult.Critical("interrupt converted to emergency stop; running workers must be cancelled separately");
                _renderer.WriteResult(output, interrupted, "emergency");
                return interrupted.ExitCode;
            }
            if (line is null) break;
            var tokenize = CommandDeskTokenizer.TryTokenize(line, _options.MaxInputLength, out var tokenization);
            if (tokenize.ExitCode != 0)
            {
                _renderer.WriteResult(output, tokenize, null);
                continue;
            }
            if (tokenization.Tokens.Count == 0) continue;
            var invocation = new CommandDeskInvocation(line, tokenization.Tokens[0], tokenization.Tokens.Skip(1).ToArray(), DateTimeOffset.UtcNow);
            if (invocation.Verb.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
            if (_registry.NearestVerb(invocation.Verb) is null)
            {
                var suggestion = _registry.NearestVerb(invocation.Verb);
                var unknown = suggestion is null
                    ? CommandDeskResult.UsageError($"unknown verb '{CommandDeskRenderer.TruncateByRune(invocation.Verb, 64)}'", "run help for the fixed verb list")
                    : CommandDeskResult.UsageError($"unknown verb '{CommandDeskRenderer.TruncateByRune(invocation.Verb, 64)}'", $"nearest registered verb: {suggestion}");
                _renderer.WriteResult(output, unknown, null);
                continue;
            }
            if (_options.PersistHistory && _historyDirectory is not null)
            {
                try
                {
                    await new CommandDeskHistoryStore().AppendAsync(
                        _historyDirectory,
                        state.EngagementLabel,
                        invocation,
                        _options.MaxHistoryEntries,
                        linked.Token);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    error.WriteLine("ERROR: command history could not be written; continuing without persistence");
                }
            }
            CommandDeskExecution execution;
            try
            {
                execution = await _handler.ExecuteAsync(invocation, state, linked.Token);
            }
            catch (OperationCanceledException)
            {
                execution = new(
                    new CommandDeskResult(
                        1,
                        CommandDeskSeverity.Critical,
                        "operation cancelled; emergency stop engaged",
                        Array.Empty<string>()),
                    state with { EmergencyStopped = true });
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or TimeoutException)
            {
                execution = new(CommandDeskResult.Failure(exception.Message), state);
            }
            state = execution.State ?? state;
            if (execution.Result.Severity == CommandDeskSeverity.Critical) state = state with { EmergencyStopped = true };
            _renderer.WriteResult(output, execution.Result, invocation.Verb);
            if (state.EmergencyStopped) break;
        }
        return 0;
    }
}
