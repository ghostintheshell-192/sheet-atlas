using System.Text.Json.Serialization;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Core.Application.Json
{
    /// <summary>
    /// JSON serialization context for source-generated serializers.
    /// Required for PublishTrimmed=true support (AOT and trimming).
    /// All types used with JsonSerializer must be registered here.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = [
            typeof(JsonStringEnumConverter<ThemePreference>),
            typeof(JsonStringEnumConverter<ExportFormat>),
            typeof(JsonStringEnumConverter<NamingPattern>),
            typeof(JsonStringEnumConverter<DataType>),
            typeof(JsonStringEnumConverter<RuleType>),
            typeof(JsonStringEnumConverter<ValidationSeverity>),
            typeof(JsonStringEnumConverter<CellDataType>)
        ]
    )]
    [JsonSerializable(typeof(UserSettings))]
    [JsonSerializable(typeof(AppearanceSettings))]
    [JsonSerializable(typeof(DataProcessingSettings))]
    [JsonSerializable(typeof(FileLocationSettings))]
    [JsonSerializable(typeof(ThemePreference))]
    [JsonSerializable(typeof(ExportFormat))]
    [JsonSerializable(typeof(NamingPattern))]
    [JsonSerializable(typeof(ExcelTemplate))]
    [JsonSerializable(typeof(ExpectedColumn))]
    [JsonSerializable(typeof(ValidationRule))]
    [JsonSerializable(typeof(DataType))]
    [JsonSerializable(typeof(CellDataType))]
    [JsonSerializable(typeof(RuleType))]
    [JsonSerializable(typeof(ValidationSeverity))]
    [JsonSerializable(typeof(FileLogEntry))]
    [JsonSerializable(typeof(FileInfoDto))]
    [JsonSerializable(typeof(LoadAttemptInfo))]
    [JsonSerializable(typeof(ErrorSummary))]
    [JsonSerializable(typeof(ExcelError))]
    [JsonSerializable(typeof(List<ExpectedColumn>))]
    [JsonSerializable(typeof(List<ValidationRule>))]
    [JsonSerializable(typeof(List<ExcelError>))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(Dictionary<string, object?>))]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
