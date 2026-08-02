namespace ImageViewer.Services;

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
    string? RegisteredExecutablePath = null)
{
    public bool IsRegistered => State is
        WindowsIntegrationState.RegisteredHere or
        WindowsIntegrationState.RegisteredElsewhere;
}
