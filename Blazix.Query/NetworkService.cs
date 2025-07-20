using Microsoft.JSInterop;

namespace Blazix.Query;


/// <summary>
/// Provides an abstraction for monitoring network status.
/// </summary>
public interface INetworkService
{
    /// <summary>
    /// Event raised when the network status changes.
    /// </summary>
    event Func<Task>? NetworkStatusChanged;

    /// <summary>
    /// Gets the value indicating whether the system is currently online.
    /// </summary>
    bool IsOnline { get; }

    /// <summary>
    /// Initializes the network service.
    /// </summary>
    Task InitializeAsync();
}

/// <summary>
/// Monitors the network status using JavaScript.
/// </summary>
/// <param name="jsRuntime">The JavaScript runtime dependency.</param>
public class NetworkService(IJSRuntime jsRuntime) : INetworkService, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
        "import", "./_content/Blazix.Query/blazix-network.js").AsTask());
    private DotNetObjectReference<NetworkService>? dotNetHelper;

    /// <inheritdoc />
    public event Func<Task>? NetworkStatusChanged;

    /// <inheritdoc />
    public bool IsOnline { get; private set; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        dotNetHelper = DotNetObjectReference.Create(this);
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("initialize", dotNetHelper);
    }

    /// <summary>
    /// Raises the NetworkStatusChanged event. Do not call this method directly.
    /// </summary>
    /// <param name="isOnline">The new network status.</param>
    [JSInvokable]
    public async Task OnNetworkStatusChanged(bool isOnline)
    {
        IsOnline = isOnline;
        if (NetworkStatusChanged != null)
        {
            await NetworkStatusChanged.Invoke();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        dotNetHelper?.Dispose();

        if (moduleTask.IsValueCreated)
        {
            var module = await moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
