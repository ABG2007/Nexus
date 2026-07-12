using Nexus.Core.Entities;
using Nexus.UI.Mvvm;

namespace Nexus.UI.ViewModels;

public sealed class TabViewModel : ObservableObject
{
    public Tab Model { get; }
    public TabViewModel(Tab model) => Model = model;

    public Guid Id => Model.Id;
    public string Title { get => Model.Title; set { Model.Title = value; Raise(); } }
    public string? Url { get => Model.Url?.ToString(); }
    public bool IsLive { get => Model.IsLive; set { Model.IsLive = value; Raise(); } }
}

