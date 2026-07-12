using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Core.Abstractions;
using Nexus.Infrastructure.Ai;
using Nexus.Infrastructure.Persistence;

namespace Nexus.Infrastructure;

public sealed record NexusStorageOptions(string HistoryDbPath, string BookmarksPath, string SettingsPath);
public sealed record NexusAiOptions(string? OpenAiApiKey, string DefaultProvider = "ollama");

public static class DependencyInjection
{
    // Cle de reglage sous laquelle la cle API OpenAI est persistee (settings.json).
    public const string OpenAiApiKeySetting = "ai.openai.apikey";

    public static IServiceCollection AddNexusInfrastructure(
        this IServiceCollection services,
        NexusStorageOptions storage,
        NexusAiOptions ai)
    {
        services.AddSingleton<IHistoryRepository>(_ => new SqliteHistoryRepository(storage.HistoryDbPath));
        services.AddSingleton<IBookmarkRepository>(_ => new JsonBookmarkRepository(storage.BookmarksPath));
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(storage.SettingsPath));

        // Streaming local : pas de timeout global, l'annulation passe par le CancellationToken.
        services.AddHttpClient<OllamaProvider>(c =>
        {
            c.BaseAddress = new Uri("http://localhost:11434");
            c.Timeout = Timeout.InfiniteTimeSpan;
        });

        // OpenAI est TOUJOURS enregistre (le client nomme est configure meme sans cle),
        // pour apparaitre dans la liste des fournisseurs. La cle est resolue a chaud.
        services.AddHttpClient(nameof(OpenAiProvider), c =>
        {
            c.BaseAddress = new Uri("https://api.openai.com");
            c.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddSingleton<IAiProviderRegistry>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var settings = sp.GetRequiredService<ISettingsStore>();

            var providers = new List<IAiProvider>
            {
                sp.GetRequiredService<OllamaProvider>(),
                // Resolveur de cle : settings.json d'abord, sinon la cle passee aux options
                // au demarrage (retro-compatible). Lu a CHAQUE requete OpenAI.
                new OpenAiProvider(
                    factory.CreateClient(nameof(OpenAiProvider)),
                    () =>
                    {
                        var fromSettings = settings.Get<string?>(OpenAiApiKeySetting, null);
                        return string.IsNullOrWhiteSpace(fromSettings) ? ai.OpenAiApiKey : fromSettings;
                    })
            };

            return new AiProviderRegistry(providers, ai.DefaultProvider);
        });

        return services;
    }
}
