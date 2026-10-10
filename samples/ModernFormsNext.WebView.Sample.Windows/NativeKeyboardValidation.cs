using System.Runtime.InteropServices;
using ModernFormsNext.WebView.Sample;

// Native keyboard injection is a Windows sample validation fixture, never framework or
// browser adapter behavior. It sends only Tab/Shift+Tab and checks the foreground owner.
internal static class NativeKeyboardValidation
{
    internal static async Task RunAsync(SampleForm form)
    {
        var browser = form.Browser;
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml(TestContent.Html));
        form.Activate();
        // Native input needs an explicit foreground test window. Activation alone may
        // leave Codex foreground when this sample is launched by a background shell.
        SetForegroundWindow(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle);
        browser.Select();
        TaskCompletionSource? focus = null;
        string expected = "";
        EventHandler<ModernFormsNext.WebView.WebViewMessageReceivedEventArgs> handler = (_, e) =>
        { if (e.Message == "native-focus:" + expected) focus?.TrySetResult(); };
        browser.WebMessageReceived += handler;
        try
        {
            await browser.ExecuteScriptAsync("document.addEventListener('focusin',function(e){modernFormsWebView.postMessage('native-focus:'+e.target.id);});document.getElementById('editor').focus();null");
            expected = "page-send"; focus = new(TaskCreationOptions.RunContinuationsAsynchronously);
            PressTab(false); await focus.Task.WaitAsync(TimeSpan.FromSeconds(15));
            RuntimeValidation.Require(await browser.ExecuteScriptAsync("document.activeElement.id") == "\"page-send\"", "native Tab within HTML");
            expected = "editor"; focus = new(TaskCreationOptions.RunContinuationsAsynchronously);
            PressTab(true); await focus.Task.WaitAsync(TimeSpan.FromSeconds(15));
            RuntimeValidation.Require(await browser.ExecuteScriptAsync("document.activeElement.id") == "\"editor\"", "native Shift+Tab within HTML");
        }
        finally { browser.WebMessageReceived -= handler; }

        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<input id='only' aria-label='only native focus target'>"));
        browser.Select(); await browser.ExecuteScriptAsync("document.getElementById('only').focus();null");
        var left = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler lost = (_, _) => left.TrySetResult();
        browser.LostFocus += lost;
        try { PressTab(false); await left.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { browser.LostFocus -= lost; }
        RuntimeValidation.Require(!browser.Selected, "native Tab boundary commits canonical MFN focus");
        Console.WriteLine("PASS native Tab + Shift+Tab + HTML-to-MFN focus boundary");
    }

    private static void PressTab(bool shift)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint owner);
        if (owner != Environment.ProcessId) throw new InvalidOperationException("Keyboard validation requires the sample's foreground window.");
        Input[] keys = shift ? [Key(0x10, false), Key(0x09, false), Key(0x09, true), Key(0x10, true)]
            : [Key(0x09, false), Key(0x09, true)];
        if (SendInput((uint)keys.Length, keys, Marshal.SizeOf<Input>()) != keys.Length)
            throw new InvalidOperationException("Native keyboard injection failed.");
    }
    private static Input Key(ushort value, bool up) => new() { Type = 1, Data = new() { Keyboard = new() { VirtualKey = value, Flags = up ? 2u : 0u } } };
    [StructLayout(LayoutKind.Sequential)] private struct Input { internal uint Type; internal InputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData
    { [FieldOffset(0)] internal KeyboardInput Keyboard; [FieldOffset(0)] internal MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput
    { internal ushort VirtualKey, Scan; internal uint Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
}
