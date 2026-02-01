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
                    Version = "δ���",
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
                                break;
                            case "音频":
                                AudioCodecs.Add(codec);
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
    }
}
