using HermesStack.Application.Abstractions;
using HermesStack.Application.Network;
using HermesStack.Domain.Network;
using Spectre.Console;

namespace HermesStack.Cli;

internal sealed class ProxyCliService(IProxyConfigurationStore store)
{
    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] == "show")
        {
            return await ShowAsync();
        }

        if (args[0] == "disable")
        {
            await store.SaveAsync(new ProxyConfiguration());
            AnsiConsole.MarkupLine("[green]✓[/] Proxy disabled.");
            return 0;
        }

        if (args[0] != "set")
        {
            throw new ArgumentException(
                "Usage: hstack proxy show | set [--http <uri>] [--https <uri>] [--no-proxy <csv>] | disable");
        }

        var http = GetOption(args, "--http");
        var https = GetOption(args, "--https");
        var noProxy = (GetOption(args, "--no-proxy") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var configuration = ProxyConfigurationPolicy.ValidateAndNormalize(
            new ProxyConfiguration(true, http, https, noProxy));
        await store.SaveAsync(configuration);

        AnsiConsole.MarkupLine("[green]✓[/] Proxy configuration saved.");
        return await ShowAsync();
    }

    private async Task<int> ShowAsync()
    {
        var configuration = await store.GetAsync();
        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Enabled", configuration.Enabled ? "yes" : "no");
        table.AddRow("HTTP", Markup.Escape(configuration.Http ?? "-"));
        table.AddRow("HTTPS", Markup.Escape(configuration.Https ?? "-"));
        table.AddRow(
            "NO_PROXY",
            Markup.Escape(string.Join(",", configuration.EffectiveNoProxy)));
        AnsiConsole.Write(table);
        return 0;
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            ? args[index + 1]
            : null;
    }
}
