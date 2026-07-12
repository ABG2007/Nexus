using Nexus.Core;
using Nexus.Core.Entities;

namespace Nexus.Application.Services;

public sealed class TabManager
{
    private readonly List<Tab> _tabs = new();
    private readonly TimeSpan _idleThreshold;

    public TabManager(TimeSpan? idleThreshold = null)
        => _idleThreshold = idleThreshold ?? TimeSpan.FromMinutes(30);

    public IReadOnlyList<Tab> Tabs => _tabs;
    public Tab? Active { get; private set; }

    public event Action<Tab>? TabOpened;
    public event Action<Tab>? TabClosed;
    public event Action<Tab>? ActiveChanged;

    public Tab Open(Uri? url = null, Guid? spaceId = null)
    {
        var tab = new Tab { Url = url, SpaceId = spaceId ?? Guid.Empty };
        _tabs.Add(tab);
        TabOpened?.Invoke(tab);
        Activate(tab);
        return tab;
    }

    public void Close(Guid id)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == id);
        if (tab is null) return;
        _tabs.Remove(tab);
        TabClosed?.Invoke(tab);
        if (Active?.Id == id)
            Activate(_tabs.LastOrDefault());
    }

    public void Activate(Tab? tab)
    {
        if (Active is not null && Active != tab)
            Active.State = TabState.Background;
        Active = tab;
        tab?.Touch();
        if (tab is not null) ActiveChanged?.Invoke(tab);
    }

    /// <summary>Called by a periodic timer. Sleeps stale, low-priority background tabs.</summary>
    public IReadOnlyList<Tab> RunSleepScheduler(DateTimeOffset now)
    {
        var slept = new List<Tab>();
        foreach (var tab in _tabs)
        {
            if (tab == Active || tab.State == TabState.Sleeping) continue;
            var idleFor = now - tab.LastAccessed;
            if (idleFor > _idleThreshold && tab.Priority <= 0)
            {
                tab.State = TabState.Sleeping;
                slept.Add(tab);
            }
        }
        return slept;
    }
}