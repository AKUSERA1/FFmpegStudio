using System.Collections.ObjectModel;

namespace FFmpegStudio.ViewModels
{
    public class VideoExtractViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;

        private string _videoFilePath = string.Empty;
        private string _outputPath = string.Empty;
        private string _fileNameTemplate = "frame_%06d.png";
        private string _selectedFormat = "PNG";
        private string _frameInterval = "1";

        public VideoExtractViewModel()
        {
            _settingsService = Services.SettingsService.Instance;
        }

        public string VideoFilePath
        {
            get => _videoFilePath;
            set => SetProperty(ref _videoFilePath, value);
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

        public bool ShowAdvanced
        {
            get => _settingsService.ShowAdvancedFeatures;
            set
            {
                _settingsService.ShowAdvancedFeatures = value;
                OnPropertyChanged(nameof(ShowAdvanced));
            }
        }

        public string FrameInterval
        {
            get => _frameInterval;
            set => SetProperty(ref _frameInterval, value);
        }

        public ObservableCollection<string> Formats { get; } = new() { "PNG", "JPG", "BMP", "TIFF" };

        public ObservableCollection<string> FileNameTemplates { get; } = new() { "frame_%06d.png", "image_%05d.jpg", "frame_%04d.bmp" };
    }
}
