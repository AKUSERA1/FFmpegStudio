using System;
using System.Diagnostics;
using System.IO;
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
                    Version = "Î´¼ì²â",
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
                    Version = "Î´¼ì²â",
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
                versionInfo.Version = "ÒÑ°²×°";
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
    }
}
