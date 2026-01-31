namespace SheetAtlas.Core.Application.Services.HeaderResolvers
{
    using SheetAtlas.Core.Application.Interfaces;

    /// <summary>
    /// Resolves semantic names using a function delegate.
    /// Used by ViewModels with injected resolver function.
    /// </summary>
    public class FunctionHeaderResolver : IHeaderResolver
    {
        private readonly Func<string, string?>? _resolverFunc;

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionHeaderResolver"/> class.
        /// </summary>
        /// <param name="resolverFunc">Function that maps original header names to semantic names</param>
        public FunctionHeaderResolver(Func<string, string?>? resolverFunc)
        {
            _resolverFunc = resolverFunc;
        }

        /// <inheritdoc/>
        public string? ResolveSemanticName(string originalHeader)
        {
            return _resolverFunc?.Invoke(originalHeader);
        }
    }
}
