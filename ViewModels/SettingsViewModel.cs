using System;

namespace FFmpegStudio.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;

        private string _ffmpegPath = string.Empty;
        private bool _useWinget;
        private string _ffmpegStatus = "未检测";

        public SettingsViewModel()
        {
            _settingsService = Services.SettingsService.Instance;
            LoadSettings();
        }

        private void LoadSettings()
        {
            _ffmpegPath = _settingsService.FFmpegPath;
            _useWinget = _settingsService.UseWinget;
            OnPropertyChanged(nameof(FFmpegPath));
            OnPropertyChanged(nameof(UseWinget));
        }

        public string FFmpegPath
        {
            get => _ffmpegPath;
            set
            {
                if (SetProperty(ref _ffmpegPath, value))
                {
                    _settingsService.FFmpegPath = value;
                }
            }
        }

        public bool UseWinget
        {
            get => _useWinget;
            set
            {
                if (SetProperty(ref _useWinget, value))
                {
                    _settingsService.UseWinget = value;
                }
            }
        }

        public bool ShowAdvancedFeatures
        {
            get => _settingsService.ShowAdvancedFeatures;
            set
            {
                _settingsService.ShowAdvancedFeatures = value;
                OnPropertyChanged(nameof(ShowAdvancedFeatures));
            }
        }

        public string FFmpegStatus
        {
            get => _ffmpegStatus;
            set => SetProperty(ref _ffmpegStatus, value);
        }

        public RelayCommand BrowseFFmpegCommand { get; }
        public RelayCommand InstallFFmpegCommand { get; }
        public RelayCommand SaveSettingsCommand { get; }

        public SettingsViewModel(Services.SettingsService settingsService) : this()
        {
            _settingsService = settingsService;
            BrowseFFmpegCommand = new RelayCommand(_ => BrowseFFmpeg());
            InstallFFmpegCommand = new RelayCommand(_ => InstallFFmpeg());
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        }

        private void BrowseFFmpeg()
        {
        }

        private void InstallFFmpeg()
        {
        }

        private void SaveSettings()
        {
        }
    }

    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}
