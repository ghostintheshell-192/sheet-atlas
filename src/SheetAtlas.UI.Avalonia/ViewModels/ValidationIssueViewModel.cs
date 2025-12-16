using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel for displaying validation issues in the UI.
/// </summary>
public class ValidationIssueViewModel
{
    private readonly ValidationIssue _issue;

    public ValidationIssueViewModel(ValidationIssue issue)
    {
        _issue = issue ?? throw new ArgumentNullException(nameof(issue));
    }

    /// <summary>
    /// Icon representing severity level.
    /// </summary>
    public string SeverityIcon => _issue.Severity switch
    {
        ValidationSeverity.Critical => "\u26D4", // No entry
        ValidationSeverity.Error => "\u274C",    // Red X
        ValidationSeverity.Warning => "\u26A0",  // Warning triangle
        ValidationSeverity.Info => "\u2139",     // Info
        _ => "\u2022"                            // Bullet
    };

    /// <summary>
    /// Location description (column name or cell reference).
    /// </summary>
    public string Location => _issue.LocationDescription;

    /// <summary>
    /// Issue message.
    /// </summary>
    public string Message => _issue.Message;

    /// <summary>
    /// Severity level for sorting/filtering.
    /// </summary>
    public ValidationSeverity Severity => _issue.Severity;
}
