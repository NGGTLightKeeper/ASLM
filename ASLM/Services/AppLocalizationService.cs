// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Globalization;
using ASLM.Localization;
using ASLM.Models;
using ASLM.Resources.Strings;

namespace ASLM.Services
{
    // Host app localization

    /// <summary>
    /// Exposes the active UI language for ASLM, applies culture to RESX resources,
    /// and notifies <see cref="ILocalizable"/> views when the language changes.
    /// </summary>
    public sealed class AppLocalizationService
    {
        private readonly AppDataStore _appData;
        private readonly List<WeakReference<ILocalizable>> _localizableViews = [];
        private readonly object _localizableLock = new();
        private string _appliedLanguage = "en";

        /// <summary>
        /// Languages available in personalization settings.
        /// </summary>
        public static IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
        [
            new LanguageOption("en", "English")
        ];

        /// <summary>
        /// Raised after <see cref="ApplyCulture"/> updates thread culture and resources.
        /// </summary>
        public event EventHandler? CultureChanged;

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
        /// Applies the persisted language to UI culture and RESX resources.
        /// </summary>
        /// <returns>True when the culture actually changed.</returns>
        public bool ApplyCulture()
        {
            var language = GetCurrentLanguage();
            if (string.Equals(_appliedLanguage, language, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, language, StringComparison.OrdinalIgnoreCase))
            {
                AppResources.Culture = CultureInfo.CurrentUICulture;
                return false;
            }

            var culture = CreateCulture(language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            AppResources.Culture = culture;
            _appliedLanguage = language;

            NotifyLocalizableViews();
            CultureChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Returns a localized string for the given resource key.
        /// </summary>
        public string GetString(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            return AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key;
        }

        /// <summary>
        /// Returns a formatted localized string.
        /// </summary>
        public string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            return args.Length == 0 ? format : string.Format(format, args);
        }

        /// <summary>
        /// Registers a view that should refresh when the active culture changes.
        /// </summary>
        public void Register(ILocalizable view)
        {
            if (view == null)
            {
                return;
            }

            lock (_localizableLock)
            {
                PruneLocalizableViews();
                if (_localizableViews.All(reference => !reference.TryGetTarget(out var target) || !ReferenceEquals(target, view)))
                {
                    _localizableViews.Add(new WeakReference<ILocalizable>(view));
                }
            }
        }

        /// <summary>
        /// Removes a previously registered view.
        /// </summary>
        public void Unregister(ILocalizable view)
        {
            if (view == null)
            {
                return;
            }

            lock (_localizableLock)
            {
                _localizableViews.RemoveAll(reference =>
                    !reference.TryGetTarget(out var target) || ReferenceEquals(target, view));
            }
        }

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
                    return GetLanguageDisplayNameKey(normalized) is { } key
                        ? AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? option.DisplayName
                        : option.DisplayName;
                }
            }

            return normalized;
        }

        private static string? GetLanguageDisplayNameKey(string languageCode) =>
            string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase)
                ? LocalizationKeys.Settings_Language_English
                : null;

        private static CultureInfo CreateCulture(string language)
        {
            try
            {
                return CultureInfo.GetCultureInfo(language);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en");
            }
        }

        private void NotifyLocalizableViews()
        {
            List<ILocalizable> targets;
            lock (_localizableLock)
            {
                PruneLocalizableViews();
                targets = _localizableViews
                    .Select(reference =>
                    {
                        reference.TryGetTarget(out var target);
                        return target;
                    })
                    .Where(target => target != null)
                    .Cast<ILocalizable>()
                    .ToList();
            }

            foreach (var target in targets)
            {
                try
                {
                    target.ApplyLocalization();
                }
                catch
                {
                    // Best-effort refresh for each registered view.
                }
            }
        }

        private void PruneLocalizableViews()
        {
            _localizableViews.RemoveAll(reference => !reference.TryGetTarget(out _));
        }
    }

    /// <summary>
    /// One selectable UI language in personalization settings.
    /// </summary>
    public sealed record LanguageOption(string Id, string DisplayName);
}
