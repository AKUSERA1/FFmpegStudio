using System.Collections.ObjectModel;
using FFmpegStudio.Models;

namespace FFmpegStudio.ViewModels
{
    public class FrameSequenceSynthViewModel : ViewModelBase
    {
        private string _frameSequencePath = string.Empty;
        private string _selectedFormat = "MP4";
        private string _frameRate = "30";
        private bool _showAdvanced;
        private string _colorSpace = "BT.709";
        private int _selectedFrameIndex;

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
            get => _showAdvanced;
            set => SetProperty(ref _showAdvanced, value);
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
