using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application;
using Nexus.Infrastructure;
using Nexus.UI.ViewModels;
using Nexus.UI.Views;

namespace Nexus.UI;

public partial class App : System.Windows.Application
{
    private IServiceProvider _services = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nexus");
        Directory.CreateDirectory(dataDir);

        var storage = new NexusStorageOptions(
            HistoryDbPath: Path.Combine(dataDir, "nexus.db"),
            BookmarksPath: Path.Combine(dataDir, "bookmarks.json"),
            SettingsPath:  Path.Combine(dataDir, "settings.json"));

        var ai = new NexusAiOptions(
            OpenAiApiKey: Environment.GetEnvironmentVariable("NEXUS_OPENAI_KEY"),
            DefaultProvider: "ollama");

        var collection = new ServiceCollection();
        collection.AddNexusApplication();
        collection.AddNexusInfrastructure(storage, ai);
        collection.AddSingleton<CommandOrbViewModel>();
        collection.AddSingleton<ShellViewModel>();
        collection.AddSingleton<ShellWindow>();
        _services = collection.BuildServiceProvider();

        var shell = _services.GetRequiredService<ShellWindow>();
        shell.DataContext = _services.GetRequiredService<ShellViewModel>();
        shell.Show();
    }
}