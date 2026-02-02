using System;

namespace FFmpegStudio.Models
{
    public class VideoInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileSizeFormatted => FormatFileSize(FileSize);

        public string VideoCodec { get; set; } = string.Empty;
        public string AudioCodec { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string Bitrate { get; set; } = string.Empty;
        public string FrameRate { get; set; } = string.Empty;
        public string BitDepth { get; set; } = string.Empty;
        public string ColorSpace { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string DurationFormatted => Duration.ToString(@"hh\:mm\:ss");

        public bool HasVideo { get; set; }
        public bool HasAudio { get; set; }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024 * 1024):F2} TB";
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            if (bytes >= 1024L * 1024)
                return $"{bytes / (1024.0 * 1024):F2} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} B";
        }
    }
}
