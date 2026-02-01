using System.Collections.ObjectModel;
using FFmpegStudio.Models;

namespace FFmpegStudio.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private FFmpegVersionInfo _ffmpegVersion = new();
        private HardwareInfo _hardwareInfo = new();
        private bool _isLoading;

        public FFmpegVersionInfo FFmpegVersion
        {
            get => _ffmpegVersion;
            set => SetProperty(ref _ffmpegVersion, value);
        }

        public HardwareInfo HardwareInfo
        {
            get => _hardwareInfo;
            set => SetProperty(ref _hardwareInfo, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<CodecInfo> Codecs { get; } = new();

        public HomeViewModel()
        {
            LoadMockData();
        }

        private void LoadMockData()
        {
            // 模拟 FFmpeg 版本信息
            FFmpegVersion = new FFmpegVersionInfo
            {
                Version = "未检测",
                BuildDate = "-",
                IsInstalled = false
            };

            // 模拟硬件信息
            HardwareInfo = new HardwareInfo
            {
                CpuInfo = "Intel Core i7-10700K",
                GpuInfo = "NVIDIA GeForce RTX 3080",
                TotalMemory = 32 * 1024 * 1024 * 1024UL,
                AvailableMemory = 16 * 1024 * 1024 * 1024UL,
                CpuCoreCount = 8
            };

            // 模拟编解码器信息
            var codecs = new[]
            {
                new CodecInfo { Name = "H.264", Description = "H.264 / AVC / MPEG-4 AVC", Category = "视频", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "H.265", Description = "H.265 / HEVC", Category = "视频", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "VP9", Description = "Google VP9", Category = "视频", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "AV1", Description = "AV1 Video Codec", Category = "视频", IsEncoder = false, IsDecoder = true },
                new CodecInfo { Name = "AAC", Description = "Advanced Audio Coding", Category = "音频", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "MP3", Description = "MP3 Audio", Category = "音频", IsEncoder = false, IsDecoder = true },
                new CodecInfo { Name = "OPUS", Description = "Opus Audio Codec", Category = "音频", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "FLAC", Description = "FLAC Audio Codec", Category = "音频", IsEncoder = true, IsDecoder = true },
            };

            foreach (var codec in codecs)
            {
                Codecs.Add(codec);
            }
        }
    }
}
