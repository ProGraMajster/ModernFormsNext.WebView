using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace ModernFormsNext.WebView.BlazorProbe;

internal static class ContractValidation
{
    internal static async Task RunAsync()
    {
        using var services = RazorSnapshots.CreateServices();
        string first = await RazorSnapshots.RenderAsync(services, 7);
        string second = await RazorSnapshots.RenderAsync(services, 8);
        Require(first.Contains("id=\"razor-count\">7", StringComparison.Ordinal) && second.Contains("id=\"razor-count\">8", StringComparison.Ordinal), "compiled Razor parameter rendering");
        Require(first != second, "fresh scoped DI identities");
        var counters = services.GetRequiredService<ProbeCounters>();
        Require(counters.ScopesCreated == 2 && counters.ScopesDisposed == 2 && counters.ComponentsDisposed == 2, "Razor component/scope disposal");
        foreach (var policy in new[] { WebViewLocalContentPolicy.Default, WebViewLocalContentPolicy.Application })
        {
            var catalog = new ResourceCatalog(first, policy); Uri? navigated = null;
            await using var manager = new ManagerProbe(services, Dispatcher.CreateDefault(), catalog, uri => navigated = uri);
            manager.Navigate("index.html"); Require(navigated == catalog.Content.GetUri("index.html"), "public manager navigation seam");
            foreach (string path in catalog.Paths) manager.CheckResource(path);
            await manager.AddRootComponentAsync(typeof(ProbeRoot), "#interactive-root", ParameterView.Empty);
            Require(!await manager.TryDispatchAsync(_ => throw new InvalidOperationException("No page should be attached.")), "no unverified page/DI dispatch");
            await manager.RemoveRootComponentAsync("#interactive-root");
            await using (var interopScope = services.CreateAsyncScope())
            {
                var interop = interopScope.ServiceProvider.GetRequiredService<IJSRuntime>();
                try
                {
                    await interop.InvokeAsync<int>("Math.abs", -42);
                    throw new InvalidOperationException("Unexpected interop before a qualified page attachment.");
                }
                catch (InvalidOperationException error) when (!error.Message.StartsWith("Unexpected", StringComparison.Ordinal))
                { Console.WriteLine("PASS public IJSRuntime rejects invocation before page attachment"); }
            }
            Require(!new CatalogFileProvider(catalog).GetFileInfo("../private.txt").Exists, "no filesystem traversal/provider fallback");
            Require(catalog.Content.Policy == policy, "explicit per-catalog policy retained");
        }
        Console.WriteLine("CONTRACT PASS: compiled Razor/scoped DI/disposal/public WebViewManager navigation/assets/root registration; no privileged page attached");
    }
    internal static void Require(bool value, string name) { if (!value) throw new InvalidOperationException(name); }
}
