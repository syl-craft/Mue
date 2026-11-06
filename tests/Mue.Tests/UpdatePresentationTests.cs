namespace Mue.Tests;

public class UpdatePresentationTests
{
    [Theory]
    [InlineData(UpdateState.Idle, "Update_TitleCurrent", UpdateAction.Check, UpdateAction.Close, true)]
    [InlineData(UpdateState.Available, "Update_TitleAvailable", UpdateAction.Close, UpdateAction.Install, true)]
    [InlineData(UpdateState.Downloading, "Update_TitleDownloading", UpdateAction.Stop, UpdateAction.Install, false)]
    [InlineData(UpdateState.Ready, "Update_TitleReady", UpdateAction.Close, UpdateAction.Install, true)]
    [InlineData(UpdateState.Failed, "Update_TitleFailed", UpdateAction.Close, UpdateAction.Check, true)]
    [InlineData(UpdateState.Unavailable, "Update_TitleUnavailable", UpdateAction.DownloadLatest, UpdateAction.Close, true)]
    [InlineData(UpdateState.Checking, "Update_TitleChecking", UpdateAction.Close, UpdateAction.Check, false)]
    [InlineData(UpdateState.Preparing, "Update_TitlePreparing", UpdateAction.None, UpdateAction.Install, false)]
    [InlineData(UpdateState.Installing, "Update_TitleInstalling", UpdateAction.None, UpdateAction.Install, false)]
    public void EachStateHasItsTitleAndSafeActions(UpdateState state, string title, UpdateAction secondary, UpdateAction primary, bool enabled)
    {
        var presentation = UpdatePresentation.For(state, hasChecked: true);
        Assert.Equal(title, presentation.TitleKey);
        Assert.Equal(secondary, presentation.Secondary);
        Assert.Equal(primary, presentation.Primary);
        Assert.Equal(enabled, presentation.PrimaryEnabled);
    }

    [Fact]
    public void IdleDoesNotClaimToBeCurrentBeforeTheFirstCheck()
    {
        Assert.Equal("Update_Title", UpdatePresentation.For(UpdateState.Idle, hasChecked: false).TitleKey);
        Assert.Equal("Update_TitleCurrent", UpdatePresentation.For(UpdateState.Idle, hasChecked: true).TitleKey);
    }

    [Fact]
    public void DownloadAndRestartAreDifferentActionsWithTheSameInstallationFlow()
    {
        Assert.Equal("Update_DownloadInstall", UpdatePresentation.For(UpdateState.Available, true).PrimaryKey);
        Assert.Equal("Update_RestartInstall", UpdatePresentation.For(UpdateState.Ready, true).PrimaryKey);
        Assert.False(UpdatePresentation.For(UpdateState.Idle, true).PrimaryAccent);
        Assert.False(UpdatePresentation.For(UpdateState.Unavailable, true).PrimaryAccent);
    }

}
