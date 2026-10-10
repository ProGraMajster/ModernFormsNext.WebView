namespace ModernFormsNext.WebView.Windows;

// WebView2 supplies a native request ID only after a command has been submitted.
// This small acknowledgement queue preserves that command's logical identity.
internal sealed class NavigationCommandQueue(Func<bool> current, Action<Action> post)
{
    private WebViewNavigationRequest? nextRequest;
    private Action? nextCommand;
    internal WebViewNavigationRequest? Pending { get; private set; }
    internal void Submit(WebViewNavigationRequest request, Action command)
    { nextRequest = request; nextCommand = command; Drain(); }
    internal void Acknowledge(long originalId)
    {
        if (Pending?.Id != originalId) return;
        Pending = null; post(Drain);
    }
    internal bool Stop()
    { nextRequest = null; nextCommand = null; return Pending is null; }
    internal void Clear() { nextRequest = Pending = null; nextCommand = null; }
    private void Drain()
    {
        if (!current() || Pending is not null || nextCommand is null) return;
        Pending = nextRequest; var command = nextCommand; nextRequest = null; nextCommand = null;
        try { command(); }
        catch { Pending = null; throw; }
    }
}
