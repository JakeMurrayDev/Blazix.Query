/// <summary>
/// Represents the status of the query.
/// </summary>
public enum QueryStatus
{
    /// <summary>
    /// The query is active but has no data yet.
    /// </summary>
    Pending,
    /// <summary>
    /// The query was successful and has data.
    /// </summary>
    Success,
    /// <summary>
    /// The query encountered an error.
    /// </summary>
    Error
}

/// <summary>
/// Represents the status of the query function.
/// </summary>
public enum QueryFetchStatus
{
    /// <summary>
    /// The query function is currently fetching.
    /// </summary>
    Fetching,
    /// <summary>
    /// The query wanted to fetch but is currently paused (e.g., due to no network connection).
    /// </summary>
    Paused,
    /// <summary>
    /// The query is not currently fetching.
    /// </summary>
    Idle
}