namespace SheetAtlas.Core.Application.Services.HeaderResolvers
{
    using SheetAtlas.Core.Application.Interfaces;

    /// <summary>
    /// Resolves semantic names from a dictionary.
    /// Used by export services that receive semantic name mappings as parameters.
    /// </summary>
    public class DictionaryHeaderResolver : IHeaderResolver
    {
        private readonly IReadOnlyDictionary<string, string>? _semanticNames;

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryHeaderResolver"/> class.
        /// </summary>
        /// <param name="semanticNames">Dictionary mapping original header names to semantic names</param>
        public DictionaryHeaderResolver(IReadOnlyDictionary<string, string>? semanticNames)
        {
            _semanticNames = semanticNames;
        }

        /// <inheritdoc/>
        public string? ResolveSemanticName(string originalHeader)
        {
            if (_semanticNames == null)
                return null;

            return _semanticNames.TryGetValue(originalHeader, out var semanticName)
                ? semanticName
                : null;
        }
    }
}
