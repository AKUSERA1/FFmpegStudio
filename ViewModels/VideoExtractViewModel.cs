using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FFmpegStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace FFmpegStudio.ViewModels
{
    public class VideoExtractViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;
        private readonly Services.FFmpegService _ffmpegService;

        private string _videoFilePath = string.Empty;
        private string _outputPath = string.Empty;
        private string _fileNameTemplate = "frame_%04d.png";
        private string _selectedFormat = "PNG";
        private double _quality = 90;
        private VideoInfo? _videoInfo;
        private bool _isLoadingVideoInfo;
        private string _alphaChannel = "否";
        private string _exrPrecision = "单精度(float32)";
        private string _exrEncoding = "无压缩";
        private string _ffmpegCommand = string.Empty;
        private TranscodeTask? _currentTask;
        private System.Diagnostics.Process? _ffmpegProcess;
        private CancellationTokenSource? _ffmpegCts;

        public VideoExtractViewModel()
        {
            _settingsService = Services.SettingsService.Instance;
            _ffmpegService = Services.FFmpegService.Instance;
            BrowseCommand = new RelayCommand(async _ => await BrowseFileAsync());
            BrowseOutputCommand = new RelayCommand(async _ => await BrowseOutputAsync());
            StartExtractCommand = new RelayCommand(async _ => await StartExtractAsync());
            CancelExtractCommand = new RelayCommand(_ => CancelExtract());
        }

        public string VideoFilePath
        {
            get => _videoFilePath;
            set
            {
                if (SetProperty(ref _videoFilePath, value))
                {
                    _ = LoadVideoInfoAsync();
                }
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set => SetProperty(ref _outputPath, value);
        }

        private string GetFormatForTemplate(string template)
        {
            if (template.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return "PNG";
            else if (template.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                     template.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                return "JPG";
            else if (template.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                return "BMP";
            else if (template.EndsWith(".exr", StringComparison.OrdinalIgnoreCase))
                return "OpenEXR";
            else
                return "PNG"; // Default
        }

        private string GetTemplateForFormat(string format)
        {
            return format switch
            {
                "PNG" => "frame_%04d.png",
                "JPG" => "frame_%04d.jpg",
                "BMP" => "frame_%04d.bmp",
                "OpenEXR" => "frame_%04d.exr",
                _ => "frame_%04d.png" // Default
            };
        }

        public string FileNameTemplate
        {
            get => _fileNameTemplate;
            set
            {
                if (SetProperty(ref _fileNameTemplate, value))
                {
                    // Only update SelectedFormat if it doesn't already match the expected format
                    string expectedFormat = GetFormatForTemplate(value);
                    if (!string.Equals(SelectedFormat, expectedFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedFormat = expectedFormat; // Direct assignment to avoid triggering the setter
                        OnPropertyChanged(nameof(SelectedFormat)); // Manually notify the change
                    }
                    ConstructFFmpegCommand();
                }
            }
        }

        public string SelectedFormat
        {
            get => _selectedFormat;
            set
            {
                if (SetProperty(ref _selectedFormat, value))
                {
                    // Only update FileNameTemplate if it doesn't already match the expected template
                    string expectedTemplate = GetTemplateForFormat(value);
                    if (!string.Equals(FileNameTemplate, expectedTemplate, StringComparison.OrdinalIgnoreCase))
                    {
                        _fileNameTemplate = expectedTemplate; // Direct assignment to avoid triggering the setter
                        OnPropertyChanged(nameof(FileNameTemplate)); // Manually notify the change
                    }
                    
                    // Update control enable states when format changes
                    OnPropertyChanged(nameof(IsQualityEnabled));
                    OnPropertyChanged(nameof(IsAlphaChannelEnabled));
                    OnPropertyChanged(nameof(IsExrPrecisionEnabled));
                    OnPropertyChanged(nameof(IsExrEncodingEnabled));
                    ConstructFFmpegCommand();
                }
            }
        }

        public double Quality
        {
            get => _quality;
            set
            {
                if (SetProperty(ref _quality, Math.Clamp(value, 0, 100)))
                {
                    ConstructFFmpegCommand();
                }
            }
        }

        public string AlphaChannel
        {
            get => _alphaChannel;
            set
            {
                if (SetProperty(ref _alphaChannel, value))
                {
                    ConstructFFmpegCommand();
                }
            }
        }

        public string ExrPrecision
        {
            get => _exrPrecision;
            set
            {
                if (SetProperty(ref _exrPrecision, value))
                {
                    ConstructFFmpegCommand();
                }
            }
        }

        public string ExrEncoding
        {
            get => _exrEncoding;
            set
            {
                if (SetProperty(ref _exrEncoding, value))
                {
                    ConstructFFmpegCommand();
                }
            }
        }

        public bool IsQualityEnabled => SelectedFormat == "JPG";

        public bool IsAlphaChannelEnabled => SelectedFormat == "PNG" || SelectedFormat == "OpenEXR";

        public bool IsExrPrecisionEnabled => SelectedFormat == "OpenEXR";

        public bool IsExrEncodingEnabled => SelectedFormat == "OpenEXR";

        public bool ShowAdvanced
        {
            get => _settingsService.ShowAdvancedFeatures;
            set
            {
                _settingsService.ShowAdvancedFeatures = value;
                OnPropertyChanged(nameof(ShowAdvanced));
            }
        }

        public VideoInfo? VideoInfo
        {
            get => _videoInfo;
            set
            {
                if (SetProperty(ref _videoInfo, value))
                {
                    OnPropertyChanged(nameof(HasVideoInfo));
                }
            }
        }

        public bool IsLoadingVideoInfo
        {
            get => _isLoadingVideoInfo;
            set => SetProperty(ref _isLoadingVideoInfo, value);
        }

        public bool HasVideoInfo => VideoInfo != null;

        public ObservableCollection<string> Formats { get; } = new() { "PNG", "JPG", "BMP", "OpenEXR" };

        public ObservableCollection<string> FileNameTemplates { get; } = new() { "frame_%04d.png", "frame_%04d.jpg", "frame_%04d.bmp","frame_%04d.exr" };

        public ObservableCollection<string> AlphaChannelOptions { get; } = new() { "是", "否" };

        public ObservableCollection<string> ExrPrecisionOptions { get; } = new() { "单精度(float32)", "半精度(float16)" };

        public ObservableCollection<string> ExrEncodingOptions { get; } = new() { "无压缩", "ZIP", "RLE", "PIZ(慢)", "PXR24(有损)" };

        public ObservableCollection<TranscodeTask> Tasks { get; } = new();

        public string FFmpegCommand
        {
            get => _ffmpegCommand;
            set => SetProperty(ref _ffmpegCommand, value);
        }

        public ICommand BrowseCommand { get; }
        public ICommand BrowseOutputCommand { get; }
        public ICommand StartExtractCommand { get; }
        public ICommand CancelExtractCommand { get; }

        private async Task BrowseFileAsync()
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mkv");
            picker.FileTypeFilter.Add(".avi");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".flv");
            picker.FileTypeFilter.Add(".webm");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".m4v");
            picker.FileTypeFilter.Add(".mpeg");
            picker.FileTypeFilter.Add(".mpg");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                VideoFilePath = file.Path;
            }
        }

        private async Task BrowseOutputAsync()
        {
            var picker = new FolderPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                OutputPath = folder.Path;
                ConstructFFmpegCommand();
            }
        }

        private async Task LoadVideoInfoAsync()
        {
            if (string.IsNullOrEmpty(VideoFilePath))
            {
                VideoInfo = null;
                return;
            }

            IsLoadingVideoInfo = true;
            try
            {
                VideoInfo = await _ffmpegService.GetVideoInfoAsync(VideoFilePath);
                ConstructFFmpegCommand();
            }
            finally
            {
                IsLoadingVideoInfo = false;
            }
        }

        private void ConstructFFmpegCommand()
        {
            if (string.IsNullOrEmpty(VideoFilePath) || VideoInfo == null)
            {
                FFmpegCommand = string.Empty;
                return;
            }

            try
            {
                var outputPath = GetOutputPath();
                var commandParts = new List<string>();

                // 基础命令部分
                commandParts.AddRange(new[] { "ffmpeg", "-i", $"\"{VideoFilePath}\"" });

                // 根据格式添加参数
                switch (SelectedFormat)
                {
                    case "PNG":
                        commandParts.AddRange(new[] { "-c:v", "png" });
                        if (AlphaChannel == "是")
                        {
                            commandParts.AddRange(new[] { "-pix_fmt", "rgba" });
                        }
                        break;
                    case "JPG":
                        commandParts.AddRange(new[] { "-c:v", "mjpeg" });
                        commandParts.AddRange(new[] { "-q:v", Quality.ToString() });
                        break;
                    case "BMP":
                        commandParts.AddRange(new[] { "-c:v", "bmp" });
                        break;
                    case "OpenEXR":
                        commandParts.AddRange(new[] { "-c:v", "exr" });
                        
                        // 根据精度和Alpha通道设置正确的像素格式
                        if (ExrPrecision == "半精度(float16)")
                        {
                            if (AlphaChannel == "是")
                            {
                                commandParts.AddRange(new[] { "-pix_fmt", "gbrapf16le" });
                            }
                            else
                            {
                                commandParts.AddRange(new[] { "-pix_fmt", "gbrpf16le" });
                            }
                        }
                        else
                        {
                            if (AlphaChannel == "是")
                            {
                                commandParts.AddRange(new[] { "-pix_fmt", "gbrapf32le" });
                            }
                            else
                            {
                                commandParts.AddRange(new[] { "-pix_fmt", "gbrpf32le" });
                            }
                        }
                        
                        if (ExrEncoding != "无压缩")
                        {
                            commandParts.AddRange(new[] { "-compression", GetExrCompressionCode(ExrEncoding) });
                        }
                        break;
                }

                // 输出路径
                commandParts.Add($"\"{outputPath}\"");

                FFmpegCommand = string.Join(' ', commandParts);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"构造FFmpeg命令时出错: {ex.Message}");
                FFmpegCommand = string.Empty;
            }
        }

        private string GetOutputPath()
        {
            if (string.IsNullOrEmpty(OutputPath))
            {
                var directory = Path.GetDirectoryName(VideoFilePath) ?? string.Empty;
                var fileName = Path.GetFileNameWithoutExtension(VideoFilePath);
                return Path.Combine(directory, fileName, FileNameTemplate);
            }
            else
            {
                return Path.Combine(OutputPath, FileNameTemplate);
            }
        }

        private string GetExrCompressionCode(string encoding)
        {
            return encoding switch
            {
                "ZIP" => "zip",
                "RLE" => "rle",
                "PIZ(慢)" => "piz",
                "PXR24(有损)" => "pxr24",
                _ => "none"
            };
        }

        private async Task StartExtractAsync()
        {
            if (string.IsNullOrEmpty(FFmpegCommand))
            {
                ShowErrorMessage("FFmpeg命令为空，无法开始提取");
                return;
            }

            try
            {
                var outputPath = GetOutputPath();
                var outputDirectory = Path.GetDirectoryName(outputPath);
                
                if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // 创建新任务记录
                var task = new TranscodeTask
                {
                    SourceFile = Path.GetFileName(VideoFilePath),
                    OutputFile = Path.GetFileName(GetOutputPath()),
                    Status = "提取中",
                    Progress = 0,
                    CreateTime = DateTime.Now,
                    FFmpegCommand = FFmpegCommand
                };

                Tasks.Insert(0, task);
                _currentTask = task;
                _lastProgressUpdate = DateTime.MinValue; // 重置进度更新计时器

                // 执行FFmpeg命令
                await ExecuteFFmpegCommandAsync(task);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"开始提取时出错: {ex.Message}");
                if (_currentTask != null)
                {
                    _currentTask.Status = "失败";
                    _currentTask.Progress = 0;
                    _currentTask.CompleteTime = DateTime.Now;
                }
            }
        }

        private void CancelExtract()
        {
            // 清空选中的视频文件
            VideoFilePath = string.Empty;

            // 使用 CancellationTokenSource 请求取消执行中的提取
            try
            {
                _ffmpegCts?.Cancel();
            }
            catch { }

            // 线程安全地交换并处理正在运行的进程实例
            var proc = System.Threading.Interlocked.Exchange(ref _ffmpegProcess, null);
            if (proc != null)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        try
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                        catch
                        {
                            // 忽略杀死进程时的异常
                        }
                    }
                }
                finally
                {
                    proc.Dispose();
                }
            }

            var tokenSource = Interlocked.Exchange(ref _ffmpegCts, null);
            tokenSource?.Dispose();

            if (_currentTask != null)
            {
                _currentTask.Status = "已取消";
                _currentTask.Progress = 0;
                _currentTask.CompleteTime = DateTime.Now;
                _currentTask = null;
            }
        }

        private async Task ExecuteFFmpegCommandAsync(TranscodeTask task)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = FFmpegCommand.Replace("ffmpeg ", "").Trim(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // 使用局部变量引用 Process，避免并发造成字段为 null 的竞态
            var process = new System.Diagnostics.Process { StartInfo = startInfo };

            // 将共享字段设置为当前实例，CancelExtract 会交换为 null
            _ffmpegProcess = process;

            // 创建 CancellationTokenSource，用于取消 WaitForExitAsync
            var cts = new CancellationTokenSource();
            _ffmpegCts = cts;

            // 收集错误信息
            var errorMessages = new StringBuilder();

            // 处理输出和错误
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // 标准输出通常用于其他信息，进度信息主要在错误流中
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // FFmpeg的进度信息通常在错误流中输出
                    UpdateProgressFromOutput(e.Data, task);
                    errorMessages.AppendLine(e.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cts.Token);

                if (process.ExitCode == 0)
                {
                    task.Status = "完成";
                    task.Progress = 100;
                }
                else
                {
                    task.Status = "失败";
                    task.Progress = 0;
                    
                    // 显示FFmpeg错误信息
                    var errorMessage = errorMessages.ToString();
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        // 提取关键错误信息，截断非必要信息
                        var cleanErrorMessage = ExtractKeyErrorMessage(errorMessage);
                        ShowErrorMessage($"FFmpeg提取失败 (退出码: {process.ExitCode}):\n\n{cleanErrorMessage}");
                    }
                    else
                    {
                        ShowErrorMessage($"FFmpeg提取失败 (退出码: {process.ExitCode})");
                    }
                }

                task.CompleteTime = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                // 用户主动取消操作，不显示错误信息
                task.Status = "已取消";
                task.Progress = 0;
                task.CompleteTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                task.Status = "失败";
                task.Progress = 0;
                task.CompleteTime = DateTime.Now;
                ShowErrorMessage($"执行FFmpeg命令时出错: {ex.Message}");
            }
            finally
            {
                // 清理并释放资源，确保线程安全地清空共享字段
                var proc = System.Threading.Interlocked.Exchange(ref _ffmpegProcess, null);
                try { if (proc != null) proc.Dispose(); } catch { }

                var tokenSource = Interlocked.Exchange(ref _ffmpegCts, null);
                try { tokenSource?.Dispose(); } catch { }

                _currentTask = null;
            }
        }

        private DateTime _lastProgressUpdate = DateTime.MinValue;
        
        private void UpdateProgressFromOutput(string output, TranscodeTask task)
        {
            // 更新进度，从 FFmpeg 输出中提取时间信息
            if (output.Contains("time="))
            {
                var timeMatch = System.Text.RegularExpressions.Regex.Match(output, @"time=([0-9:.]+)");
                if (timeMatch.Success)
                {
                    // 避免过于频繁的进度更新，至少间隔500毫秒
                    var now = DateTime.Now;
                    if ((now - _lastProgressUpdate).TotalMilliseconds < 500 && task.Progress > 0)
                    {
                        return;
                    }
                    _lastProgressUpdate = now;
                    
                    // 解析到时间就更新进度，限制在 95% 以内
                    // 检查退出码，如果为 100 表示成功
                    var current = task.Progress;
                    var next = Math.Min(95, current + 1);
                    
                    // 在UI线程上更新进度
                    var uiDispatcher = FFmpegStudio.App.MainWindow?.DispatcherQueue;
                    if (uiDispatcher != null)
                    {
                        uiDispatcher.TryEnqueue(() => task.Progress = next);
                    }
                    else
                    {
                        task.Progress = next;
                    }
                }
            }
        }

        private string ExtractKeyErrorMessage(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return string.Empty;

            // 提取关键错误行，通常包含"Error"、"error"、"failed"等关键词
            var lines = errorMessage.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var keyLines = new List<string>();

            foreach (var line in lines)
            {
                // 查找包含错误关键词的行
                if (line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("unable", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("cannot", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    // 截断过长的行，保留前200个字符
                    var cleanLine = line.Length > 200 ? line.Substring(0, 200) + "..." : line;
                    keyLines.Add(cleanLine);
                }
            }

            // 如果没有找到关键错误行，返回前3行作为错误信息
            if (keyLines.Count == 0 && lines.Length > 0)
            {
                for (int i = 0; i < Math.Min(3, lines.Length); i++)
                {
                    var cleanLine = lines[i].Length > 200 ? lines[i].Substring(0, 200) + "..." : lines[i];
                    keyLines.Add(cleanLine);
                }
            }

            return string.Join("\n", keyLines);
        }

        private void ShowErrorMessage(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            _ = dialog.ShowAsync();
        }
    }
}
