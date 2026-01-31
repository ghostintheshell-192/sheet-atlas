namespace SheetAtlas.Tests.Services
{
    using SheetAtlas.Core.Application.Services.HeaderResolvers;
    using Xunit;

    public class HeaderResolverTests
    {
        public class DictionaryHeaderResolverTests
        {
            [Fact]
            public void ResolveSemanticName_WithMatchingHeader_ReturnsSemanticName()
            {
                // Arrange
                var semanticNames = new Dictionary<string, string>
                {
                    ["Price"] = "Amount",
                    ["Cost"] = "Amount",
                    ["Date"] = "Transaction Date"
                };
                var resolver = new DictionaryHeaderResolver(semanticNames);

                // Act
                var result = resolver.ResolveSemanticName("Price");

                // Assert
                Assert.Equal("Amount", result);
            }

            [Fact]
            public void ResolveSemanticName_WithNonMatchingHeader_ReturnsNull()
            {
                // Arrange
                var semanticNames = new Dictionary<string, string>
                {
                    ["Price"] = "Amount"
                };
                var resolver = new DictionaryHeaderResolver(semanticNames);

                // Act
                var result = resolver.ResolveSemanticName("Unknown");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void ResolveSemanticName_WithNullDictionary_ReturnsNull()
            {
                // Arrange
                var resolver = new DictionaryHeaderResolver(null);

                // Act
                var result = resolver.ResolveSemanticName("Price");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void ResolveSemanticName_WithEmptyDictionary_ReturnsNull()
            {
                // Arrange
                var resolver = new DictionaryHeaderResolver(new Dictionary<string, string>());

                // Act
                var result = resolver.ResolveSemanticName("Price");

                // Assert
                Assert.Null(result);
            }
        }

        public class FunctionHeaderResolverTests
        {
            [Fact]
            public void ResolveSemanticName_WithMatchingFunction_ReturnsSemanticName()
            {
                // Arrange
                string? ResolverFunc(string header) => header switch
                {
                    "Price" => "Amount",
                    "Cost" => "Amount",
                    "Date" => "Transaction Date",
                    _ => null
                };
                var resolver = new FunctionHeaderResolver(ResolverFunc);

                // Act
                var result = resolver.ResolveSemanticName("Price");

                // Assert
                Assert.Equal("Amount", result);
            }

            [Fact]
            public void ResolveSemanticName_WithNonMatchingFunction_ReturnsNull()
            {
                // Arrange
                string? ResolverFunc(string header) => header == "Price" ? "Amount" : null;
                var resolver = new FunctionHeaderResolver(ResolverFunc);

                // Act
                var result = resolver.ResolveSemanticName("Unknown");

                // Assert
                Assert.Null(result);
            }

            [Fact]
            public void ResolveSemanticName_WithNullFunction_ReturnsNull()
            {
                // Arrange
                var resolver = new FunctionHeaderResolver(null);

                // Act
                var result = resolver.ResolveSemanticName("Price");

                // Assert
                Assert.Null(result);
            }
        }

        public class NullHeaderResolverTests
        {
            [Fact]
            public void ResolveSemanticName_AlwaysReturnsNull()
            {
                // Arrange
                var resolver = NullHeaderResolver.Instance;

                // Act & Assert
                Assert.Null(resolver.ResolveSemanticName("Price"));
                Assert.Null(resolver.ResolveSemanticName("Cost"));
                Assert.Null(resolver.ResolveSemanticName(""));
            }

            [Fact]
            public void Instance_ReturnsSameSingleton()
            {
                // Arrange & Act
                var instance1 = NullHeaderResolver.Instance;
                var instance2 = NullHeaderResolver.Instance;

                // Assert
                Assert.Same(instance1, instance2);
            }
        }
    }
}
