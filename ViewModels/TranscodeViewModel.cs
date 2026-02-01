using System.Collections.ObjectModel;
using FFmpegStudio.Models;

namespace FFmpegStudio.ViewModels
{
    public class TranscodeViewModel : ViewModelBase
    {
        private string _sourceFilePath = string.Empty;
        private string _selectedFormat = "MP4";
        private string _selectedEncoder = "H.264";
        private string _resolution = "1920x1080";
        private string _bitrate = "5000k";
        private string _frameRate = "30";
        private bool _showAdvanced;
        private string _colorSpace = "BT.709";

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set => SetProperty(ref _sourceFilePath, value);
        }

        public string SelectedFormat
        {
            get => _selectedFormat;
            set => SetProperty(ref _selectedFormat, value);
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
            get => _showAdvanced;
            set => SetProperty(ref _showAdvanced, value);
        }

        public string ColorSpace
        {
            get => _colorSpace;
            set => SetProperty(ref _colorSpace, value);
        }

        public ObservableCollection<string> Formats { get; } = new() { "MP4", "MKV", "AVI", "MOV", "FLV", "WebM" };

        public ObservableCollection<string> Encoders { get; } = new() { "H.264", "H.265", "VP9", "AV1" };

        public ObservableCollection<string> Resolutions { get; } = new() { "1920x1080", "1280x720", "854x480", "640x360" };

        public ObservableCollection<string> FrameRates { get; } = new() { "24", "25", "30", "60" };

        public ObservableCollection<string> ColorSpaces { get; } = new() { "BT.709", "BT.601", "BT.2020" };

        public ObservableCollection<TranscodeTask> Tasks { get; } = new();

        public TranscodeViewModel()
        {
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
                CreateTime = System.DateTime.Now.AddHours(-2)
            });

            Tasks.Add(new TranscodeTask
            {
                Id = "2",
                SourceFile = "video2.avi",
                OutputFile = "video2_transcoded.mp4",
                Status = "处理中",
                Progress = 45,
                CreateTime = System.DateTime.Now.AddMinutes(-30)
            });
        }
    }
}
