using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FFmpegStudio.Models;
using Microsoft.UI.Xaml;
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
        private string _resolution = "1920x1080";
        private string _bitrate = "原始";
        private string _frameRate = "30";
        private string _colorSpace = "BT.709";
        private VideoInfo? _videoInfo;
        private bool _isLoadingVideoInfo;
        private bool _isLoadingEncoders;
        private string _ffmpegCommand = string.Empty;

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
            set => SetProperty(ref _selectedFormat, value);
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
            set => SetProperty(ref _selectedEncoder, value);
        }

        public string Resolution
        {
            get => _resolution;
            set => SetProperty(ref _resolution, value);
        }

        public string Bitrate
        {
            get => _bitrate;
            set => SetProperty(ref _bitrate, value);
        }

        public string FrameRate
        {
            get => _frameRate;
            set => SetProperty(ref _frameRate, value);
        }

        public bool ShowAdvanced
        {
            get => _settingsService.ShowAdvancedFeatures;
            set
            {
                _settingsService.ShowAdvancedFeatures = value;
                OnPropertyChanged(nameof(ShowAdvanced));
            }
        }

        public string ColorSpace
        {
            get => _colorSpace;
            set => SetProperty(ref _colorSpace, value);
        }

        public string FFmpegCommand
        {
            get => _ffmpegCommand;
            set => SetProperty(ref _ffmpegCommand, value);
        }

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

        public ObservableCollection<string> FrameRates { get; } = new() { "25", "30", "60" };

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
                }
            }
            finally
            {
                IsLoadingEncoders = false;
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
