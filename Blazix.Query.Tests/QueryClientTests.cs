using Microsoft.JSInterop;
using NSubstitute;

namespace Blazix.Query.Tests;

public class QueryClientTests
{
    private readonly NetworkService networkServiceMock;
    private readonly QueryClient sut;

    public QueryClientTests()
    {
        networkServiceMock = Substitute.For<NetworkService>(Substitute.For<IJSRuntime>());
        sut = new QueryClient(networkServiceMock);
    }

    [Fact]
    public async Task CreateQuery_ShouldFetchData_WhenCacheIsEmpty()
    {
        // Arrange
        // NSubstitute syntax for setting a property's return value.
        networkServiceMock.IsOnline.Returns(true);
        var tcs = new TaskCompletionSource();
        var queryFn = () => Task.FromResult("Test Data");

        // Act
        var query = sut.CreateQuery(
            key: "test-key",
            queryFn: queryFn,
            stateChangedCallback: () => {
                tcs.SetResult();
                return Task.CompletedTask;
            }
        );

        await tcs.Task;

        // Assert
        Assert.True(query.IsSuccess);
        Assert.False(query.IsFetching);
        Assert.Equal("Test Data", query.Data);
        Assert.Null(query.Error);
    }

    [Fact]
    public async Task CreateQuery_ShouldReturnCachedData_AndRefetchStaleData()
    {
        // Arrange
        networkServiceMock.IsOnline.Returns(true);
        var initialEntry = sut.GetCacheEntry<string>("stale-key");
        await initialEntry.SetSuccessAsync("Stale Data");
        initialEntry.LastSuccessAt = DateTime.UtcNow.AddMinutes(-5);

        var tcs = new TaskCompletionSource();
        var fetchCount = 0;
        var queryFn = () => {
            fetchCount++;
            return Task.FromResult("Fresh Data");
        };

        // Act
        var query = sut.CreateQuery(
            key: "stale-key",
            queryFn: queryFn,
            stateChangedCallback: () => {
                if (fetchCount > 0) tcs.TrySetResult();
                return Task.CompletedTask;
            },
            options: new QueryOptions { StaleTime = TimeSpan.FromMinutes(1) }
        );

        // Assert (Initial State)
        Assert.True(query.IsSuccess);
        Assert.Equal("Stale Data", query.Data);
        Assert.True(query.IsRefetching);

        await tcs.Task;

        // Assert (Final State)
        Assert.True(query.IsSuccess);
        Assert.Equal("Fresh Data", query.Data);
        Assert.False(query.IsFetching);
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task CreateQuery_ShouldEnterPausedState_WhenOffline()
    {
        // Arrange
        networkServiceMock.IsOnline.Returns(false); // Set to offline
        var tcs = new TaskCompletionSource();
        var queryFn = () => Task.FromResult("Should not be called");

        // Act
        var query = sut.CreateQuery(
            key: "paused-key",
            queryFn: queryFn,
            stateChangedCallback: () => {
                tcs.SetResult();
                return Task.CompletedTask;
            }
        );

        await tcs.Task;

        // Assert
        Assert.True(query.IsPending);
        Assert.True(query.IsPaused);
        Assert.False(query.IsFetching);
        Assert.Null(query.Data);
    }
}
