using System.Collections.Concurrent;

namespace Blazix.Query;

internal abstract class QueryCacheEntry
{
    public Exception? Error { get; protected set; }
    public QueryStatus Status { get; protected set; } = QueryStatus.Pending;
    public QueryFetchStatus FetchStatus { get; protected set; } = QueryFetchStatus.Idle;
    public DateTime LastSuccessAt { get; set; }

    private readonly List<Func<Task>> subscribers = new();

    public void Subscribe(Func<Task> callback) => subscribers.Add(callback);
    public void Unsubscribe(Func<Task> callback) => subscribers.Remove(callback);
    public async Task NotifySubscribers()
    {
        foreach (var callback in subscribers)
        {
            await callback.Invoke();
        }
    }

    public Task SetFetchStatus(QueryFetchStatus status)
    {
        if (FetchStatus == status) return Task.CompletedTask;
        FetchStatus = status;
        return NotifySubscribers();
    }
}

internal sealed class QueryCacheEntry<TData> : QueryCacheEntry where TData : class
{
    public TData? Data { get; private set; }

    public Task SetSuccess(TData data)
    {
        Data = data;
        Error = null;
        Status = QueryStatus.Success;
        FetchStatus = QueryFetchStatus.Idle;
        LastSuccessAt = DateTime.UtcNow;
        return NotifySubscribers();
    }

    public Task SetError(Exception e)
    {
        Error = e;
        Status = QueryStatus.Error;
        FetchStatus = QueryFetchStatus.Idle;
        return NotifySubscribers();
    }
}

/// <summary>
/// Represents a client for making queries.
/// </summary>
public sealed class QueryClient
{
    private readonly NetworkService networkService;
    private readonly ConcurrentDictionary<string, QueryCacheEntry> cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> fetchSemaphores = new();

    public QueryClient(NetworkService networkService)
    {
        this.networkService = networkService;
    }

    /// <summary>
    /// Creates a new query.
    /// </summary>
    /// <typeparam name="TData">The type of data returned by the query.</typeparam>
    /// <param name="key">The key of the query.</param>
    /// <param name="queryFn">The query function.</param>
    /// <param name="stateChangedCallback">The callback to be called when the state of the query changes.</param>
    /// <param name="options">The options for the query.</param>
    /// <returns>The created query.</returns>
    public Query<TData> CreateQuery<TData>(
        string key,
        Func<Task<TData>> queryFn,
        Func<Task> stateChangedCallback,
        QueryOptions? options = null) where TData : class
    {
        return new Query<TData>(key, queryFn, this, stateChangedCallback, options);
    }

    // Internal method for Query<TData> to get its state
    internal QueryCacheEntry<TData> GetCacheEntry<TData>(string key) where TData : class
    {
        var entry = cache.GetOrAdd(key, _ => new QueryCacheEntry<TData>());
        return (QueryCacheEntry<TData>)entry;
    }

    // The core fetching logic
    internal async Task FetchQueryAsync<TData>(string key, Func<Task<TData>> queryFn) where TData : class
    {
        var entry = (QueryCacheEntry<TData>)cache[key];

        // Use a semaphore to prevent multiple simultaneous fetches for the same key
        var semaphore = fetchSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();

        try
        {
            // Double-check if another thread already fetched while we were waiting
            if (entry.FetchStatus == QueryFetchStatus.Fetching) return;

            if (!networkService.IsOnline)
            {
                await entry.SetFetchStatus(QueryFetchStatus.Paused);
                return;
            }

            await entry.SetFetchStatus(QueryFetchStatus.Fetching);

            try
            {
                var data = await queryFn();
                await entry.SetSuccess(data);
            }
            catch (Exception e)
            {
                await entry.SetError(e);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Invalidates the cache for the specified key.
    /// </summary>
    /// <param name="key">The key to invalidate.</param>
    public Task InvalidateQueries(string key)
    {
        if (cache.TryGetValue(key, out var entry))
        {
            // Mark as stale by resetting the update time
            entry.LastSuccessAt = DateTime.MinValue;
            return entry.NotifySubscribers();
        }

        return Task.CompletedTask;
    }
}
