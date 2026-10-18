using System.Drawing;

namespace ModernFormsNext.WebView.Sample;

internal sealed class SampleForm : Form
{
    internal readonly WebView Browser = new() { Dock = DockStyle.Fill, IsJavaScriptEnabled = true };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 52, Text = "Experimental WebView baseline" };
    private readonly TextBox address = new() { Dock = DockStyle.Top, Height = 36, Text = "https://example.com/" };

    internal SampleForm()
    {
        Text = "ModernFormsNext.WebView — development";
        Size = new Size(900, 650);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 148 };
        AddButton(toolbar, "Navigate", () => Browser.Navigate(new Uri(address.Text, UriKind.Absolute)));
        AddButton(toolbar, "Back", () => Browser.GoBack()); AddButton(toolbar, "Forward", () => Browser.GoForward());
        AddButton(toolbar, "Reload", () => Browser.Reload()); AddButton(toolbar, "Stop", Browser.Stop);
        AddButton(toolbar, "Offline HTML", () => Browser.NavigateToHtml(TestContent.Html));
        AddButton(toolbar, "Local resources", () => Browser.NavigateToLocalContent(LocalContentSample.Create()));
        AddButton(toolbar, "Local fetch opt-in", () => Browser.NavigateToLocalContent(ApplicationContentSample.Create()));
        AddAsyncButton(toolbar, "JavaScript", async () => status.Text = await Browser.ExecuteScriptAsync("testResult()"));
        AddAsyncButton(toolbar, "Message to page", () => Browser.PostMessageAsync("app message"));
        AddAsyncButton(toolbar, "MFN dialog", async () =>
        {
            using var dialog = new Form { Text = "MFN modal above WebView", Size = new(320, 220) };
            dialog.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Framework modal; browser owner input is disabled." });
            var close = dialog.Controls.Add(new Button { Dock = DockStyle.Bottom, Height = 48, Text = "Close dialog" });
            close.Click += (_, _) => dialog.Close();
            await dialog.ShowDialog(this);
        });
        Browser.NavigationStarting += (_, e) => e.Cancel = e.Uri?.Host == "blocked.example";
        Browser.NavigationCompleted += (_, e) => status.Text = "Completed: " + (e.Uri?.AbsoluteUri ?? "raw HTML");
        Browser.NavigationFailed += (_, e) => status.Text = "Navigation failure: " + e.Category;
        Browser.InitializationFailed += (_, e) => status.Text = "Initialization failure: " + e.Category;
        Browser.StateChanged += (_, _) => status.Text = $"{Browser.InitializationState}; loading={Browser.IsLoading}; back={Browser.CanGoBack}; forward={Browser.CanGoForward}";
        Browser.WebMessageReceived += (_, e) => status.Text = "Untrusted page string: " + e.Message;
        Controls.Add(Browser); Controls.Add(status); Controls.Add(toolbar); Controls.Add(address);
        Browser.NavigateToHtml(TestContent.Html);
    }

    private void AddButton(FlowLayoutPanel panel, string text, Action action)
    {
        var button = panel.Controls.Add(new Button { Text = text, Width = 125, Height = 36 });
        button.Click += (_, _) => { try { action(); } catch (Exception error) { status.Text = error.Message; } };
    }
    private void AddAsyncButton(FlowLayoutPanel panel, string text, Func<Task> action)
        => AddButton(panel, text, async () => { try { await action(); } catch (Exception error) { status.Text = error.Message; } });
}

internal static class TestContent
{
    internal static string Html
    {
        get
        {
            var assembly = typeof(TestContent).Assembly;
            string resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("test-page.html", StringComparison.Ordinal));
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            return reader.ReadToEnd();
        }
    }
}
