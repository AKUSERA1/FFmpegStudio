using System.Collections.ObjectModel;
using FFmpegStudio.Models;

namespace FFmpegStudio.ViewModels
{
    public class FrameSequenceSynthViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;

        private string _frameSequencePath = string.Empty;
        private string _selectedFormat = "MP4";
        private string _frameRate = "30";
        private string _colorSpace = "BT.709";
        private int _selectedFrameIndex;

        public FrameSequenceSynthViewModel()
        {
            _settingsService = Services.SettingsService.Instance;
        }

        public string FrameSequencePath
        {
            get => _frameSequencePath;
            set => SetProperty(ref _frameSequencePath, value);
        }

        public string SelectedFormat
        {
            get => _selectedFormat;
            set => SetProperty(ref _selectedFormat, value);
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

        public int SelectedFrameIndex
        {
            get => _selectedFrameIndex;
            set => SetProperty(ref _selectedFrameIndex, value);
        }

        public ObservableCollection<string> Formats { get; } = new() { "MP4", "MKV", "AVI", "MOV", "WebM" };

        public ObservableCollection<string> FrameRates { get; } = new() { "24", "25", "30", "60" };

        public ObservableCollection<string> ColorSpaces { get; } = new() { "BT.709", "BT.601", "BT.2020" };

        public ObservableCollection<string> FrameSequences { get; } = new();
    }
}
