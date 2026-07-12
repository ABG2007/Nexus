using Nexus.Core;
using Nexus.Core.Abstractions;
using Nexus.Core.Entities;
using Nexus.Core.Services;
namespace Nexus.Application.Services;
public sealed record NavigationOutcome(CommandKind Kind, Uri? Target, string? InlineResult);
public sealed class NavigationService
{
 private readonly CommandInterpreter _interpreter;
 private readonly IHistoryRepository _history;
 private readonly ISettingsStore _settings;

 // VAULT : quand true, la navigation ne laisse aucune trace dans l'historique.
 public static bool PrivateMode { get; set; }

 public NavigationService(CommandInterpreter interpreter, IHistoryRepository history, ISettingsStore settings)
 {
 _interpreter = interpreter;
 _history = history;
 _settings = settings;
 }
 public async Task<NavigationOutcome> ExecuteAsync(string rawInput, CancellationToken ct = default)
 {
 var intent = _interpreter.Interpret(rawInput);
 return intent.Kind switch
 {
 CommandKind.Navigate => await NavigateAsync(new Uri(intent.Payload), rawInput, ct),
 CommandKind.Search => new NavigationOutcome(CommandKind.Search, BuildSearch(intent.Payload), null),
 CommandKind.Calculate => new NavigationOutcome(CommandKind.Calculate, null, intent.Result ?? ""),
 CommandKind.Translate => new NavigationOutcome(CommandKind.Translate, null, intent.Payload),
 CommandKind.AskAi => new NavigationOutcome(CommandKind.AskAi, null, intent.Payload),
 _ => new NavigationOutcome(CommandKind.Search, BuildSearch(rawInput), null)
 };
 }
 private async Task<NavigationOutcome> NavigateAsync(Uri url, string title, CancellationToken ct)
 {
 // Mode Vault : on ne journalise pas la visite.
 if (!PrivateMode)
 await _history.AddAsync(new HistoryEntry { Url = url, Title = title }, ct);
 return new NavigationOutcome(CommandKind.Navigate, url, null);
 }
 private Uri BuildSearch(string query)
 {
 var engine = _settings.Get("search.engine", "https://duckduckgo.com/?q=");
 return new Uri(engine + Uri.EscapeDataString(query));
 }
}