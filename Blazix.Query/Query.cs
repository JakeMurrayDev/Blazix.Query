namespace Blazix.Query;

/// <summary>
/// Represents the options for a query.
/// </summary>
public class QueryOptions
{
    /// <summary>
    /// Gets or sets time after which the query is considered stale.
    /// </summary>
    public TimeSpan StaleTime { get; set; } = TimeSpan.Zero;
    /// <summary>
    /// Gets or sets whether the query is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// Gets or sets the number of times to retry the query if it fails.
    /// </summary>
    public int Retry { get; set; } = 0;
    /// <summary>
    /// Gets or sets the delay between retries.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}


/// <summary>
/// Represents a query.
/// </summary>
/// <typeparam name="TData">The type of data returned by the query.</typeparam>
public sealed class Query<TData> : IDisposable where TData : class
{
    private readonly string key;
    private readonly Func<Task<TData>> queryFn;
    private readonly QueryClient client;
    private readonly Func<Task> stateChangedCallback;
    private readonly QueryOptions options;
    private readonly QueryCacheEntry<TData> cacheEntry;

    /// <summary>
    /// The data returned by the query.
    /// </summary>
    public TData? Data => cacheEntry.Data;

    /// <summary>
    /// The error returned by the query.
    /// </summary>
    public Exception? Error => cacheEntry.Error;

    /// <summary>
    /// The status of the query.
    /// </summary>
    public QueryStatus Status => cacheEntry.Status;

    /// <summary>
    /// The status of the query function.
    /// </summary>
    public QueryFetchStatus FetchStatus => cacheEntry.FetchStatus;

    /// <summary>
    /// True if the query is stale.
    /// </summary>
    public bool IsStale => cacheEntry.IsStale(options.StaleTime);

    /// <summary>
    /// True if the query is in the 'Pending' state.
    /// </summary>
    public bool IsPending => Status == QueryStatus.Pending;

    /// <summary>
    /// True if the query is in the 'Success' state.
    /// </summary>
    public bool IsSuccess => Status == QueryStatus.Success;

    /// <summary>
    /// True if the query is in the 'Error' state.
    /// </summary>
    public bool IsError => Status == QueryStatus.Error;

    /// <summary>
    /// True if the query is currently fetching data (includes initial load and background refetches).
    /// </summary>
    public bool IsFetching => FetchStatus == QueryFetchStatus.Fetching;

    /// <summary>
    /// True if the query is paused, likely due to being offline.
    /// </summary>
    public bool IsPaused => FetchStatus == QueryFetchStatus.Paused;

    /// <summary>
    /// True only during the initial fetch when the query has no data yet.
    /// Perfect for showing a full-page loading spinner.
    /// </summary>
    public bool IsLoading => IsPending && IsFetching;

    /// <summary>
    /// True when a background refetch is happening, but you already have data to display.
    /// Perfect for showing a subtle "refreshing..." indicator.
    /// </summary>
    public bool IsRefetching => !IsPending && IsFetching;

    internal Query(
        string key,
        Func<Task<TData>> queryFn,
        QueryClient client,
        Func<Task> stateChangedCallback,
        QueryOptions? options)
    {
        this.key = key;
        this.queryFn = queryFn;
        this.client = client;
        this.stateChangedCallback = stateChangedCallback;
        this.options = options ?? new QueryOptions();

        cacheEntry = this.client.GetCacheEntry<TData>(this.key);
        cacheEntry.Subscribe(this.stateChangedCallback);

        InitializeFetch();
    }

    private void InitializeFetch()
    {
        if (!options.Enabled)
        {
            return;
        }

        if (Status == QueryStatus.Success && !IsStale)
        {
            return;
        }

        _ = client.FetchQueryAsync(key, queryFn, options);
    }

    /// <summary>
    /// Manually refetches the query.
    /// </summary>
    public Task RefetchAsync()
    {
        if (!options.Enabled)
        {
            return Task.CompletedTask;
        }
        return client.FetchQueryAsync(key, queryFn, options);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        cacheEntry.Unsubscribe(stateChangedCallback);
    }
}
