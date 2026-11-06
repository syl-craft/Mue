namespace Mue.Tests;

public class UpdateBlockerGroupsTests
{

    [Fact]
    public void RelaysAreGroupedFirstAndProgramsAreGroupedByNameWithoutSelectingThem()
    {
        UpdateBlocker[] processes = [
            new() { Pid = 1, IsRelay = true, ParentName = "Claude Code", Selected = true },
            new() { Pid = 2, IsRelay = true, ParentName = "Claude Desktop", Selected = true },
            new() { Pid = 3, Name = "Editor" },
            new() { Pid = 4, Name = "editor" },
            new() { Pid = 5, Name = "Terminal" },
        ];
        var groups = UpdateBlockerGroups.Group(processes);
        Assert.Equal(3, groups.Count);
        Assert.True(groups[0].IsRelay);
        Assert.Equal(2, groups[0].Count);
        Assert.True(groups[0].Selected);
        Assert.False(groups[1].Selected);
        Assert.Equal(2, groups[1].Count);
        groups[1].Selected = true;
        Assert.True(processes[2].Selected && processes[3].Selected);
        Assert.False(processes[4].Selected);
        groups[0].Selected = false;
        Assert.False(processes[0].Selected || processes[1].Selected);
    }

    [Fact]
    public void ReusedPidCannotMatchAStopRequestForEitherKindOfProcess()
    {
        foreach (var relay in new[] { false, true })
        {
            var blocker = new UpdateBlocker { Pid = 42, Started = 100, IsRelay = relay };
            Assert.False(UpdateBlockers.MatchesIdentity(blocker, started: 200));
            Assert.True(UpdateBlockers.MatchesIdentity(blocker, started: 100));
        }
    }

    [Fact]
    public void OnlyRelaysAreSelectedByDefault()
    {
        Assert.True(new UpdateBlocker { IsRelay = true }.Selected);
        Assert.False(new UpdateBlocker { IsRelay = false }.Selected);
    }
}
