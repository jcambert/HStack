namespace HermesStack.Application.Abstractions;

public interface IAgentHarnessRegistry
{
    IReadOnlyCollection<IAgentHarness> All { get; }
    IAgentHarness GetRequired(string id);
}
