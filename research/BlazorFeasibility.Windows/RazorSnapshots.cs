using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ModernFormsNext.WebView.BlazorProbe;

internal sealed class ProbeCounters
{
    internal int ScopesCreated, ScopesDisposed, ComponentsDisposed;
}
internal sealed class ProbeScope : IDisposable
{
    internal readonly ProbeCounters Counters;
    internal readonly string Identity = Guid.NewGuid().ToString("N");
    public ProbeScope(ProbeCounters counters) { Counters = counters; Interlocked.Increment(ref counters.ScopesCreated); }
    public void Dispose() => Interlocked.Increment(ref Counters.ScopesDisposed);
}
internal static class RazorSnapshots
{
    internal static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddBlazorWebView();
        services.AddSingleton<ProbeCounters>(); services.AddScoped<ProbeScope>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    internal static async Task<string> RenderAsync(ServiceProvider services, int count)
    {
        // HtmlRenderer owns the standard Razor rendering synchronization context. No
        // application UI thread, native host or window loop is created by this renderer.
        // Native callers enter/leave on MFN's existing UI dispatcher; components use data only.
        await using var scope = services.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ProbeRoot.Count)] = count });
            var output = await renderer.RenderComponentAsync<ProbeRoot>(parameters);
            return output.ToHtmlString();
        });
    }
}
