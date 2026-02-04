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

        public string FileNameTemplate
        {
            get => _fileNameTemplate;
            set => SetProperty(ref _fileNameTemplate, value);
        }

        public string SelectedFormat
        {
            get => _selectedFormat;
            set => SetProperty(ref _selectedFormat, value);
        }

        public double Quality
        {
            get => _quality;
            set => SetProperty(ref _quality, Math.Clamp(value, 0, 100));
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
