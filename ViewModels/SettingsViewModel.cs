using System;

namespace FFmpegStudio.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private string _ffmpegPath = string.Empty;
        private bool _useWinget;
        private bool _showAdvancedFeatures;
        private string _ffmpegStatus = "未检测";

        public string FFmpegPath
        {
            get => _ffmpegPath;
            set => SetProperty(ref _ffmpegPath, value);
        }

        public bool UseWinget
        {
            get => _useWinget;
            set => SetProperty(ref _useWinget, value);
        }

        public bool ShowAdvancedFeatures
        {
            get => _showAdvancedFeatures;
            set => SetProperty(ref _showAdvancedFeatures, value);
        }

        public string FFmpegStatus
        {
            get => _ffmpegStatus;
            set => SetProperty(ref _ffmpegStatus, value);
        }

        public RelayCommand BrowseFFmpegCommand { get; }
        public RelayCommand InstallFFmpegCommand { get; }
        public RelayCommand SaveSettingsCommand { get; }

        public SettingsViewModel()
        {
            BrowseFFmpegCommand = new RelayCommand(_ => BrowseFFmpeg());
            InstallFFmpegCommand = new RelayCommand(_ => InstallFFmpeg());
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        }

        private void BrowseFFmpeg()
        {
            // TODO: 实现文件浏览逻辑
        }

        private void InstallFFmpeg()
        {
            // TODO: 实现 winget 安装逻辑
        }

        private void SaveSettings()
        {
            // TODO: 实现设置保存逻辑
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
