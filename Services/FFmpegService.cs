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
    }
}
