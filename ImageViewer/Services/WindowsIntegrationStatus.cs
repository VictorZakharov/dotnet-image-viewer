namespace ImageViewer.Services;

[System.Flags]
public enum MediaAssociationGroups
{
    None = 0,
    Images = 1,
    Videos = 2,
    All = Images | Videos
}

public enum WindowsIntegrationState
{
    Unsupported,
    NotRegistered,
    RegisteredHere,
    RegisteredElsewhere,
    NeedsRepair
}

public sealed record WindowsIntegrationStatus(
    WindowsIntegrationState State,
    string? RegisteredExecutablePath = null,
    MediaAssociationGroups RegisteredGroups = MediaAssociationGroups.None)
{
    public bool IsRegistered => State is
        WindowsIntegrationState.RegisteredHere or
        WindowsIntegrationState.RegisteredElsewhere;
}
