using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexus.Core.Services;

public sealed record CommandIntent(CommandKind Kind, string Payload, string? Result = null);

public sealed partial class CommandInterpreter
{
    [GeneratedRegex(@"^[\d\s()+\-*/.^%]+$")] private static partial Regex MathShape();
    [GeneratedRegex(@"[+\-*/^%]")] private static partial Regex MathOp();
    [GeneratedRegex(@"^(who|what|why|how|when|where|is|are|can|should|does|do)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionStart();

    public CommandIntent Interpret(string raw)
    {
        var input = (raw ?? string.Empty).Trim();
        if (input.Length == 0)
            return new CommandIntent(CommandKind.Search, string.Empty);

        // 1. Arithmetic: "47*89", "(3+4)^2"
        if (MathShape().IsMatch(input) && MathOp().IsMatch(input))
        {
            var value = TryEval(input);
            return new CommandIntent(CommandKind.Calculate, input, value);
        }

        // 2. Translation: "translate hello"
        if (input.StartsWith("translate ", StringComparison.OrdinalIgnoreCase))
            return new CommandIntent(CommandKind.Translate, input["translate ".Length..].Trim());

        // 3. URL / domain: single token containing a dot, no spaces
        if (!input.Contains(' ') && input.Contains('.') && LooksLikeHost(input))
            return new CommandIntent(CommandKind.Navigate, Normalize(input));

        // 4. Natural-language question -> AI
        if (input.EndsWith('?') || QuestionStart().IsMatch(input))
            return new CommandIntent(CommandKind.AskAi, input);

        // 5. Fallback: web search
        return new CommandIntent(CommandKind.Search, input);
    }

    private static bool LooksLikeHost(string s)
    {
        var host = s.Contains("://") ? s[(s.IndexOf("://", StringComparison.Ordinal) + 3)..] : s;
        host = host.Split('/')[0];
        var lastDot = host.LastIndexOf('.');
        return lastDot > 0 && lastDot < host.Length - 1;
    }

    private static string Normalize(string s) =>
        s.Contains("://", StringComparison.Ordinal) ? s : "https://" + s;

    private static string? TryEval(string expr)
    {
        try
        {
            var dt = new DataTable();
            var netExpr = expr.Replace("^", string.Empty); // DataTable has no power op; guarded below
            if (expr.Contains('^')) return null;
            var result = dt.Compute(netExpr, string.Empty);
            return Convert.ToDouble(result, CultureInfo.InvariantCulture)
                .ToString("0.######", CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }
}