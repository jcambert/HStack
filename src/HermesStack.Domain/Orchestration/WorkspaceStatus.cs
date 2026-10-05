namespace HermesStack.Domain.Orchestration;

public enum WorkspaceState
{
    Unknown,
    Stopped,
    Running
}

public sealed record WorkspaceStatus(WorkspaceState State, string? Details = null);
