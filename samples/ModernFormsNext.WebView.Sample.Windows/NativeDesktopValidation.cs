using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.WebView.Sample;

// Reads real monitor/DPI state and moves only this test Form. No display settings,
// keyboard layouts, global scaling or another application's window are changed.
internal static class NativeDesktopValidation
{
    internal static async Task RunAsync(SampleForm form)
    {
        var monitors = new List<Monitor>();
        MonitorCallback callback = (nint handle, nint dc, ref Rect bounds, nint data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(handle, ref info)) throw new InvalidOperationException("Cannot read native monitor bounds");
            int result = GetDpiForMonitor(handle, 0, out uint x, out uint y);
            if (result != 0) Marshal.ThrowExceptionForHR(result);
            monitors.Add(new Monitor(info.Work, x, y)); return true;
        };
        if (!EnumDisplayMonitors(0, 0, callback, 0) || monitors.Count == 0) throw new InvalidOperationException("No interactive monitors");
        nint window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        RuntimeValidation.Require(window != 0, "test Form owns a native window");
        var original = form.Bounds;
        long generation = form.Browser.GetDiagnostics().PeerGeneration;
        try
        {
            foreach (var monitor in monitors)
            {
                Console.WriteLine($"MONITOR work={monitor.Work.Left},{monitor.Work.Top},{monitor.Work.Right},{monitor.Work.Bottom} effectiveDpi={monitor.X},{monitor.Y}");
                form.Location = new(monitor.Work.Left + 24, monitor.Work.Top + 24);
                await Task.Delay(250);
                if (!GetWindowRect(window, out var actual)) throw new InvalidOperationException("Cannot read moved native window");
                RuntimeValidation.Require(actual.Left >= monitor.Work.Left && actual.Left < monitor.Work.Right &&
                    actual.Top >= monitor.Work.Top && actual.Top < monitor.Work.Bottom, "real native window moved into selected monitor");
                RuntimeValidation.Require(form.Browser.HostingDiagnostics.Placement.Visible &&
                    await form.Browser.ExecuteScriptAsync("1+1") == "2", "native placement and document survive monitor move");
                Console.WriteLine("PASS real monitor move windowDpi=" + GetDpiForWindow(window) + "; no additional scales simulated");
                Console.WriteLine("DESKTOP peer after monitor move=" + form.Browser.GetDiagnostics().PeerGeneration);
            }
            form.WindowState = FormWindowState.Maximized; await Task.Delay(150);
            RuntimeValidation.Require(form.WindowState == FormWindowState.Maximized && IsZoomed(window), "real native maximize");
            form.WindowState = FormWindowState.Minimized; await Task.Delay(150);
            RuntimeValidation.Require(form.WindowState == FormWindowState.Minimized && IsIconic(window), "real native minimize");
            form.WindowState = FormWindowState.Normal; form.Activate(); await Task.Delay(150);
            Console.WriteLine("DESKTOP restored state=" + form.WindowState + " iconic=" + IsIconic(window) + " zoomed=" + IsZoomed(window) +
                " peer=" + generation + "->" + form.Browser.GetDiagnostics().PeerGeneration);
            RuntimeValidation.Require(!IsIconic(window) && !IsZoomed(window), "Normal returns a maximized/minimized native window to normal state");
            RuntimeValidation.Require(form.Browser.GetDiagnostics().PeerGeneration == generation &&
                await form.Browser.ExecuteScriptAsync("1+1") == "2", "restore preserves real browser peer/document");
            Console.WriteLine("PASS native maximize/minimize/restore/activation request; keyboard fixture separately guards actual foreground");
            var layouts = new nint[32]; int count = GetKeyboardLayoutList(layouts.Length, layouts);
            Console.WriteLine("KEYBOARD installed HKL=" + string.Join(",", layouts.Take(Math.Min(count, layouts.Length)).Select(x => x.ToString("X"))) +
                "; full human IME composition NOT EXECUTED");
        }
        finally { form.WindowState = FormWindowState.Normal; form.Bounds = original; }
    }
    private readonly record struct Monitor(Rect Work, uint X, uint Y);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { internal int Size; internal Rect Bounds, Work; internal uint Flags; }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref Rect bounds, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor, int kind, out uint x, out uint y);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect bounds);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern int GetKeyboardLayoutList(int count, [Out] nint[] layouts);
}
