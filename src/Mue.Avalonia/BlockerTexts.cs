namespace Mue.Avalonia;

internal static class BlockerTexts
{
    internal static string DetectionIncomplete => nameof(UpdateBlockerScan.Incomplete);
    internal static string CloseResult(UpdateBlockerCloseResult result) => result.ToString();
}
