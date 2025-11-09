using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SheetAtlas.Core.Shared.Helpers
{
    /// <summary>
    /// Utility class for file path operations and naming conventions
    /// Used for generating consistent folder names for file logging
    /// </summary>
    public static partial class FilePathHelper
    {
        /// <summary>
        /// Generates a folder name for file logging based on filename and path hash
        /// Pattern: {sanitized-name}-{hash-6char}/
        /// </summary>
        /// <param name="filePath">Full path to the Excel file</param>
        /// <returns>Folder name (e.g., "report-2024-xlsx-a3f912")</returns>
        public static string GenerateLogFolderName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            var fileName = Path.GetFileName(filePath);
            var sanitized = SanitizeFileName(fileName);
            var pathHash = ComputePathHash(filePath);

            return $"{sanitized}-{pathHash}";
        }

        /// <summary>
        /// Sanitizes a filename for use in folder names
        /// Rules: lowercase, spaces to hyphens, remove special chars, remove extension
        /// </summary>
        /// <param name="fileName">Original filename (e.g., "Report Q4 2024.xlsx")</param>
        /// <returns>Sanitized name (e.g., "report-q4-2024-xlsx")</returns>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "unknown-file";

            // Remove extension
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // Convert to lowercase
            var sanitized = nameWithoutExt.ToLowerInvariant();

            // Replace spaces with hyphens
            sanitized = sanitized.Replace(' ', '-');

            // Remove special characters (keep only letters, digits, hyphens, underscores)
            sanitized = MyRegex().Replace(sanitized, "");

            // Remove multiple consecutive hyphens
            sanitized = Regex.Replace(sanitized, @"-+", "-");

            // Trim hyphens from start/end
            sanitized = sanitized.Trim('-');

            // If empty after sanitization, use fallback
            if (string.IsNullOrEmpty(sanitized))
                sanitized = "file";

            // Append original extension indicator
            var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext))
                sanitized = $"{sanitized}-{ext}";

            return sanitized;
        }

        /// <summary>
        /// Computes a short hash (6 characters) from the file path using MD5
        /// Used to ensure folder name uniqueness for files with same name in different locations
        /// </summary>
        /// <param name="filePath">Full file path</param>
        /// <returns>6-character hash (e.g., "a3f912")</returns>
        public static string ComputePathHash(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            // Normalize path for consistent hashing (handle different separators)
            var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();

            var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(normalizedPath));
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            // Return first 6 characters (16 million combinations)
            return hashString.Substring(0, 6);
        }

        /// <summary>
        /// Computes MD5 hash of file content
        /// Used to detect if file has been modified
        /// </summary>
        /// <param name="filePath">Path to file</param>
        /// <returns>Full MD5 hash with "md5:" prefix</returns>
        public static string ComputeFileHash(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = MD5.HashData(stream);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                return $"md5:{hashString}";
            }
        }

        /// <summary>
        /// Generates a timestamp-based filename for log entries
        /// Pattern: yyyyMMdd_HHmmss.json
        /// </summary>
        /// <param name="timestamp">Timestamp to use</param>
        /// <returns>Filename (e.g., "20251018_142315.json")</returns>
        public static string GenerateLogFileName(DateTime timestamp)
        {
            return $"{timestamp:yyyyMMdd_HHmmss}.json";
        }

        [GeneratedRegex(@"[^a-z0-9\-_]")]
        private static partial Regex MyRegex();
    }
}
