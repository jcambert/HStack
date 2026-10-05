namespace HermesStack.Application.Abstractions;

public interface ISecretRedactor
{
    string Redact(string input, IEnumerable<string>? knownSecrets = null);
}
