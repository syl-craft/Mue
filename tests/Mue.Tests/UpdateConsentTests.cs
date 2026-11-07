using System.Globalization;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

namespace Mue.Tests;

public class UpdateConsentTests
{
    [Theory]
    [InlineData(UpdateShutdownBehavior.Refuse, true)]
    [InlineData(UpdateShutdownBehavior.Kill, false)]
    [InlineData(UpdateShutdownBehavior.AskToQuit, false)]
    public void FinalScanOnlyToleratesAcceptedIdentities(UpdateShutdownBehavior behavior, bool blocked)
    {
        HashSet<(int Pid, long Started)> ignored = [(42, 100)];
        Assert.Equal(blocked, UpdateBlockers.HasUnacceptedBlockers([new() { Pid = 42, Started = 100 }], behavior, ignored));
        Assert.Equal(blocked, UpdateBlockers.HasUnacceptedBlockers([new() { Pid = 42, Started = 100, Unavailable = true }], behavior, ignored));
        Assert.True(UpdateBlockers.HasUnacceptedBlockers([new() { Pid = 42, Started = 200 }], behavior, ignored));
        Assert.True(UpdateBlockers.HasUnacceptedBlockers([new() { Pid = 43, Started = 100 }], behavior, ignored));
        Assert.False(UpdateBlockers.HasUnacceptedBlockers([], behavior, ignored));
    }

    [Fact]
    public void EmbeddedLanguagesHaveExactlyTheSameKeysAndNoProductName()
    {
        var root = Path.GetFullPath(Path.Combine(SourceDirectory(), "../../src/Mue/Resources"));
        var english = XDocument.Load(Path.Combine(root, "Strings.resx"));
        var french = XDocument.Load(Path.Combine(root, "Strings.fr.resx"));
        Assert.Equal(Keys(english), Keys(french));
        foreach (var resource in new[] { english, french })
        {
            Assert.DoesNotContain("DockPad", resource.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Keys(resource).Length, Keys(resource).Distinct().Count());
        }
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void AppNameIsReplacedBeforeFormattingOnlyInEmbeddedTexts(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        var appName = UpdateTexts.AppName;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            UpdateTexts.AppName = "Host Example";
            UpdateTexts.Configure(null);
            Assert.Contains("Host Example", UpdateTexts.T("Update_TitleCurrent"));
            Assert.Contains("Host Example", UpdateTexts.F("Update_TitleCurrent"));
            Assert.DoesNotContain("{AppName}", UpdateTexts.F("Update_McpHelp"));
            foreach (var result in Enum.GetValues<UpdateBlockerCloseResult>())
                Assert.DoesNotContain("[", UpdatePresentation.CloseResultText(result));
            UpdateTexts.Configure(new LiteralHostTexts());
            Assert.Equal("Host {AppName}", UpdateTexts.T("Update_TitleCurrent"));
            Assert.Equal("Host {AppName}", UpdateTexts.F("Update_TitleCurrent"));
            Assert.Contains("Host Example", UpdateTexts.F("Update_McpHelp"));
        }
        finally { UpdateTexts.AppName = appName; CultureInfo.CurrentUICulture = previous; TestUpdateTexts.Initialize(); }
    }

    private static string[] Keys(XDocument document) => document.Descendants("data")
        .Select(element => (string?)element.Attribute("name") ?? throw new InvalidOperationException()).Order().ToArray();
    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path) ?? throw new InvalidOperationException();

    private sealed class LiteralHostTexts : IUpdateTexts
    {
        public bool TryGet(string key, out string text) { text = T(key); return key == "Update_TitleCurrent"; }
        public string T(string key) => "Host {AppName}";
        public string F(string key, params object?[] args) => T(key);
        public CultureInfo Formatting => CultureInfo.CurrentUICulture;
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}
