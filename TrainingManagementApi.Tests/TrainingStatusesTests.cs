using TrainingManagementApi.Models;

namespace TrainingManagementApi.Tests;

/// <summary>Plain unit tests (no web server) for the status parsing rules.</summary>
public class TrainingStatusesTests
{
    [Theory]
    [InlineData("Assigned", "Assigned")]
    [InlineData("assigned", "Assigned")]
    [InlineData("ASSIGNED", "Assigned")]
    [InlineData("In Progress", "In Progress")]
    [InlineData("in progress", "In Progress")]
    [InlineData("IN-PROGRESS", "In Progress")]
    [InlineData("InProgress", "In Progress")]
    [InlineData("in_progress", "In Progress")]
    [InlineData("  completed  ", "Completed")]
    public void TryNormalize_AcceptsKnownStatuses_InAnyCasingOrSpacing(string input, string expected)
    {
        Assert.True(TrainingStatuses.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Done")]
    [InlineData("Finished")]
    [InlineData("Completed2")]
    [InlineData("In Progress!")]
    [InlineData("Assigned,Completed")]
    [InlineData("123")]
    public void TryNormalize_RejectsEverythingElse(string? input)
    {
        Assert.False(TrainingStatuses.TryNormalize(input, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void InvalidStatusMessage_ListsAllAllowedValues()
    {
        var message = TrainingStatuses.InvalidStatusMessage;

        Assert.All(TrainingStatuses.All, status => Assert.Contains(status, message));
    }
}
