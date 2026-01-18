using SheetAtlas.Core.Application.DTOs;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Service for managing user preferences with persistent storage.
    /// Settings are stored as JSON in the user's application data folder.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the currently loaded user settings.
        /// Returns defaults if no settings file exists or if it's corrupted.
        /// </summary>
        UserSettings Current { get; }

        /// <summary>
        /// Loads settings from disk asynchronously.
        /// Creates default settings if file doesn't exist or is corrupted.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Loaded or default user settings</returns>
        Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves settings to disk asynchronously.
        /// Creates the settings directory if it doesn't exist.
        /// </summary>
        /// <param name="settings">Settings to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets settings to defaults and saves to disk.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the full path to the settings file.
        /// </summary>
        /// <returns>Path to settings.json</returns>
        string GetSettingsFilePath();
    }
}
