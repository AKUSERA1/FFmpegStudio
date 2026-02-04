using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using FFmpegStudio.Models;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace FFmpegStudio.ViewModels
{
    public class VideoExtractViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;
        private readonly Services.FFmpegService _ffmpegService;

        private string _videoFilePath = string.Empty;
        private string _outputPath = string.Empty;
        private string _fileNameTemplate = "frame_%04d.png";
        private string _selectedFormat = "PNG";
        private double _quality = 90;
        private VideoInfo? _videoInfo;
        private bool _isLoadingVideoInfo;
        private string _alphaChannel = "否";
        private string _exrPrecision = "单精度(float32)";
        private string _exrEncoding = "无压缩";

        public VideoExtractViewModel()
        {
            _settingsService = Services.SettingsService.Instance;
            _ffmpegService = Services.FFmpegService.Instance;
            BrowseCommand = new RelayCommand(async _ => await BrowseFileAsync());
        }

        public string VideoFilePath
        {
            get => _videoFilePath;
            set
            {
                if (SetProperty(ref _videoFilePath, value))
                {
                    _ = LoadVideoInfoAsync();
                }
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set => SetProperty(ref _outputPath, value);
        }

        private string GetFormatForTemplate(string template)
        {
            if (template.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return "PNG";
            else if (template.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                     template.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                return "JPG";
            else if (template.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                return "BMP";
            else if (template.EndsWith(".exr", StringComparison.OrdinalIgnoreCase))
                return "OpenEXR";
            else
                return "PNG"; // Default
        }

        private string GetTemplateForFormat(string format)
        {
            return format switch
            {
                "PNG" => "frame_%04d.png",
                "JPG" => "frame_%04d.jpg",
                "BMP" => "frame_%04d.bmp",
                "OpenEXR" => "frame_%04d.exr",
                _ => "frame_%04d.png" // Default
            };
        }

        public string FileNameTemplate
        {
            get => _fileNameTemplate;
            set
            {
                if (SetProperty(ref _fileNameTemplate, value))
                {
                    // Only update SelectedFormat if it doesn't already match the expected format
                    string expectedFormat = GetFormatForTemplate(value);
                    if (!string.Equals(SelectedFormat, expectedFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedFormat = expectedFormat; // Direct assignment to avoid triggering the setter
                        OnPropertyChanged(nameof(SelectedFormat)); // Manually notify the change
                    }
                }
            }
        }

        public string SelectedFormat
        {
            get => _selectedFormat;
            set
            {
                if (SetProperty(ref _selectedFormat, value))
                {
                    // Only update FileNameTemplate if it doesn't already match the expected template
                    string expectedTemplate = GetTemplateForFormat(value);
                    if (!string.Equals(FileNameTemplate, expectedTemplate, StringComparison.OrdinalIgnoreCase))
                    {
                        _fileNameTemplate = expectedTemplate; // Direct assignment to avoid triggering the setter
                        OnPropertyChanged(nameof(FileNameTemplate)); // Manually notify the change
                    }
                    
                    // Update control enable states when format changes
                    OnPropertyChanged(nameof(IsQualityEnabled));
                    OnPropertyChanged(nameof(IsAlphaChannelEnabled));
                    OnPropertyChanged(nameof(IsExrPrecisionEnabled));
                    OnPropertyChanged(nameof(IsExrEncodingEnabled));
                }
            }
        }

        public double Quality
        {
            get => _quality;
            set => SetProperty(ref _quality, Math.Clamp(value, 0, 100));
        }

        public string AlphaChannel
        {
            get => _alphaChannel;
            set => SetProperty(ref _alphaChannel, value);
        }

        public string ExrPrecision
        {
            get => _exrPrecision;
            set => SetProperty(ref _exrPrecision, value);
        }

        public string ExrEncoding
        {
            get => _exrEncoding;
            set => SetProperty(ref _exrEncoding, value);
        }

        public bool IsQualityEnabled => SelectedFormat == "JPG";

        public bool IsAlphaChannelEnabled => SelectedFormat == "PNG" || SelectedFormat == "OpenEXR";

        public bool IsExrPrecisionEnabled => SelectedFormat == "OpenEXR";

        public bool IsExrEncodingEnabled => SelectedFormat == "OpenEXR";

        public bool ShowAdvanced
        {
            get => _settingsService.ShowAdvancedFeatures;
            set
            {
                _settingsService.ShowAdvancedFeatures = value;
                OnPropertyChanged(nameof(ShowAdvanced));
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

        public bool HasVideoInfo => VideoInfo != null;

        public ObservableCollection<string> Formats { get; } = new() { "PNG", "JPG", "BMP", "OpenEXR" };

        public ObservableCollection<string> FileNameTemplates { get; } = new() { "frame_%04d.png", "frame_%04d.jpg", "frame_%04d.bmp","frame_%04d.exr" };

        public ObservableCollection<string> AlphaChannelOptions { get; } = new() { "是", "否" };

        public ObservableCollection<string> ExrPrecisionOptions { get; } = new() { "单精度(float32)", "半精度(float16)" };

        public ObservableCollection<string> ExrEncodingOptions { get; } = new() { "无压缩", "ZIP", "RLE", "PIZ(慢)", "PXR24(有损)" };

        public ICommand BrowseCommand { get; }

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
                VideoFilePath = file.Path;
            }
        }



        private async Task LoadVideoInfoAsync()
        {
            if (string.IsNullOrEmpty(VideoFilePath))
            {
                VideoInfo = null;
                return;
            }

            IsLoadingVideoInfo = true;
            try
            {
                VideoInfo = await _ffmpegService.GetVideoInfoAsync(VideoFilePath);
            }
            finally
            {
                IsLoadingVideoInfo = false;
            }
        }
    }
}
