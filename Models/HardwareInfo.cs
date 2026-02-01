using System;

namespace FFmpegStudio.Models
{
    public class HardwareInfo
    {
        public string? CpuInfo { get; set; }
        public string? GpuInfo { get; set; }
        public ulong TotalMemory { get; set; }
        public ulong AvailableMemory { get; set; }
        public int CpuCoreCount { get; set; }

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

    public class TranscodeTask
    {
        public string? Id { get; set; }
        public string? SourceFile { get; set; }
        public string? OutputFile { get; set; }
        public string? Status { get; set; }
        public int Progress { get; set; }
        public DateTime CreateTime { get; set; }
    }
}
