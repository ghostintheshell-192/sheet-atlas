using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using System.Globalization;

namespace SheetAtlas.Tests.Foundation.Fixtures
{
    /// <summary>
    /// Loads and provides real sample files from samples/real-data/ directory.
    /// Used for integration tests with actual Excel/CSV data.
    /// </summary>
    public class SampleDataFixture : IDisposable
    {
        private readonly string _sampleDataPath;
        private bool _disposed = false;

        public SampleDataFixture()
        {
            // Resolve sample data path relative to project
            var projectRoot = FindProjectRoot();
            _sampleDataPath = Path.Combine(projectRoot, "samples", "real-data");
        }

        /// <summary>
        /// Loads the test-mixed-dates.csv file.
        /// Contains dates in multiple formats: ISO, US (MM/DD/YYYY), Excel serial, and text formats.
        /// </summary>
        public string GetMixedDatesFilePath()
        {
            var path = Path.Combine(_sampleDataPath, "test-mixed-dates.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Sample file not found: {path}");
            return path;
        }

        /// <summary>
        /// Loads the test-currency-issues.csv file.
        /// Contains currency amounts with mixed symbols ($, €, £) and various formats.
        /// </summary>
        public string GetCurrencyIssuesFilePath()
        {
            var path = Path.Combine(_sampleDataPath, "test-currency-issues.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Sample file not found: {path}");
            return path;
        }

        /// <summary>
        /// Loads the test-boolean-variations.csv file.
        /// Contains boolean values in various formats: Yes/No, 1/0, TRUE/FALSE, etc.
        /// </summary>
        public string GetBooleanVariationsFilePath()
        {
            var path = Path.Combine(_sampleDataPath, "test-boolean-variations.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Sample file not found: {path}");
            return path;
        }

        /// <summary>
        /// Loads the test-whitespace-issues.csv file.
        /// Contains text with leading/trailing spaces and zero-width characters.
        /// </summary>
        public string GetWhitespaceIssuesFilePath()
        {
            var path = Path.Combine(_sampleDataPath, "test-whitespace-issues.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Sample file not found: {path}");
            return path;
        }

        /// <summary>
        /// Loads the test-all-problems.csv file.
        /// Contains a mix of all data quality issues for comprehensive testing.
        /// </summary>
        public string GetAllProblemsFilePath()
        {
            var path = Path.Combine(_sampleDataPath, "test-all-problems.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Sample file not found: {path}");
            return path;
        }

        /// <summary>
        /// Loads the test-clean-data.csv file.
        /// Contains well-formed data for positive test cases.
        /// </summary>
        public string GetCleanDataFilePath()
        {
            var path = Path.Combine(_sampleDataPath, "test-clean-data.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Sample file not found: {path}");
            return path;
        }

        /// <summary>
        /// Gets financial sample files (McDonald's, Yahoo)
        /// </summary>
        public string GetMcDonaldsFinnancialFilePath()
        {
            var path = Path.Combine(_sampleDataPath, "financials", "McDonalds-financials.csv");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Sample file not found: {path}");
            return path;
        }

        /// <summary>
        /// Reads a CSV file and returns its content as lines.
        /// </summary>
        public string[] ReadCsvLines(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            return File.ReadAllLines(filePath);
        }

        /// <summary>
        /// Reads a CSV file and returns parsed data rows (skipping header).
        /// Each row is represented as a dictionary: column name -> cell value
        /// </summary>
        public List<Dictionary<string, string>> ReadCsvAsRecords(string filePath)
        {
            var lines = ReadCsvLines(filePath);
            if (lines.Length == 0)
                return new List<Dictionary<string, string>>();

            var headers = ParseCsvLine(lines[0]);
            var records = new List<Dictionary<string, string>>();

            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCsvLine(lines[i]);
                var record = new Dictionary<string, string>();

                for (int j = 0; j < headers.Count && j < values.Count; j++)
                {
                    record[headers[j]] = values[j];
                }

                records.Add(record);
            }

            return records;
        }

        /// <summary>
        /// Simple CSV line parser that handles quoted values.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString().Trim('"'));
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString().Trim('"'));
            return values;
        }

        /// <summary>
        /// Finds the project root directory by searching for .sln or .csproj files.
        /// </summary>
        private static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "samples")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            // Fallback: use current directory
            return Directory.GetCurrentDirectory();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // No unmanaged resources to clean up
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
