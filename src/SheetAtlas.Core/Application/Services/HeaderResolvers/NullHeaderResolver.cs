namespace SheetAtlas.Core.Application.Services.HeaderResolvers
{
    using SheetAtlas.Core.Application.Interfaces;

    /// <summary>
    /// No-op resolver that returns null for all headers.
    /// Used when no semantic name mapping is needed (identity mapping).
    /// </summary>
    public class NullHeaderResolver : IHeaderResolver
    {
        /// <summary>
        /// Gets the singleton instance of <see cref="NullHeaderResolver"/>.
        /// </summary>
        public static readonly NullHeaderResolver Instance = new();

        private NullHeaderResolver()
        {
        }

        /// <inheritdoc/>
        public string? ResolveSemanticName(string originalHeader) => null;
    }
}
