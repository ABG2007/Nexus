using Nexus.Application.Services;
using Nexus.Core;
using Nexus.Core.Services;
using Nexus.UI.Mvvm;

namespace Nexus.UI.ViewModels;

public sealed class CommandOrbViewModel : ObservableObject
{
    private readonly CommandInterpreter _interpreter;
    private readonly NavigationService _navigation;

    private string _input = string.Empty;
    private string _modeLabel = "Search";
    private string? _inlineResult;
    private bool _isOpen;

    public CommandOrbViewModel(CommandInterpreter interpreter, NavigationService navigation)
    {
        _interpreter = interpreter;
        _navigation = navigation;
        SubmitCommand = new RelayCommand(async _ => await SubmitAsync());
    }

    public RelayCommand SubmitCommand { get; }
    public event Action<NavigationOutcome>? Navigated;

    public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }
    public string ModeLabel { get => _modeLabel; private set => SetProperty(ref _modeLabel, value); }
    public string? InlineResult { get => _inlineResult; private set => SetProperty(ref _inlineResult, value); }

    public string Input
    {
        get => _input;
        set { if (SetProperty(ref _input, value)) Reclassify(); }
    }

    private void Reclassify()
    {
        var intent = _interpreter.Interpret(_input);
        ModeLabel = intent.Kind switch
        {
            CommandKind.Navigate  => "Go to site",
            CommandKind.Calculate => "Calculate",
            CommandKind.Translate => "Translate",
            CommandKind.AskAi     => "Ask Nexus",
            _                     => "Search"
        };
        InlineResult = intent.Kind == CommandKind.Calculate ? intent.Result : null;
    }

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_input)) return;
        var outcome = await _navigation.ExecuteAsync(_input);
        Navigated?.Invoke(outcome);
        IsOpen = false;
        Input = string.Empty;
    }
}