using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using FFmpegStudio.Models;
using FFmpegStudio.Services;

namespace FFmpegStudio.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private FFmpegVersionInfo _ffmpegVersion = new();
        private HardwareInfo _hardwareInfo = new();
        private bool _isLoading;
        private bool _isCodecsLoading;
        private string _codecsErrorMessage = string.Empty;

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

        public bool IsCodecsLoading
        {
            get => _isCodecsLoading;
            set => SetProperty(ref _isCodecsLoading, value);
        }

        public string CodecsErrorMessage
        {
            get => _codecsErrorMessage;
            set => SetProperty(ref _codecsErrorMessage, value);
        }

        public ObservableCollection<CodecInfo> VideoCodecs { get; } = new();
        public ObservableCollection<CodecInfo> AudioCodecs { get; } = new();
        public ObservableCollection<CodecInfo> SubtitleCodecs { get; } = new();
        public ObservableCollection<CodecInfo> OtherCodecs { get; } = new();

        // Video codec categories
        public ObservableCollection<CodecInfo> H264VideoCodecs { get; } = new();
        public ObservableCollection<CodecInfo> HevcVideoCodecs { get; } = new();
        public ObservableCollection<CodecInfo> Av1VideoCodecs { get; } = new();
        public ObservableCollection<CodecInfo> Mpeg4VideoCodecs { get; } = new();
        public ObservableCollection<CodecInfo> QuickTimeVideoCodecs { get; } = new();
        public ObservableCollection<CodecInfo> Vp9VideoCodecs { get; } = new();
        public ObservableCollection<CodecInfo> OtherVideoCodecs { get; } = new();

        // Audio codec categories
        public ObservableCollection<CodecInfo> Mp3AudioCodecs { get; } = new();
        public ObservableCollection<CodecInfo> Mp2AudioCodecs { get; } = new();
        public ObservableCollection<CodecInfo> AacAudioCodecs { get; } = new();
        public ObservableCollection<CodecInfo> Ac3AudioCodecs { get; } = new();
        public ObservableCollection<CodecInfo> FlacAudioCodecs { get; } = new();
        public ObservableCollection<CodecInfo> PcmAudioCodecs { get; } = new();
        public ObservableCollection<CodecInfo> OtherAudioCodecs { get; } = new();

        public HomeViewModel()
        {
            LoadFFmpegVersionAsync();
            LoadHardwareInfo();
            LoadCodecInfoAsync();
        }

        private async void LoadFFmpegVersionAsync()
        {
            try
            {
                var ffmpegService = FFmpegService.Instance;
                var versionInfo = await ffmpegService.GetFFmpegVersionAsync();
                FFmpegVersion = versionInfo;
            }
            catch
            {
                FFmpegVersion = new FFmpegVersionInfo
                {
                    Version = "未安装",
                    BuildDate = "-",
                    IsInstalled = false
                };
            }
        }

        private void LoadHardwareInfo()
        {
            var hardwareInfo = new HardwareInfo();

            // Load CPU information
            try
            {
                var processorCount = Environment.ProcessorCount;
                var cpuInfo = GetProcessorName();
                hardwareInfo.CpuInfo = cpuInfo;
                hardwareInfo.CpuCoreCount = processorCount;
            }
            catch
            {
                // CPU information failed to load, leave as null for "Unknown" display
            }

            // Load GPU information
            try
            {
                var gpuInfoList = GetGpuInfo();
                hardwareInfo.GpuInfoList = gpuInfoList;
            }
            catch
            {
                // GPU information failed to load, leave as empty for "Unknown" display
            }

            // Load memory information
            try
            {
                var memInfo = GetMemoryInfo();
                hardwareInfo.TotalMemory = memInfo.total;
                hardwareInfo.AvailableMemory = memInfo.available;
            }
            catch
            {
                // Memory information failed to load
            }

            HardwareInfo = hardwareInfo;
        }

        private string GetProcessorName()
        {
            try
            {
                // Try reading from registry
                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key != null)
                {
                    return key.GetValue("ProcessorNameString")?.ToString();
                }
            }
            catch { }

            return null;
        }

        private List<string> GetGpuInfo()
        {
            var gpuList = new List<string>();

            try
            {
                // Try to get from registry
                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\VIDEO");
                if (key != null)
                {
                    var subKeyNames = key.GetSubKeyNames();
                    foreach (var subKeyName in subKeyNames.Take(4)) // Limit to first 4 GPUs
                    {
                        var subKey = key.OpenSubKey(subKeyName);
                        var description = subKey?.GetValue("Description")?.ToString();
                        if (!string.IsNullOrEmpty(description))
                        {
                            gpuList.Add(description);
                        }
                    }
                }
            }
            catch { }

            // Fallback: try WMI through PowerShell
            if (gpuList.Count == 0)
            {
                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Get-WmiObject Win32_VideoController | Select-Object -ExpandProperty Name\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });

                    if (process != null)
                    {
                        using (var reader = process.StandardOutput)
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    gpuList.Add(line.Trim());
                                }
                            }
                        }
                        process.WaitForExit();
                    }
                }
                catch { }
            }

            return gpuList;
        }

        private (ulong total, ulong available) GetMemoryInfo()
        {
            ulong totalSystemMemory = 0;
            ulong availableMemory = 0;

            try
            {
                // Get available memory using PowerShell
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"[Math]::Round((Get-WmiObject Win32_OperatingSystem).FreePhysicalMemory * 1024)\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    using (var reader = process.StandardOutput)
                    {
                        var output = reader.ReadToEnd().Trim();
                        if (ulong.TryParse(output, out var available))
                        {
                            availableMemory = available;
                        }
                    }
                    process.WaitForExit();
                }
            }
            catch { }

            try
            {
                // Get total memory using PowerShell
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"[Math]::Round((Get-WmiObject Win32_OperatingSystem).TotalVisibleMemorySize * 1024)\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    using (var reader = process.StandardOutput)
                    {
                        var output = reader.ReadToEnd().Trim();
                        if (ulong.TryParse(output, out var total))
                        {
                            totalSystemMemory = total;
                        }
                    }
                    process.WaitForExit();
                }
            }
            catch { }

            // Fallback: use default if unable to detect
            if (totalSystemMemory == 0)
            {
                totalSystemMemory = 8UL * 1024 * 1024 * 1024; // 8GB default
            }

            return (totalSystemMemory, availableMemory);
        }

        private async void LoadCodecInfoAsync()
        {
            IsCodecsLoading = true;
            CodecsErrorMessage = string.Empty;

            try
            {
                var ffmpegService = FFmpegService.Instance;
                var codecs = await ffmpegService.GetCodecsAsync();

                VideoCodecs.Clear();
                AudioCodecs.Clear();
                SubtitleCodecs.Clear();
                OtherCodecs.Clear();

                // Clear categorized collections
                H264VideoCodecs.Clear();
                HevcVideoCodecs.Clear();
                Av1VideoCodecs.Clear();
                Mpeg4VideoCodecs.Clear();
                QuickTimeVideoCodecs.Clear();
                Vp9VideoCodecs.Clear();
                OtherVideoCodecs.Clear();

                Mp3AudioCodecs.Clear();
                Mp2AudioCodecs.Clear();
                AacAudioCodecs.Clear();
                Ac3AudioCodecs.Clear();
                FlacAudioCodecs.Clear();
                PcmAudioCodecs.Clear();
                OtherAudioCodecs.Clear();

                if (codecs.Count == 0)
                {
                    CodecsErrorMessage = "未能获取编解码器信息，请检查 FFmpeg 是否正确安装。";
                }
                else
                {
                    foreach (var codec in codecs)
                    {
                        switch (codec.Category)
                        {
                            case "视频":
                                VideoCodecs.Add(codec);
                                CategorizeVideoCodec(codec);
                                break;
                            case "音频":
                                AudioCodecs.Add(codec);
                                CategorizeAudioCodec(codec);
                                break;
                            case "字幕":
                                SubtitleCodecs.Add(codec);
                                break;
                            default:
                                OtherCodecs.Add(codec);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CodecsErrorMessage = $"加载编解码器信息时出错: {ex.Message}";
            }
            finally
            {
                IsCodecsLoading = false;
            }
        }

        private void CategorizeVideoCodec(CodecInfo codec)
        {
            var name = codec.Name?.ToLower() ?? string.Empty;

            if (name.Contains("h264") || name.Contains("h.264") || name == "libx264")
            {
                H264VideoCodecs.Add(codec);
            }
            else if (name.Contains("hevc") || name.Contains("h265") || name.Contains("h.265") || name == "libx265")
            {
                HevcVideoCodecs.Add(codec);
            }
            else if (name.Contains("av1"))
            {
                Av1VideoCodecs.Add(codec);
            }
            else if (name.Contains("mpeg4"))
            {
                Mpeg4VideoCodecs.Add(codec);
            }
            else if (name.Contains("qtrle") || name.Contains("prores") || name.Contains("dnxhd"))
            {
                QuickTimeVideoCodecs.Add(codec);
            }
            else if (name.Contains("vp9") || name == "libvpx-vp9")
            {
                Vp9VideoCodecs.Add(codec);
            }
            else
            {
                OtherVideoCodecs.Add(codec);
            }
        }

        private void CategorizeAudioCodec(CodecInfo codec)
        {
            var name = codec.Name?.ToLower() ?? string.Empty;

            if (name.Contains("mp3") || name == "libmp3lame")
            {
                Mp3AudioCodecs.Add(codec);
            }
            else if (name.Contains("mp2"))
            {
                Mp2AudioCodecs.Add(codec);
            }
            else if (name.Contains("aac") || name == "aac")
            {
                AacAudioCodecs.Add(codec);
            }
            else if (name.Contains("ac3"))
            {
                Ac3AudioCodecs.Add(codec);
            }
            else if (name.Contains("flac"))
            {
                FlacAudioCodecs.Add(codec);
            }
            else if (name.Contains("pcm"))
            {
                PcmAudioCodecs.Add(codec);
            }
            else
            {
                OtherAudioCodecs.Add(codec);
            }
        }
    }
}
