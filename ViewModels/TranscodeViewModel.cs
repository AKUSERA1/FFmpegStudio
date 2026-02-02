using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using FFmpegStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace FFmpegStudio.ViewModels
{
    public class TranscodeViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;
        private readonly Services.FFmpegService _ffmpegService;

        private string _sourceFilePath = string.Empty;
        private string _selectedFormat = "MP4";
        private string _selectedCodecCategory = "H.264";
        private string _selectedEncoder = "libx264";
        private string _resolution = "原始";
        private string _bitrate = "原始";
        private string _frameRate = "原始";
        private string _colorSpace = "BT.709";
        private bool _isCommandManuallyEdited = false;
        private VideoInfo? _videoInfo;
        private bool _isLoadingVideoInfo;
        private bool _isLoadingEncoders;
        private string _ffmpegCommand = string.Empty;
        private string _selectedQualityPreset = "medium";
        private string _qualityPresetParams = string.Empty;

        public TranscodeViewModel()
        {
            _settingsService = Services.SettingsService.Instance;
            _ffmpegService = Services.FFmpegService.Instance;
            BrowseCommand = new RelayCommand(async _ => await BrowseFileAsync());
            _ = LoadEncodersAsync();
        }

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set
            {
                if (SetProperty(ref _sourceFilePath, value))
                {
                    OnPropertyChanged(nameof(HasSourceFile));
                    _ = LoadVideoInfoAsync();
                    if (!_isCommandManuallyEdited)
                    {
                        // 源文件改变时，清空并重新构造命令
                        ConstructFFmpegCommand(true);
                    }
                }
            }
        }

        public VideoInfo? VideoInfo
        {
            get => _videoInfo;
            set
            {
                if (SetProperty(ref _videoInfo, value))
                {
                    OnPropertyChanged(nameof(HasVideoInfo));
                }
            }
        }

        public bool IsLoadingVideoInfo
        {
            get => _isLoadingVideoInfo;
            set => SetProperty(ref _isLoadingVideoInfo, value);
        }

        public bool IsLoadingEncoders
        {
            get => _isLoadingEncoders;
            set => SetProperty(ref _isLoadingEncoders, value);
        }

        public bool HasSourceFile => !string.IsNullOrEmpty(_sourceFilePath);
        public bool HasVideoInfo => VideoInfo != null;

        public string SelectedFormat
        {
            get => _selectedFormat;
            set
            {
                if (SetProperty(ref _selectedFormat, value) && !_isCommandManuallyEdited)
                {
                    // 容器改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string SelectedCodecCategory
        {
            get => _selectedCodecCategory;
            set
            {
                if (SetProperty(ref _selectedCodecCategory, value))
                {
                    _ = LoadEncodersAsync();
                }
            }
        }

        public string SelectedEncoder
        {
            get => _selectedEncoder;
            set
            {
                if (SetProperty(ref _selectedEncoder, value))
                {
                    _ = LoadQualityPresetParamsAndConstructCommandAsync();
                }
            }
        }

        public string Resolution
        {
            get => _resolution;
            set
            {
                if (SetProperty(ref _resolution, value) && !_isCommandManuallyEdited)
                {
                    // 分辨率改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string Bitrate
        {
            get => _bitrate;
            set
            {
                if (SetProperty(ref _bitrate, value) && !_isCommandManuallyEdited)
                {
                    // 比特率改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string FrameRate
        {
            get => _frameRate;
            set
            {
                if (SetProperty(ref _frameRate, value) && !_isCommandManuallyEdited)
                {
                    // 帧率改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public bool ShowAdvanced
        {
            get => _settingsService.ShowAdvancedFeatures;
            set
            {
                _settingsService.ShowAdvancedFeatures = value;
                OnPropertyChanged(nameof(ShowAdvanced));
                if (!_isCommandManuallyEdited)
                {
                    // 高级开关改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string ColorSpace
        {
            get => _colorSpace;
            set
            {
                if (SetProperty(ref _colorSpace, value) && !_isCommandManuallyEdited)
                {
                    // 色彩空间改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string SelectedQualityPreset
        {
            get => _selectedQualityPreset;
            set
            {
                if (SetProperty(ref _selectedQualityPreset, value))
                {
                    _ = LoadQualityPresetParamsAndConstructCommandAsync();
                }
            }
        }

        public string FFmpegCommand
        {
            get => _ffmpegCommand;
            set
            {
                if (SetProperty(ref _ffmpegCommand, value))
                {
                    // 用户手动编辑了命令
                    _isCommandManuallyEdited = true;
                    // 更新高级参数为"自定义"
                    _resolution = "自定义";
                    _bitrate = "自定义";
                    _frameRate = "自定义";
                    _colorSpace = "自定义";
                    OnPropertyChanged(nameof(Resolution));
                    OnPropertyChanged(nameof(Bitrate));
                    OnPropertyChanged(nameof(FrameRate));
                    OnPropertyChanged(nameof(ColorSpace));
                }
            }
        }

        public string QualityPresetParams
        {
            get => _qualityPresetParams;
            set => SetProperty(ref _qualityPresetParams, value);
        }

        public ObservableCollection<string> QualityPresets { get; } = new() { "high", "medium", "low" };

        public ObservableCollection<string> Formats { get; } = new() { "MP4", "MKV", "AVI", "MOV", "FLV", "WebM" };

        public ObservableCollection<string> CodecCategories { get; } = new()
        {
            "H.264",
            "HEVC",
            "AV1",
            "MPEG4",
            "QuickTime",
            "VP9"
        };

        public ObservableCollection<string> Encoders { get; } = new();

        public ObservableCollection<string> Resolutions { get; } = new() { "原始","1920x1080", "1080x1920", "2560x1440", "1440x2560" };

        public ObservableCollection<string> FrameRates { get; } = new() { "原始","25", "30", "60" };

        public ObservableCollection<string> ColorSpaces { get; } = new() { "BT.709", "BT.601", "BT.2020" };

        public ObservableCollection<TranscodeTask> Tasks { get; } = new();

        public ICommand BrowseCommand { get; }

        private async Task LoadEncodersAsync()
        {
            IsLoadingEncoders = true;
            try
            {
                var encoders = await _ffmpegService.GetEncodersByCodecCategoryAsync(SelectedCodecCategory);
                Encoders.Clear();
                foreach (var encoder in encoders)
                {
                    Encoders.Add(encoder);
                }
                // 设置默认编码器
                if (Encoders.Count > 0)
                {
                    SelectedEncoder = Encoders[0];
                    _ = LoadQualityPresetParamsAsync();
                }
            }
            finally
            {
                IsLoadingEncoders = false;
            }
        }

        private async Task LoadQualityPresetParamsAndConstructCommandAsync()
        {
            if (string.IsNullOrEmpty(SelectedEncoder))
                return;

            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "QualityPresets.json");
                if (!File.Exists(jsonPath))
                {
                    ShowErrorMessage("QualityPresets.json 文件不存在");
                    _qualityPresetParams = string.Empty;
                    ConstructFFmpegCommand(true);
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var jsonDoc = JsonDocument.Parse(jsonContent);

                if (!jsonDoc.RootElement.TryGetProperty(SelectedEncoder, out var encoderElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 没有对应的质量预设");
                    _qualityPresetParams = string.Empty;
                    ConstructFFmpegCommand(true);
                    return;
                }

                if (!encoderElement.TryGetProperty("presets", out var presetsElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 的预设配置不完整");
                    _qualityPresetParams = string.Empty;
                    ConstructFFmpegCommand(true);
                    return;
                }

                if (!presetsElement.TryGetProperty(SelectedQualityPreset, out var presetElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 没有 {SelectedQualityPreset} 质量预设");
                    _qualityPresetParams = string.Empty;
                    ConstructFFmpegCommand(true);
                    return;
                }

                _qualityPresetParams = presetElement.GetString() ?? string.Empty;
                OnPropertyChanged(nameof(QualityPresetParams));
                
                if (!_isCommandManuallyEdited)
                {
                    ConstructFFmpegCommand(true);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"读取质量预设时出错: {ex.Message}");
                _qualityPresetParams = string.Empty;
                if (!_isCommandManuallyEdited)
                {
                    ConstructFFmpegCommand(true);
                }
            }
        }

        private async Task LoadQualityPresetParamsAsync()
        {
            if (string.IsNullOrEmpty(SelectedEncoder))
                return;

            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "QualityPresets.json");
                if (!File.Exists(jsonPath))
                {
                    ShowErrorMessage("QualityPresets.json 文件不存在");
                    _qualityPresetParams = string.Empty;
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var jsonDoc = JsonDocument.Parse(jsonContent);

                if (!jsonDoc.RootElement.TryGetProperty(SelectedEncoder, out var encoderElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 没有对应的质量预设");
                    _qualityPresetParams = string.Empty;
                    return;
                }

                if (!encoderElement.TryGetProperty("presets", out var presetsElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 的预设配置不完整");
                    _qualityPresetParams = string.Empty;
                    return;
                }

                if (!presetsElement.TryGetProperty(SelectedQualityPreset, out var presetElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 没有 {SelectedQualityPreset} 质量预设");
                    _qualityPresetParams = string.Empty;
                    return;
                }

                _qualityPresetParams = presetElement.GetString() ?? string.Empty;
                OnPropertyChanged(nameof(QualityPresetParams));
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"读取质量预设时出错: {ex.Message}");
                _qualityPresetParams = string.Empty;
            }
        }

        private void ShowErrorMessage(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        private void ConstructFFmpegCommand(bool rebuildFromScratch)
        {
            if (string.IsNullOrEmpty(SourceFilePath))
            {
                _ffmpegCommand = string.Empty;
                OnPropertyChanged(nameof(FFmpegCommand));
                return;
            }

            try
            {
                var outputPath = GetOutputFilePath();
                var commandParts = new List<string>();

                // 基础命令部分
                commandParts.AddRange(new[] { "ffmpeg", "-i", $"\"{SourceFilePath}\"" });

                // 视频编码参数
                AddVideoEncodingParameters(commandParts);

                // 音频编码参数
                commandParts.AddRange(new[] { "-c:a", "copy" });

                // 输出路径
                commandParts.Add($"\"{outputPath}\"");

                _ffmpegCommand = string.Join(' ', commandParts);
                OnPropertyChanged(nameof(FFmpegCommand));
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"构造FFmpeg命令时出错: {ex.Message}");
                _ffmpegCommand = string.Empty;
                OnPropertyChanged(nameof(FFmpegCommand));
            }
        }

        private string GetOutputFilePath()
        {
            var outputPath = Path.ChangeExtension(SourceFilePath, "." + SelectedFormat.ToLower());
            
            // 避免输出路径与源文件相同
            if (outputPath == SourceFilePath)
            {
                var directory = Path.GetDirectoryName(SourceFilePath) ?? string.Empty;
                var fileName = Path.GetFileNameWithoutExtension(SourceFilePath);
                outputPath = Path.Combine(directory, $"{fileName}_output.{SelectedFormat.ToLower()}");
            }
            
            return outputPath;
        }

        private void AddVideoEncodingParameters(List<string> commandParts)
        {
            // 视频编码器
            commandParts.AddRange(new[] { "-c:v", SelectedEncoder });

            // 质量预设参数
            if (!string.IsNullOrEmpty(_qualityPresetParams))
            {
                var presetParams = _qualityPresetParams.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                commandParts.AddRange(presetParams);
            }

            // 高级参数
            if (ShowAdvanced)
            {
                AddAdvancedParameters(commandParts);
            }
        }

        private void AddAdvancedParameters(List<string> commandParts)
        {
            // 分辨率
            if (_resolution != "原始" && !string.IsNullOrEmpty(_resolution))
            {
                commandParts.AddRange(new[] { "-s", _resolution });
            }

            // 比特率
            if (_bitrate != "原始" && !string.IsNullOrEmpty(_bitrate))
            {
                commandParts.AddRange(new[] { "-b:v", $"{_bitrate}k" });
            }

            // 帧率
            if (_frameRate != "原始" && !string.IsNullOrEmpty(_frameRate))
            {
                commandParts.AddRange(new[] { "-r", _frameRate });
            }

            // 色彩空间
            if (!string.IsNullOrEmpty(_colorSpace) && _colorSpace != "BT.709")
            {
                commandParts.AddRange(new[] { "-colorspace", _colorSpace });
            }
        }

        private async Task BrowseFileAsync()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mkv");
            picker.FileTypeFilter.Add(".avi");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".flv");
            picker.FileTypeFilter.Add(".webm");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".m4v");
            picker.FileTypeFilter.Add(".mpeg");
            picker.FileTypeFilter.Add(".mpg");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                SourceFilePath = file.Path;
            }
        }

        private async Task LoadVideoInfoAsync()
        {
            if (string.IsNullOrEmpty(SourceFilePath))
            {
                VideoInfo = null;
                return;
            }

            IsLoadingVideoInfo = true;
            try
            {
                VideoInfo = await _ffmpegService.GetVideoInfoAsync(SourceFilePath);
            }
            finally
            {
                IsLoadingVideoInfo = false;
            }
        }

        public TranscodeViewModel(Services.SettingsService settingsService) : this()
        {
            _settingsService = settingsService;
            LoadMockTasks();
        }

        private void LoadMockTasks()
        {
            Tasks.Add(new TranscodeTask
            {
                Id = "1",
                SourceFile = "video1.mp4",
                OutputFile = "video1_transcoded.mkv",
                Status = "完成",
                Progress = 100,
                CreateTime = DateTime.Now.AddHours(-2)
            });

            Tasks.Add(new TranscodeTask
            {
                Id = "2",
                SourceFile = "video2.avi",
                OutputFile = "video2_transcoded.mp4",
                Status = "转换中",
                Progress = 45,
                CreateTime = DateTime.Now.AddMinutes(-30)
            });
        }
    }
}
