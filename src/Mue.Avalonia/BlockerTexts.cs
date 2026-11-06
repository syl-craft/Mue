namespace Mue.Avalonia;

internal static class BlockerTexts
{
    internal static string DetectionIncomplete => AvaloniaUpdateTexts.T("Update_DetectionIncomplete");
    internal static string CloseResult(UpdateBlockerCloseResult result) => AvaloniaUpdateTexts.T(result switch
    {
        UpdateBlockerCloseResult.Closed => "Update_CloseResultClosed",
        UpdateBlockerCloseResult.StillRunning => "Update_CloseResultStillRunning",
        UpdateBlockerCloseResult.AccessDenied => "Update_CloseResultAccessDenied",
        UpdateBlockerCloseResult.Gone => "Update_CloseResultGone",
        UpdateBlockerCloseResult.IdentityChanged => "Update_CloseResultIdentityChanged",
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    });
}
