using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using FFmpegStudio.Models;
using FFmpegStudio.Services;

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
            LoadFFmpegVersionAsync();
            LoadHardwareInfo();
            LoadCodecInfo();
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
                    Version = "Œ¥ºÏ≤‚",
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

        private void LoadCodecInfo()
        {
            var codecs = new[]
            {
                new CodecInfo { Name = "H.264", Description = "H.264 / AVC / MPEG-4 AVC", Category = " ”∆µ", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "H.265", Description = "H.265 / HEVC", Category = " ”∆µ", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "VP9", Description = "Google VP9", Category = " ”∆µ", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "AV1", Description = "AV1 Video Codec", Category = " ”∆µ", IsEncoder = false, IsDecoder = true },
                new CodecInfo { Name = "AAC", Description = "Advanced Audio Coding", Category = "“Ù∆µ", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "MP3", Description = "MP3 Audio", Category = "“Ù∆µ", IsEncoder = false, IsDecoder = true },
                new CodecInfo { Name = "OPUS", Description = "Opus Audio Codec", Category = "“Ù∆µ", IsEncoder = true, IsDecoder = true },
                new CodecInfo { Name = "FLAC", Description = "FLAC Audio Codec", Category = "“Ù∆µ", IsEncoder = true, IsDecoder = true },
            };

            foreach (var codec in codecs)
            {
                Codecs.Add(codec);
            }
        }
    }
}
