namespace ModernFormsNext.WebView.Sample;

internal static class NavigationRuntimeValidation
{
    internal static async Task RunAsync(WebView browser, Uri first, Uri second, Action<string> report)
    {
        await RuntimeValidation.NavigateAsync(browser, () => browser.Navigate(first));
        await RuntimeValidation.NavigateAsync(browser, () => browser.Navigate(second));
        RuntimeValidation.Require(browser.Source == second && browser.CanGoBack, "second URI and native history");
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.GoBack(), "back available"));
        RuntimeValidation.Require(browser.Source == first && browser.CanGoForward, "back source and forward availability");
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.GoForward(), "forward available"));
        RuntimeValidation.Require(browser.Source == second, "forward source");
        report("PASS URI navigation + back/forward + active native history");
    }

    internal static async Task RunPostAsync(WebView browser, Uri destination, Action<string> report)
    {
        string html = "<form method='post' action='" + System.Net.WebUtility.HtmlEncode(destination.AbsoluteUri) + "'></form>";
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml(html));
        Task<string>? submission = null;
        await RuntimeValidation.NavigateAsync(browser, () => submission = browser.ExecuteScriptAsync("document.forms[0].submit();null"));
        await ObserveSubmissionAsync(submission!);
        RuntimeValidation.Require(browser.Source == destination && !browser.IsLoading, "POST has a new URI identity and settles");
        report("PASS native POST fallback from raw HTML + source/loading identity");

        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml(html));
        bool redirectedByObserver = false;
        EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) =>
        {
            if (!redirectedByObserver && e.Uri == destination)
            { redirectedByObserver = true; browser.NavigateToHtml("<p id='reentrant'>newer document</p>"); }
        };
        browser.NavigationStarting += starting;
        try
        {
            await RuntimeValidation.NavigateAsync(browser, () => submission = browser.ExecuteScriptAsync("document.forms[0].submit();null"));
            await ObserveSubmissionAsync(submission!);
            RuntimeValidation.Require(redirectedByObserver && browser.Source is null && !browser.IsLoading,
                "POST observer replacement completes instead of stopping the newer request");
            RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!document.getElementById('reentrant')") == "true", "newer DOM active");
        }
        finally { browser.NavigationStarting -= starting; }
        report("PASS native POST Starting reentrancy preserves newer HTML");
    }

    private static async Task ObserveSubmissionAsync(Task<string> submission)
    {
        try { await submission; }
        catch (OperationCanceledException) { /* Document replacement revokes the submitted script's result. */ }
    }
}
