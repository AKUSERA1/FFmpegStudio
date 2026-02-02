using System;
using System.Collections.Generic;
using System.Linq;

namespace FFmpegStudio.Models
{
    public class HardwareInfo
    {
        public string? CpuInfo { get; set; }
        public List<string> GpuInfoList { get; set; } = new();
        public ulong TotalMemory { get; set; }
        public ulong AvailableMemory { get; set; }
        public int CpuCoreCount { get; set; }
        
        public string CpuCoreCountDisplay => $"CPU内核数：{CpuCoreCount}";

        public string CpuInfoDisplay => string.IsNullOrEmpty(CpuInfo) ? "δ֪" : CpuInfo;
        public string GpuInfoDisplay => GpuInfoList.Count > 0 ? string.Join("\n", GpuInfoList.Select((gpu, i) => $"GPU{i}: {gpu}")) : "δ֪";
        public string MemoryDisplay => $"{AvailableMemory / (1024 * 1024 * 1024)} GB / {TotalMemory / (1024 * 1024 * 1024)} GB";
    }

    public class CodecInfo
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsEncoder { get; set; }
        public bool IsDecoder { get; set; }
        public string? Category { get; set; }
    }

    public class FFmpegVersionInfo
    {
        public string? Version { get; set; }
        public string? BuildDate { get; set; }
        public bool IsInstalled { get; set; }
    }

}
