using System.Globalization;

namespace Mue.Tests;

public sealed class UpdateTextKeysTests
{
    [Fact]
    public void All_lists_every_neutral_key_including_the_040_additions()
    {
        Assert.Equal(89, UpdateTextKeys.All.Count);
        Assert.Contains("Update_Credit", UpdateTextKeys.All);
        Assert.Contains("Update_ConfirmHelpForced", UpdateTextKeys.All);
        Assert.Contains("Update_ConfirmHelpAskToQuit", UpdateTextKeys.All);
        Assert.Contains("Update_Download", UpdateTextKeys.All);
    }

    [Fact]
    public void Each_built_in_language_is_complete()
    {
        // Règle des langues : une langue n'entre que complète, sinon un hôte en français verrait de l'anglais.
        var french = UpdateTextKeys.ReadKeys(CultureInfo.GetCultureInfo("fr"));

        Assert.Equal(UpdateTextKeys.All, french);
    }

    [Fact]
    public void Missing_reports_the_keys_a_host_does_not_provide()
    {
        var provided = UpdateTextKeys.All.Where(key => key != "Update_Download").ToHashSet();
        var texts = new PartialTexts(provided);

        Assert.Equal(["Update_Download"], UpdateTextKeys.Missing(texts));
    }

    [Fact]
    public void Missing_is_empty_for_a_host_that_provides_every_key()
    {
        Assert.Empty(UpdateTextKeys.Missing(new TestUpdateTexts()));
    }

    [Fact]
    public void Missing_lists_everything_for_a_host_that_keeps_Mue_texts()
    {
        Assert.Equal(UpdateTextKeys.All, UpdateTextKeys.Missing(new PartialTexts(new HashSet<string>())));
    }

    [Fact]
    public void Style_keys_are_brushes_then_styles()
    {
        Assert.Equal(25, UpdateStyleKeys.Required.Count);
        Assert.Equal(UpdateStyleKeys.Brushes.Concat(UpdateStyleKeys.Styles), UpdateStyleKeys.Required);
    }

    private sealed class PartialTexts(IReadOnlySet<string> keys) : IUpdateTexts
    {
        public bool TryGet(string key, out string text) { text = key; return keys.Contains(key); }
        public string T(string key) => key;
        public string F(string key, params object?[] args) => key;
        public CultureInfo Formatting => CultureInfo.InvariantCulture;
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}
