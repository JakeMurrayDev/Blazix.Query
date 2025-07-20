using Microsoft.JSInterop;

namespace Blazix.Query;

public interface INetworkService
{
    bool IsOnline { get; }

    event Func<Task>? NetworkStatusChanged;

    Task InitializeAsync();
}

public sealed class NetworkService(IJSRuntime jsRuntime) : IAsyncDisposable, INetworkService
{
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
        "import", "./_content/Blazix.Query/blazix-network.js").AsTask());
    private DotNetObjectReference<NetworkService>? dotNetHelper;

    /// <summary>
    /// Event raised when the network status changes.
    /// </summary>
    public event Func<Task>? NetworkStatusChanged;

    /// <summary>
    /// Gets the value indicating whether the system is currently online.
    /// </summary>
    public bool IsOnline { get; private set; }

    /// <summary>
    /// Initializes the network service by loading the JavaScript module and setting up the event listener.
    /// </summary>
    /// <returns></returns>
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
            await NetworkStatusChanged?.Invoke()!;
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
