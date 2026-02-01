using System;
using System.IO;
using System.Threading.Tasks;
using FFmpegStudio.Services;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Controls;

namespace FFmpegStudio.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly FFmpegService _ffmpegService;

        private string _ffmpegPath = string.Empty;
        private bool _useWinget;
        private string _ffmpegStatus = "未检测";
        private bool _isFFmpegInstalled;
        private bool _isInstallingFFmpeg;
        private string _installationStatus = string.Empty;
        private bool _showInstallationStatus;

        public SettingsViewModel()
        {
            _settingsService = SettingsService.Instance;
            _ffmpegService = FFmpegService.Instance;
            BrowseFFmpegCommand = new RelayCommand(_ => BrowseFFmpegAsync());
            InstallFFmpegWithWinGetCommand = new RelayCommand(_ => InstallFFmpegAsync());
            LoadSettings();
        }

        private void LoadSettings()
        {
            _ffmpegPath = _settingsService.FFmpegPath;
            _useWinget = _settingsService.UseWinget;
            CheckFFmpegInstallationAsync();
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
                    CheckFFmpegInstallationAsync();
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

        public bool IsFFmpegInstalled
        {
            get => _isFFmpegInstalled;
            set => SetProperty(ref _isFFmpegInstalled, value);
        }

        public bool IsInstallingFFmpeg
        {
            get => _isInstallingFFmpeg;
            set => SetProperty(ref _isInstallingFFmpeg, value);
        }

        public string InstallationStatus
        {
            get => _installationStatus;
            set => SetProperty(ref _installationStatus, value);
        }

        public bool ShowInstallationStatus
        {
            get => _showInstallationStatus;
            set => SetProperty(ref _showInstallationStatus, value);
        }

        public RelayCommand BrowseFFmpegCommand { get; }
        public RelayCommand InstallFFmpegWithWinGetCommand { get; }
        public RelayCommand SaveSettingsCommand { get; }

        private async void BrowseFFmpegAsync()
        {
            try
            {
                var openPicker = new FileOpenPicker();
                openPicker.FileTypeFilter.Add(".exe");
                openPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

                // Get the main window handle
                var mainWindow = (Microsoft.UI.Xaml.Application.Current as App)?.Window;
                if (mainWindow != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
                }

                var file = await openPicker.PickSingleFileAsync();
                if (file != null && file.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    FFmpegPath = file.Path;
                }
                else if (file != null)
                {
                    ShowInstallationStatus = true;
                    InstallationStatus = "错误: 请选择 ffmpeg.exe 文件";
                }
            }
            catch (Exception ex)
            {
                ShowInstallationStatus = true;
                InstallationStatus = $"错误: {ex.Message}";
            }
        }

        private async void CheckFFmpegInstallationAsync()
        {
            try
            {
                var versionInfo = await _ffmpegService.GetFFmpegVersionAsync();
                IsFFmpegInstalled = versionInfo.IsInstalled;
                FFmpegStatus = versionInfo.IsInstalled ? $"已安装 - 版本 {versionInfo.Version}" : "未检测";
            }
            catch
            {
                IsFFmpegInstalled = false;
                FFmpegStatus = "未检测";
            }
        }

        private async void InstallFFmpegAsync()
        {
            IsInstallingFFmpeg = true;
            ShowInstallationStatus = true;
            InstallationStatus = "正在安装 FFmpeg...";

            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"winget install FFmpeg -e\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        await Task.Run(() => process.WaitForExit());
                        
                        // Check if installation was successful
                        await Task.Delay(1000);
                        CheckFFmpegInstallationAsync();

                        if (IsFFmpegInstalled)
                        {
                            InstallationStatus = "FFmpeg 安装成功";
                        }
                        else
                        {
                            InstallationStatus = "FFmpeg 安装可能失败，请检查";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InstallationStatus = $"安装失败: {ex.Message}";
            }
            finally
            {
                IsInstallingFFmpeg = false;
            }
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
