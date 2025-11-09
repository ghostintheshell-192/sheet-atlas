using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Custom JSON converter for ExcelError
    /// Handles serialization of Exception property by extracting only serializable info
    /// </summary>
    public class ExcelErrorJsonConverter : JsonConverter<ExcelError>
    {
        public override ExcelError Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject token");

            // Variables to store parsed values
            Logging.Models.LogSeverity severity = Logging.Models.LogSeverity.Info;
            string message = string.Empty;
            string context = string.Empty;
            CellReference? location = null;
            Exception? exception = null;
            DateTime timestamp = DateTime.Now; // Default to now if not in JSON

            // Temporary variables for location and exception parsing
            string? locationSheet = null;
            string? locationCell = null;
            string? exceptionType = null;
            string? exceptionMessage = null;
            string? exceptionStackTrace = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                string propertyName = reader.GetString()!;
                reader.Read(); // Move to value

                switch (propertyName.ToLowerInvariant())
                {
                    case "severity":
                        if (Enum.TryParse<Logging.Models.LogSeverity>(reader.GetString(), true, out var parsedSeverity))
                            severity = parsedSeverity;
                        break;

                    case "message":
                        message = reader.GetString() ?? string.Empty;
                        break;

                    case "context":
                        context = reader.GetString() ?? string.Empty;
                        break;

                    case "timestamp":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            if (DateTime.TryParse(reader.GetString(), out var parsedTimestamp))
                                timestamp = parsedTimestamp;
                        }
                        break;

                    case "location":
                        // Parse nested location object
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType == JsonTokenType.PropertyName)
                                {
                                    string locProp = reader.GetString()!.ToLowerInvariant();
                                    reader.Read();

                                    if (locProp == "sheet")
                                        locationSheet = reader.GetString();
                                    else if (locProp == "cell")
                                        locationCell = reader.GetString();
                                }
                            }
                        }
                        break;

                    case "exception":
                        // Parse nested exception object
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType == JsonTokenType.PropertyName)
                                {
                                    string exProp = reader.GetString()!.ToLowerInvariant();
                                    reader.Read();

                                    if (exProp == "type")
                                        exceptionType = reader.GetString();
                                    else if (exProp == "message")
                                        exceptionMessage = reader.GetString();
                                    else if (exProp == "stacktrace")
                                        exceptionStackTrace = reader.GetString();
                                }
                            }
                        }
                        break;

                    // Ignore other fields (id, code, timestamp, isRecoverable)
                    default:
                        reader.Skip();
                        break;
                }
            }

            // Reconstruct CellReference if we have location data
            if (!string.IsNullOrEmpty(locationCell))
            {
                location = ParseCellReference(locationCell);
            }

            // Reconstruct Exception if we have exception data
            if (!string.IsNullOrEmpty(exceptionMessage))
            {
                // Create a generic exception with the stored info
                // We can't recreate the original exception type, so use a wrapper
                exception = new InvalidOperationException(
                    $"[{exceptionType ?? "Unknown"}] {exceptionMessage}");
            }

            // Use FromJson factory method to preserve timestamp
            return ExcelError.FromJson(severity, message, context, timestamp, location, exception);
        }

        private CellReference? ParseCellReference(string cellNotation)
        {
            // Parse Excel notation like "A1", "B2", etc.
            // Returns null for invalid formats (validation handled explicitly below)
            if (string.IsNullOrEmpty(cellNotation))
                return null;

            // Extract column letters and row number
            int i = 0;
            while (i < cellNotation.Length && char.IsLetter(cellNotation[i]))
                i++;

            if (i == 0 || i == cellNotation.Length)
                return null;

            string columnLetters = cellNotation.Substring(0, i);
            string rowString = cellNotation.Substring(i);

            if (!int.TryParse(rowString, out int row))
                return null;

            // Convert column letters to 0-based index (A=0, B=1, Z=25, AA=26, etc.)
            int column = 0;
            for (int j = 0; j < columnLetters.Length; j++)
            {
                column = column * 26 + (char.ToUpperInvariant(columnLetters[j]) - 'A');
            }

            // Convert Excel 1-based row to 0-based absolute index
            return new CellReference(row - 1, column);
        }

        public override void Write(Utf8JsonWriter writer, ExcelError value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Write simple properties
            writer.WriteString("id", Guid.NewGuid().ToString());
            writer.WriteString("timestamp", value.Timestamp);
            writer.WriteString("severity", value.Level.ToString());
            writer.WriteString("code", GetErrorCode(value));
            writer.WriteString("message", value.Message);
            writer.WriteString("context", value.Context);

            // Write location if present
            if (value.Location != null)
            {
                writer.WritePropertyName("location");
                writer.WriteStartObject();

                // Extract sheet name from context if available
                var sheetName = ExtractSheetName(value.Context);
                if (!string.IsNullOrEmpty(sheetName))
                {
                    writer.WriteString("sheet", sheetName);
                }

                writer.WriteString("cell", value.Location.ToExcelNotation());

                if (!string.IsNullOrEmpty(sheetName))
                {
                    writer.WriteString("cellReference", $"{sheetName}!{value.Location.ToExcelNotation()}");
                }
                else
                {
                    writer.WriteString("cellReference", value.Location.ToExcelNotation());
                }

                writer.WriteEndObject();
            }

            // Write exception info if present (extract only serializable parts)
            if (value.InnerException != null)
            {
                writer.WritePropertyName("exception");
                writer.WriteStartObject();
                writer.WriteString("type", value.InnerException.GetType().FullName);
                writer.WriteString("message", value.InnerException.Message);
                if (!string.IsNullOrEmpty(value.InnerException.StackTrace))
                {
                    writer.WriteString("stackTrace", value.InnerException.StackTrace);
                }
                writer.WriteEndObject();
            }

            // Write isRecoverable (based on exception type)
            writer.WriteBoolean("isRecoverable", IsRecoverable(value.InnerException));

            writer.WriteEndObject();
        }

        private string GetErrorCode(ExcelError error)
        {
            // Generate error code based on context category
            // Simple categorization: FILE, SHEET, CELL
            if (error.Context.StartsWith("File", StringComparison.OrdinalIgnoreCase))
                return "FILE";
            if (error.Context.StartsWith("Sheet", StringComparison.OrdinalIgnoreCase))
                return "SHEET";
            if (error.Context.StartsWith("Cell", StringComparison.OrdinalIgnoreCase))
                return "CELL";

            return "UNKNOWN";
        }

        private string? ExtractSheetName(string context)
        {
            // Context format: "Sheet:SheetName" or "Cell:SheetName"
            if (string.IsNullOrEmpty(context))
                return null;

            var parts = context.Split(':', 2);
            if (parts.Length == 2)
            {
                return parts[1];
            }

            return null;
        }

        private bool IsRecoverable(Exception? exception)
        {
            // Delegate to centralized exception recovery logic
            // This ensures consistency across the application
            return ExceptionHandler.IsRecoverableException(exception);
        }
    }
}
