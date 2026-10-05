namespace HermesStack.Domain.Security;

public sealed record SecretReference(string ProjectId, string Name);

public sealed record SecretValue(string Value);

public sealed record SecretPolicy(
    string ProjectId,
    string Name,
    IReadOnlyList<string> Agents);
