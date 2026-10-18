using UiDispatcher = ModernFormsNext.WindowKit.Threading.Dispatcher;

namespace ModernFormsNext.WebView.BlazorProbe;

// Adapts only the public ASP.NET dispatcher contract to MFN's canonical UI queue.
// It owns no thread, timer, synchronization context or application message pump.
internal sealed class MfnComponentDispatcher : Microsoft.AspNetCore.Components.Dispatcher
{
    public override bool CheckAccess() => UiDispatcher.UIThread.CheckAccess();
    public override async Task InvokeAsync(Action workItem) => await UiDispatcher.UIThread.InvokeAsync(workItem);
    public override async Task<T> InvokeAsync<T>(Func<T> workItem) => await UiDispatcher.UIThread.InvokeAsync(workItem);
    public override Task InvokeAsync(Func<Task> workItem) => UiDispatcher.UIThread.InvokeAsync(workItem);
    public override Task<T> InvokeAsync<T>(Func<Task<T>> workItem) => UiDispatcher.UIThread.InvokeAsync(workItem);
}
