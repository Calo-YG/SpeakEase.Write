using Moq;

using AINWZ.Tests.AI;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.Write.Application.Abstractions.Authorization;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Infrastructure.AI;

namespace AINWZ.Tests.Security;

public sealed class WorkToolExecutionGuardTests
{
    [Fact]
    public async Task AuthorizeAsync_RejectsToolArgumentsForAnotherUsersWork()
    {
        var access = new Mock<IWorkAccessChecker>();
        access.Setup(x => x.OwnsWorkAsync("work-2", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var user = new TestUserContext("user-1");
        var guard = new WorkToolExecutionGuard(access.Object, user);

        var result = await guard.AuthorizeAsync("get_work_info", "{\"work_id\":\"work-2\"}");

        Assert.False(result.Success);
        Assert.Equal("work_access_denied", result.ErrorCode);
    }

    [Fact]
    public async Task AuthorizeAsync_AllowsNonWorkScopedToolsWithoutInspectingArguments()
    {
        var access = new Mock<IWorkAccessChecker>();
        var user = new TestUserContext("user-1");
        var guard = new WorkToolExecutionGuard(access.Object, user);

        var result = await guard.AuthorizeAsync("web_search", "not-json");

        Assert.True(result.Success);
        access.Verify(x => x.OwnsWorkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData("{\"work_id\":42}")]
    public async Task AuthorizeAsync_RejectsWorkScopedToolWhenWorkIdCannotBeRead(string arguments)
    {
        var access = new Mock<IWorkAccessChecker>();
        var user = new TestUserContext("user-1");
        var guard = new WorkToolExecutionGuard(access.Object, user);

        var result = await guard.AuthorizeAsync("get_work_info", arguments);

        Assert.False(result.Success);
        Assert.Equal("work_access_denied", result.ErrorCode);
        access.Verify(x => x.OwnsWorkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsPascalCaseWorkIdForAnotherUsersWork()
    {
        var access = new Mock<IWorkAccessChecker>();
        access.Setup(x => x.OwnsWorkAsync("work-2", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var user = new TestUserContext("user-1");
        var guard = new WorkToolExecutionGuard(access.Object, user);

        var result = await guard.AuthorizeAsync("get_work_info", "{\"WorkId\":\"work-2\"}");

        Assert.False(result.Success);
        Assert.Equal("work_access_denied", result.ErrorCode);
        access.Verify(x => x.OwnsWorkAsync("work-2", "user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsConflictingWorkIdAliases()
    {
        var access = new Mock<IWorkAccessChecker>();
        access.Setup(x => x.OwnsWorkAsync("work-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var user = new TestUserContext("user-1");
        var guard = new WorkToolExecutionGuard(access.Object, user);

        var result = await guard.AuthorizeAsync(
            "get_work_info",
            "{\"work_id\":\"work-1\",\"WorkId\":\"work-2\"}");

        Assert.False(result.Success);
        Assert.Equal("work_access_denied", result.ErrorCode);
        access.Verify(x => x.OwnsWorkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
