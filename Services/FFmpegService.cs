using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FFmpegStudio.Models;

namespace FFmpegStudio.Services
{
    public class FFmpegService
    {
        private static FFmpegService? _instance;
        public static FFmpegService Instance => _instance ??= new FFmpegService();

        private readonly SettingsService _settingsService;

        private FFmpegService()
        {
            _settingsService = SettingsService.Instance;
        }

        public async Task<VideoInfo?> GetVideoInfoAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            var ffmpegPath = _settingsService.FFmpegPath;
            var executablePath = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;

            try
            {
                var output = await ExecuteFFmpegAsync(executablePath, $"-i \"{filePath}\"");
                return ParseVideoInfo(output, filePath);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private VideoInfo? ParseVideoInfo(string output, string filePath)
        {
            if (string.IsNullOrEmpty(output))
                return null;

            var videoInfo = new VideoInfo
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileSize = new FileInfo(filePath).Length
            };

            // Parse video stream
            var videoMatch = Regex.Match(output, @"Stream #\d+:\d+(?:\[\w+\])?(?:\(\w+\))?: Video: ([^,]+)");
            if (videoMatch.Success)
            {
                videoInfo.HasVideo = true;
                videoInfo.VideoCodec = videoMatch.Groups[1].Value.Trim();
            }

            // Parse audio stream
            var audioMatch = Regex.Match(output, @"Stream #\d+:\d+(?:\[\w+\])?(?:\(\w+\))?: Audio: ([^,]+)");
            if (audioMatch.Success)
            {
                videoInfo.HasAudio = true;
                videoInfo.AudioCodec = audioMatch.Groups[1].Value.Trim();
            }

            // Parse resolution
            var resolutionMatch = Regex.Match(output, @"(\d{2,5})x(\d{2,5})");
            if (resolutionMatch.Success)
            {
                videoInfo.Resolution = $"{resolutionMatch.Groups[1].Value}x{resolutionMatch.Groups[2].Value}";
            }

            // Parse bitrate
            var bitrateMatch = Regex.Match(output, @"bitrate: (\d+) kb/s");
            if (bitrateMatch.Success)
            {
                videoInfo.Bitrate = $"{bitrateMatch.Groups[1].Value} kbps";
            }

            // Parse frame rate
            var fpsMatch = Regex.Match(output, @"(\d+(?:\.\d+)?) fps");
            if (fpsMatch.Success)
            {
                videoInfo.FrameRate = fpsMatch.Groups[1].Value;
            }

            // Parse duration
            var durationMatch = Regex.Match(output, @"Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})");
            if (durationMatch.Success)
            {
                var hours = int.Parse(durationMatch.Groups[1].Value);
                var minutes = int.Parse(durationMatch.Groups[2].Value);
                var seconds = double.Parse(durationMatch.Groups[3].Value);
                videoInfo.Duration = new TimeSpan(hours, minutes, 0).Add(TimeSpan.FromSeconds(seconds));
            }

            // Parse bit depth (yuv420p, yuv420p10le, etc.)
            var bitDepthMatch = Regex.Match(output, @"yuv\w*(\d+)p");
            if (bitDepthMatch.Success)
            {
                var bits = bitDepthMatch.Groups[1].Value;
                videoInfo.BitDepth = bits == "10" ? "10-bit" : bits == "12" ? "12-bit" : "8-bit";
            }

            // Parse color space (bt709, bt601, bt2020)
            var colorSpaceMatch = Regex.Match(output, @"(bt709|bt601|bt2020)", RegexOptions.IgnoreCase);
            if (colorSpaceMatch.Success)
            {
                videoInfo.ColorSpace = colorSpaceMatch.Groups[1].Value.ToUpperInvariant() switch
                {
                    "BT709" => "BT.709",
                    "BT601" => "BT.601",
                    "BT2020" => "BT.2020",
                    _ => colorSpaceMatch.Groups[1].Value.ToUpperInvariant()
                };
            }

            return videoInfo;
        }

