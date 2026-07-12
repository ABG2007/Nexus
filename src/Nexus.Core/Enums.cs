namespace Nexus.Core;

/// <summary>What the command orb decided the user's input means.</summary>
public enum CommandKind
{
    Search,
    Navigate,
    Calculate,
    Translate,
    AskAi
}

public enum DownloadState
{
    Queued,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>Lifecycle of a tab; used for the sleep/priority scheduler.</summary>
public enum TabState
{
    Active,
    Background,
    Sleeping
}

public enum AiCapability
{
    Summarize,
    Translate,
    Rewrite,
    GenerateCode,
    Answer,
    ExtractKeyPoints
}