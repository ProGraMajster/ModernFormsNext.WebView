using System.Runtime.InteropServices;
using ModernFormsNext.WebView.Sample;

// Native keyboard injection is a Windows sample validation fixture, never framework or
// browser adapter behavior. Every SendInput batch checks the foreground owner.
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
            const string national = "Zażółć gęślą jaźń ĄĘŁŃÓŚŹŻ";
            await browser.ExecuteScriptAsync("document.getElementById('editor').value='';null");
            foreach (char character in national)
                Inject([Unicode(character, false), Unicode(character, true)]);
            await BaselineFloorRuntimeValidation.WaitAsync(browser, "document.getElementById('editor').value===" + System.Text.Json.JsonSerializer.Serialize(national));
            Console.WriteLine("PASS native Unicode SendInput Polish characters; full IME composition NOT EXECUTED");
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
        // A deterministic two-stop fixture uses the existing canonical tab traversal.
        // Restore every sample control's original TabStop and index afterwards.
        using var preceding = new ModernFormsNext.Button { Text = "Native keyboard return", Bounds = new(4, 4, 160, 32), TabIndex = 0 };
        var stops = new List<(ModernFormsNext.Control Control, bool Stop)>();
        void Capture(IList<ModernFormsNext.Control> controls)
        {
            foreach (ModernFormsNext.Control child in controls)
            { stops.Add((child, child.TabStop)); child.TabStop = ReferenceEquals(child, browser); Capture(child.Controls); }
        }
        int oldIndex = browser.TabIndex;
        Capture(form.Controls); form.Controls.Add(preceding); browser.TabIndex = 1;
        try
        {
            preceding.Select();
            RuntimeValidation.Require(preceding.Selected && !browser.Selected, "real MFN control selected before native Tab");
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler got = (_, _) => entered.TrySetResult(); browser.GotFocus += got;
            EventHandler<ModernFormsNext.KeyEventArgs> key = (_, e) =>
            { if (e.KeyCode == ModernFormsNext.Keys.Tab) Console.WriteLine("KEYBOARD actual MFN Tab KeyDown handled=" + e.Handled); };
            preceding.KeyDown += key;
            try { PressTab(false); await entered.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch
            {
                Console.WriteLine("KEYBOARD failed return MFN selected=" + preceding.Selected + " browser selected=" + browser.Selected +
                    " nativeFocus=" + GetFocus() + " placement=" + browser.HostingDiagnostics.State + " browserTab=" + browser.TabStop + ":" + browser.TabIndex);
                throw;
            }
            finally { browser.GotFocus -= got; preceding.KeyDown -= key; }
            RuntimeValidation.Require(browser.Selected, "native MFN-to-HTML boundary selects canonical browser");
        }
        finally
        {
            form.Controls.Remove(preceding); browser.TabIndex = oldIndex;
            foreach (var item in stops) item.Control.TabStop = item.Stop;
        }
        Console.WriteLine("PASS native Tab + Shift+Tab + HTML-to-MFN and MFN-to-HTML focus boundaries");
    }

    private static void PressTab(bool shift)
    {
        Input[] keys = shift ? [Key(0x10, false), Key(0x09, false), Key(0x09, true), Key(0x10, true)]
            : [Key(0x09, false), Key(0x09, true)];
        Inject(keys);
    }
    internal static Task PopupEnterAsync()
    {
        SetForegroundWindow(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle);
        Inject([Key(0x0D, false), Key(0x0D, true)]); // Existing foreground ownership guard applies.
        return Task.CompletedTask;
    }
    private static void Inject(Input[] keys)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint owner);
        if (owner != Environment.ProcessId) throw new InvalidOperationException("Keyboard validation requires the sample's foreground window.");
        if (SendInput((uint)keys.Length, keys, Marshal.SizeOf<Input>()) != keys.Length)
            throw new InvalidOperationException("Native keyboard injection failed.");
    }
    private static Input Unicode(char value, bool up) => new() { Type = 1, Data = new() { Keyboard = new() { Scan = value, Flags = 4u | (up ? 2u : 0u) } } };
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
    [DllImport("user32.dll")] private static extern nint GetFocus();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
}
