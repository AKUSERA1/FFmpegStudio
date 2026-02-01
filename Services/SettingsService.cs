using Windows.Storage;

namespace FFmpegStudio.Services
{
    public class SettingsService
    {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private readonly ApplicationDataContainer _localSettings;

        private const string ShowAdvancedKey = "ShowAdvancedFeatures";

        private SettingsService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        public bool ShowAdvancedFeatures
        {
            get
            {
                if (_localSettings.Values.ContainsKey(ShowAdvancedKey))
                {
                    return (_localSettings.Values[ShowAdvancedKey] as bool?) ?? false;
                }
                return false;
            }
            set
            {
                _localSettings.Values[ShowAdvancedKey] = value;
            }
        }

        public string FFmpegPath
        {
            get => _localSettings.Values["FFmpegPath"] as string ?? string.Empty;
            set => _localSettings.Values["FFmpegPath"] = value;
        }

        public bool UseWinget
        {
            get => (_localSettings.Values["UseWinget"] as bool?) ?? false;
            set => _localSettings.Values["UseWinget"] = value;
        }
    }
}
