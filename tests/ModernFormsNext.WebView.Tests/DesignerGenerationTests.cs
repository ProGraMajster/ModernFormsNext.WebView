using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.Designing;

namespace ModernFormsNext.WebView.Tests;

public sealed class DesignerGenerationTests
{
    [Fact]
    public void PrimitiveWebViewSettingsRoundTripAndGenerateThroughExistingDesignerContracts()
    {
        var document = new DesignDocument
        { Namespace = "Consumer", ClassName = "BrowserForm", FormName = "BrowserForm", Size = new(800, 600) };
        var node = new DesignControlNode
        { Name = "browser", TypeName = typeof(WebView).FullName!, Bounds = new(10, 10, 600, 400) };
        node.Properties[nameof(WebView.SourceAddress)] = DesignPropertyValue.FromString("https://design.test/page");
        node.Properties[nameof(WebView.IsJavaScriptEnabled)] = DesignPropertyValue.FromBoolean(true);
        node.Properties[nameof(WebView.UserAgent)] = DesignPropertyValue.FromString("Consumer agent");
        document.Controls.Add(node);
        var serializer = new DesignDocumentSerializer();
        var restored = serializer.Deserialize(serializer.Serialize(document));
        var generated = new CSharpDesignerGenerator().Generate(restored);
        Assert.True(generated.Succeeded);
        Assert.Contains("new ModernFormsNext.WebView.WebView()", generated.Code);
        Assert.Contains(".SourceAddress = \"https://design.test/page\";", generated.Code);
        Assert.Contains(".IsJavaScriptEnabled = true;", generated.Code);
        Assert.Contains(".UserAgent = \"Consumer agent\";", generated.Code);
        Assert.DoesNotContain("PeerFactory =", generated.Code);
        Assert.DoesNotContain("InitializationState =", generated.Code);
    }
}