        public async Task<FFmpegVersionInfo> GetFFmpegVersionAsync()
        {
            var ffmpegPath = _settingsService.FFmpegPath;
            var executablePath = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;

            try
            {
                var output = await ExecuteFFmpegAsync(executablePath, "-version");
                return ParseFFmpegVersion(output);
            }
            catch (Exception ex)
            {
                // FFmpeg not found or command failed
                return new FFmpegVersionInfo
                {
                    Version = "未知",
                    BuildDate = "-",
                    IsInstalled = false
                };
            }
        }

        public bool ValidateFFmpegPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!File.Exists(path))
                return false;

            try
            {
                var output = ExecuteFFmpegSync(path, "-version");
                return !string.IsNullOrEmpty(output) && output.Contains("ffmpeg");
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> ExecuteFFmpegAsync(string ffmpegPath, string arguments)
        {
            return await Task.Run(() => ExecuteFFmpegSync(ffmpegPath, arguments));
        }

        private string ExecuteFFmpegSync(string ffmpegPath, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process == null)
                    throw new InvalidOperationException("Failed to start FFmpeg process");

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000); // 5 second timeout

                return output + error;
            }
        }

        private FFmpegVersionInfo ParseFFmpegVersion(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return new FFmpegVersionInfo
                {
                    Version = "未检测",
                    BuildDate = "-",
                    IsInstalled = false
                };
            }

            var versionInfo = new FFmpegVersionInfo { IsInstalled = true };

            // Extract version number (e.g., "ffmpeg version 5.1.2 Copyright")
            var versionMatch = Regex.Match(output, @"ffmpeg version ([\d.]+)");
            if (versionMatch.Success)
            {
                versionInfo.Version = versionMatch.Groups[1].Value;
            }
            else
            {
                versionInfo.Version = "已安装";
            }

            // Extract build date (e.g., "built with gcc 10.2.0 (RevisionVersion 10.2.0-1)")
            var dateMatch = Regex.Match(output, @"built on (\d{4}-\d{2}-\d{2})");
            if (dateMatch.Success)
            {
                versionInfo.BuildDate = dateMatch.Groups[1].Value;
            }
            else
            {
                versionInfo.BuildDate = "-";
            }

            return versionInfo;
        }

        public async Task<List<CodecInfo>> GetCodecsAsync()
        {
            var ffmpegPath = _settingsService.FFmpegPath;
            var executablePath = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;

            try
            {
                var output = await ExecuteFFmpegAsync(executablePath, "-codecs");
                return ParseCodecs(output);
            }
            catch (Exception ex)
            {
                return new List<CodecInfo>();
            }
        }

        private List<CodecInfo> ParseCodecs(string output)
        {
            var codecs = new List<CodecInfo>();

            if (string.IsNullOrEmpty(output))
                return codecs;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // FFmpeg -codecs output format:
                //  D..... = Decoding supported
                //  .E.... = Encoding supported
                //  ..V... = Video codec
                //  ..A... = Audio codec
                //  ..S... = Subtitle codec
                //  ...I.. = Intra frame-only codec
                //  ....L. = Lossy compression
                //  .....S = Lossless compression
                //  codec_name    description

                var match = Regex.Match(line, @"^\s*([D\.])([E\.])([VAS\.])([I\.])([L\.])([S\.])\s+(\S+)\s+(.+)$");
                if (match.Success)
                {
                    var flags = match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value +
                               match.Groups[4].Value + match.Groups[5].Value + match.Groups[6].Value;
                    var name = match.Groups[7].Value.Trim();
                    var description = match.Groups[8].Value.Trim();

                    var codecType = match.Groups[3].Value;
                    string category;
                    switch (codecType)
                    {
                        case "V":
                            category = "视频";
                            break;
                        case "A":
                            category = "音频";
                            break;
                        case "S":
                            category = "字幕";
                            break;
                        default:
                            category = "其他";
                            break;
                    }

                    var isDecoder = match.Groups[1].Value == "D";
                    var isEncoder = match.Groups[2].Value == "E";

                    codecs.Add(new CodecInfo
                    {
                        Name = name,
                        Description = description,
                        Category = category,
                        IsDecoder = isDecoder,
                        IsEncoder = isEncoder
                    });
                }
            }

            return codecs.OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();
        }

        public async Task<List<string>> GetEncodersByCodecCategoryAsync(string codecCategory)
        {
            var ffmpegPath = _settingsService.FFmpegPath;
            var executablePath = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;

            try
            {
                var output = await ExecuteFFmpegAsync(executablePath, "-encoders");
                return ParseEncodersByCategory(output, codecCategory);
            }
            catch (Exception ex)
            {
                return GetDefaultEncoders(codecCategory);
            }
        }

        private List<string> ParseEncodersByCategory(string output, string codecCategory)
        {
            var encoders = new List<string>();

            if (string.IsNullOrEmpty(output))
                return GetDefaultEncoders(codecCategory);

            // 根据编码类别确定要查找的关键字
            var keywords = GetCategoryKeywords(codecCategory);

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // FFmpeg -encoders output format:
                //  V..... = Video codec
                //  A..... = Audio codec
                //  S..... = Subtitle codec
                //  .F.... = Frame-level multithreading
                //  ..S... = Slice-level multithreading
                //  ...X.. = Codec is experimental
                //  ....B. = Supports draw_horiz_band
                //  .....D = Supports direct rendering method 1
                //  encoder_name    description

                var match = Regex.Match(line, @"^\s*[VAS][F\.][S\.][X\.][B\.][D\.]\s+(\S+)\s+(.+)$");
                if (match.Success)
                {
                    var encoderName = match.Groups[1].Value.Trim();
                    var description = match.Groups[2].Value.Trim().ToLowerInvariant();

                    // 检查是否匹配当前类别
                    foreach (var keyword in keywords)
                    {
                        if (description.Contains(keyword.ToLowerInvariant()) ||
                            encoderName.Contains(keyword.ToLowerInvariant()))
                        {
                            encoders.Add(encoderName);
                            break;
                        }
                    }
                }
            }

            // 如果没有找到匹配的编码器，返回默认值
            if (encoders.Count == 0)
            {
                return GetDefaultEncoders(codecCategory);
            }

            return encoders.Distinct().ToList();
        }

        private List<string> GetCategoryKeywords(string codecCategory)
        {
            return codecCategory switch
            {
                "H.264" => new List<string> { "h264", "libx264", "h.264", "avc" },
                "HEVC" => new List<string> { "hevc", "h265", "h.265", "libx265", "x265" },
                "AV1" => new List<string> { "av1", "libaom", "svtav1", "rav1e" },
                "MPEG4" => new List<string> { "mpeg4", "mpeg-4", "mp4", "libxvid" },
                "QuickTime" => new List<string> { "qtrle", "qdrw", "rpza", "svq1", "svq3", "cinepak", "quicktime" },
                "VP9" => new List<string> { "vp9", "libvpx-vp9" },
                _ => new List<string> { codecCategory.ToLowerInvariant() }
            };
        }

        private List<string> GetDefaultEncoders(string codecCategory)
        {
            return codecCategory switch
            {
                "H.264" => new List<string> { "libx264", "h264_nvenc", "h264_amf", "h264_qsv" },
                "HEVC" => new List<string> { "libx265", "hevc_nvenc", "hevc_amf", "hevc_qsv" },
                "AV1" => new List<string> { "libaom-av1", "libsvtav1", "av1_nvenc" },
                "MPEG4" => new List<string> { "libxvid", "mpeg4" },
                "QuickTime" => new List<string> { "qtrle", "cinepak" },
                "VP9" => new List<string> { "libvpx-vp9" },
                _ => new List<string> { codecCategory }
            };
        }
    }
}
