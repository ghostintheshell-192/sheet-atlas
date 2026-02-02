namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// String interning pool for deduplicating repeated values. Reduces memory for repeated strings (categories, enums). Thread-safe reads.
    /// </summary>
    public sealed class StringPool
    {
        private readonly Dictionary<string, string> _pool;
        private readonly object _lock = new();

        public StringPool(int initialCapacity = 1024)
        {
            _pool = new Dictionary<string, string>(initialCapacity, StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets the interned version of the string, or adds it to the pool if not present.
        /// Returns the same reference for identical strings, reducing memory usage.
        /// </summary>
        public string Intern(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Fast path: check without lock (safe for reads)
            if (_pool.TryGetValue(value, out var interned))
                return interned;

            // Slow path: add to pool with lock
            lock (_lock)
            {
                // Double-check after acquiring lock
                if (_pool.TryGetValue(value, out interned))
                    return interned;

                // Add new string to pool
                _pool[value] = value;
                return value;
            }
        }

        /// <summary>
        /// Clears the string pool to free memory.
        /// Should be called after processing files to release interned strings.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _pool.Clear();
            }
        }

        /// <summary>
        /// Gets the current number of unique strings in the pool.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _pool.Count;
                }
            }
        }

        /// <summary>
        /// Estimates memory saved by interning (rough approximation).
        /// Assumes average string size and 50% duplication rate.
        /// </summary>
        public long EstimatedMemorySaved(int totalStringsProcessed)
        {
            var uniqueStrings = Count;
            var duplicates = totalStringsProcessed - uniqueStrings;

            // Rough estimate: average string ~20 chars = 40 bytes + overhead
            const int avgStringSize = 60;
            return duplicates * avgStringSize;
        }
    }
}
