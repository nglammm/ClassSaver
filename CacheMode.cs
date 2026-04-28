namespace ClassSaver;

/// <summary>
/// The cache modes available.
/// </summary>
public enum CacheMode
{
    /// <summary>
    /// No caching applied, this is often good if the class you are serializing
    /// contains little data and the cache data made the file bigger.
    /// </summary>
    None
}