using ModernFormsNext.WebView.Windows;
using Xunit;

namespace ModernFormsNext.WebView.Windows.Tests;

public sealed class NavigationQueueTests
{
    [Fact]
    public void DeferredStartingRetainsOriginalIdentityAndCoalescesOnlyUnsubmittedCommands()
    {
        var posted = new Queue<Action>(); var started = new List<long>();
        var queue = new NavigationCommandQueue(() => true, posted.Enqueue);
        for (long id = 1; id <= 3; ++id)
        { long captured = id; queue.Submit(new(id, null, "html"), () => started.Add(captured)); }
        Assert.Equal(1, queue.Pending!.Id); Assert.Equal([1L], started);
        queue.Acknowledge(1); posted.Dequeue()();
        Assert.Equal(3, queue.Pending!.Id); Assert.Equal([1L, 3L], started);
    }
    [Fact]
    public void StopRetainsUnacknowledgedIdentityAndDropsUnsubmittedCommand()
    {
        var posted = new Queue<Action>(); int started = 0;
        var queue = new NavigationCommandQueue(() => true, posted.Enqueue);
        queue.Submit(new(1, null, null), () => ++started);
        queue.Submit(new(2, null, null), () => ++started);
        Assert.False(queue.Stop()); Assert.Equal(1, queue.Pending!.Id);
        queue.Acknowledge(1); posted.Dequeue()(); Assert.Equal(1, started); Assert.True(queue.Stop());
    }
    [Fact]
    public void RetiredGenerationCannotDispatchQueuedNativeCommand()
    {
        bool current = true; var posted = new Queue<Action>(); int started = 0;
        var queue = new NavigationCommandQueue(() => current, posted.Enqueue);
        queue.Submit(new(1, null, null), () => ++started);
        queue.Submit(new(2, null, null), () => ++started);
        queue.Acknowledge(1); current = false; posted.Dequeue()(); Assert.Equal(1, started);
    }
    [Fact]
    public void NativeCommandExceptionDoesNotStrandAcknowledgementQueue()
    {
        var queue = new NavigationCommandQueue(() => true, a => a());
        Assert.Throws<InvalidOperationException>(() => queue.Submit(new(1, null, null), () => throw new InvalidOperationException()));
        Assert.Null(queue.Pending);
        queue.Submit(new(2, null, null), () => { }); Assert.Equal(2, queue.Pending!.Id);
    }
    [Fact]
    public void ProviderConstructionDoesNotStartBrowserRuntime()
    { var provider = new WindowsWebViewProvider(); Assert.NotNull(provider); }

    [Fact]
    public void OldAcknowledgementCannotClearNewReentrantCommand()
    {
        var posted = new Queue<Action>();
        var queue = new NavigationCommandQueue(() => true, posted.Enqueue);
        queue.Submit(new(1, null, null), () => { }); queue.Acknowledge(1); posted.Dequeue()();
        queue.Submit(new(2, null, null), () => { });
        queue.Acknowledge(1);
        Assert.Equal(2, queue.Pending!.Id); Assert.Empty(posted);
    }
}
