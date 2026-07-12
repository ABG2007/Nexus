using System.Collections.ObjectModel;
using Nexus.Application.Services;
using Nexus.Core;
using Nexus.UI.Mvvm;

namespace Nexus.UI.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly TabManager _tabs;
    private bool _constellationOpen;

    public ShellViewModel(TabManager tabs, CommandOrbViewModel orb)
    {
        _tabs = tabs;
        Orb = orb;

        OpenConstellationCommand = new RelayCommand(_ => ConstellationOpen = true);
        NewTabCommand = new RelayCommand(_ => _tabs.Open());
        CloseTabCommand = new RelayCommand(p => { if (p is Guid id) { _tabs.Close(id); Sync(); } });

        _tabs.TabOpened += _ => Sync();
        _tabs.TabClosed += _ => Sync();
        orb.Navigated += OnNavigated;
        Sync();
    }

    public CommandOrbViewModel Orb { get; }
    public ObservableCollection<TabViewModel> OpenTabs { get; } = new();

    public RelayCommand OpenConstellationCommand { get; }
    public RelayCommand NewTabCommand { get; }
    public RelayCommand CloseTabCommand { get; }

    public bool ConstellationOpen { get => _constellationOpen; set => SetProperty(ref _constellationOpen, value); }

    /// <summary>Raised when a real page should load in the WebView2 host.</summary>
    public event Action<Uri>? NavigateRequested;

    private void OnNavigated(NavigationOutcome outcome)
    {
        // La gestion des onglets vit désormais uniquement dans ShellWindow.
        // Ici, on se contente de demander la navigation.
        if (outcome.Kind is CommandKind.Navigate or CommandKind.Search && outcome.Target is not null)
            NavigateRequested?.Invoke(outcome.Target);
    }

    private void Sync()
    {
        OpenTabs.Clear();
        foreach (var t in _tabs.Tabs)
            OpenTabs.Add(new TabViewModel(t));
    }
}