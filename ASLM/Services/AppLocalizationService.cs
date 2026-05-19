// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;

namespace ASLM.Services
{
    // Host app localization

    /// <summary>
    /// Exposes the active UI language for ASLM and builds module locale payloads.
    /// String resources are not wired yet; only language selection is persisted.
    /// </summary>
    public sealed class AppLocalizationService
    {
        private readonly AppDataStore _appData;

        /// <summary>
        /// Languages available in personalization settings.
        /// </summary>
        public static IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
        [
            new LanguageOption("en", "English")
        ];

        /// <summary>
        /// Creates the localization service.
        /// </summary>
        public AppLocalizationService(AppDataStore appData)
        {
            _appData = appData;
        }

        /// <summary>
        /// Returns the normalized language code from persisted personalization.
        /// </summary>
        public string GetCurrentLanguage() =>
            AppPersonalizationConfig.NormalizeLanguage(_appData.Data.Personalization.Language);

        /// <summary>
        /// Returns the display name for a language code, or the code itself when unknown.
        /// </summary>
        public static string GetDisplayName(string languageCode)
        {
            var normalized = AppPersonalizationConfig.NormalizeLanguage(languageCode);
            foreach (var option in SupportedLanguages)
            {
                if (string.Equals(option.Id, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return option.DisplayName;
                }
            }

            return normalized;
        }
    }

    /// <summary>
    /// One selectable UI language in personalization settings.
    /// </summary>
    public sealed record LanguageOption(string Id, string DisplayName);
}
