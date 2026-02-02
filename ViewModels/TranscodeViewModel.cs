using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using FFmpegStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Threading;
using System.Text;

namespace FFmpegStudio.ViewModels
{
    public class TranscodeViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;
        private readonly Services.FFmpegService _ffmpegService;

        private string _sourceFilePath = string.Empty;
        private string _selectedFormat = "MP4";
        private string _selectedCodecCategory = "H.264";
        private string _selectedEncoder = "libx264";
        private string _resolution = "原始";
        private string _bitrate = "原始";
        private string _frameRate = "原始";
        private string _colorSpace = "BT.709";
        private bool _isCommandManuallyEdited = false;
        private VideoInfo? _videoInfo;
        private bool _isLoadingVideoInfo;
        private bool _isLoadingEncoders;
        private string _ffmpegCommand = string.Empty;
        private string _selectedQualityPreset = "medium";
        private string _qualityPresetParams = string.Empty;
        private TranscodeTask? _currentTask;
        private System.Diagnostics.Process? _ffmpegProcess;
        private CancellationTokenSource? _ffmpegCts;

        public TranscodeViewModel()
        {
            _settingsService = Services.SettingsService.Instance;
            _ffmpegService = Services.FFmpegService.Instance;
            BrowseCommand = new RelayCommand(async _ => await BrowseFileAsync());
            CancelCommand = new RelayCommand(_ => CancelTranscode());
            StartTranscodeCommand = new RelayCommand(async _ => await StartTranscodeAsync());
            _ = LoadEncodersAsync();
        }

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set
            {
                if (SetProperty(ref _sourceFilePath, value))
                {
                    OnPropertyChanged(nameof(HasSourceFile));
                    _ = LoadVideoInfoAsync();
                    if (!_isCommandManuallyEdited)
                    {
                        // 源文件改变时，清空并重新构造命令
                        ConstructFFmpegCommand(true);
                    }
                }
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

        public bool IsLoadingEncoders
        {
            get => _isLoadingEncoders;
            set => SetProperty(ref _isLoadingEncoders, value);
        }

        public bool HasSourceFile => !string.IsNullOrEmpty(_sourceFilePath);
        public bool HasVideoInfo => VideoInfo != null;

        public string SelectedFormat
        {
            get => _selectedFormat;
            set
            {
                if (SetProperty(ref _selectedFormat, value) && !_isCommandManuallyEdited)
                {
                    // 容器改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string SelectedCodecCategory
        {
            get => _selectedCodecCategory;
            set
            {
                if (SetProperty(ref _selectedCodecCategory, value))
                {
                    _ = LoadEncodersAsync();
                }
            }
        }

        public string SelectedEncoder
        {
            get => _selectedEncoder;
            set
            {
                if (SetProperty(ref _selectedEncoder, value))
                {
                    _ = LoadQualityPresetParamsAndConstructCommandAsync();
                }
            }
        }

        public string Resolution
        {
            get => _resolution;
            set
            {
                if (SetProperty(ref _resolution, value) && !_isCommandManuallyEdited)
                {
                    // 分辨率改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string Bitrate
        {
            get => _bitrate;
            set
            {
                if (SetProperty(ref _bitrate, value) && !_isCommandManuallyEdited)
                {
                    // 比特率改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string FrameRate
        {
            get => _frameRate;
            set
            {
                if (SetProperty(ref _frameRate, value) && !_isCommandManuallyEdited)
                {
                    // 帧率改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public bool ShowAdvanced
        {
            get => _settingsService.ShowAdvancedFeatures;
            set
            {
                _settingsService.ShowAdvancedFeatures = value;
                OnPropertyChanged(nameof(ShowAdvanced));
                if (!_isCommandManuallyEdited)
                {
                    // 高级开关改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string ColorSpace
        {
            get => _colorSpace;
            set
            {
                if (SetProperty(ref _colorSpace, value) && !_isCommandManuallyEdited)
                {
                    // 色彩空间改变时，局部更新命令
                    ConstructFFmpegCommand(false);
                }
            }
        }

        public string SelectedQualityPreset
        {
            get => _selectedQualityPreset;
            set
            {
                if (SetProperty(ref _selectedQualityPreset, value))
                {
                    _ = LoadQualityPresetParamsAndConstructCommandAsync();
                }
            }
        }

        public string FFmpegCommand
        {
            get => _ffmpegCommand;
            set
            {
                if (SetProperty(ref _ffmpegCommand, value))
                {
                    // 用户手动编辑了命令
                    _isCommandManuallyEdited = true;
                    // 更新高级参数为"自定义"
                    _resolution = "自定义";
                    _bitrate = "自定义";
                    _frameRate = "自定义";
                    _colorSpace = "自定义";
                    OnPropertyChanged(nameof(Resolution));
                    OnPropertyChanged(nameof(Bitrate));
                    OnPropertyChanged(nameof(FrameRate));
                    OnPropertyChanged(nameof(ColorSpace));
                }
            }
        }

        public string QualityPresetParams
        {
            get => _qualityPresetParams;
            set => SetProperty(ref _qualityPresetParams, value);
        }

        public ObservableCollection<string> QualityPresets { get; } = new() { "high", "medium", "low" };

        public ObservableCollection<string> Formats { get; } = new() { "MP4", "MKV", "AVI", "MOV", "FLV", "WebM" };

        public ObservableCollection<string> CodecCategories { get; } = new()
        {
            "H.264",
            "HEVC",
            "AV1",
            "MPEG4",
            "QuickTime",
            "VP9"
        };

        public ObservableCollection<string> Encoders { get; } = new();

        public ObservableCollection<string> Resolutions { get; } = new() { "原始","1920x1080", "1080x1920", "2560x1440", "1440x2560" };

        public ObservableCollection<string> FrameRates { get; } = new() { "原始","25", "30", "60" };

        public ObservableCollection<string> ColorSpaces { get; } = new() { "BT.709", "BT.601", "BT.2020" };

        public ObservableCollection<TranscodeTask> Tasks { get; } = new();

        public ICommand BrowseCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand StartTranscodeCommand { get; }

        private async Task LoadEncodersAsync()
        {
            IsLoadingEncoders = true;
            try
            {
                var encoders = await _ffmpegService.GetEncodersByCodecCategoryAsync(SelectedCodecCategory);
                Encoders.Clear();
                foreach (var encoder in encoders)
                {
                    Encoders.Add(encoder);
                }
                // 设置默认编码器
                if (Encoders.Count > 0)
                {
                    SelectedEncoder = Encoders[0];
                    _ = LoadQualityPresetParamsAsync();
                }
            }
            finally
            {
                IsLoadingEncoders = false;
            }
        }

        private async Task LoadQualityPresetParamsAndConstructCommandAsync()
        {
            if (string.IsNullOrEmpty(SelectedEncoder))
                return;

            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "QualityPresets.json");
                if (!File.Exists(jsonPath))
                {
                    ShowErrorMessage("QualityPresets.json 文件不存在");
                    _qualityPresetParams = string.Empty;
                    ConstructFFmpegCommand(true);
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var jsonDoc = JsonDocument.Parse(jsonContent);

                if (!jsonDoc.RootElement.TryGetProperty(SelectedEncoder, out var encoderElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 没有对应的质量预设");
                    _qualityPresetParams = string.Empty;
                    ConstructFFmpegCommand(true);
                    return;
                }

                if (!encoderElement.TryGetProperty("presets", out var presetsElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 的预设配置不完整");
                    _qualityPresetParams = string.Empty;
                    ConstructFFmpegCommand(true);
                    return;
                }

                if (!presetsElement.TryGetProperty(SelectedQualityPreset, out var presetElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 没有 {SelectedQualityPreset} 质量预设");
                    _qualityPresetParams = string.Empty;
                    ConstructFFmpegCommand(true);
                    return;
                }

                _qualityPresetParams = presetElement.GetString() ?? string.Empty;
                OnPropertyChanged(nameof(QualityPresetParams));
                
                if (!_isCommandManuallyEdited)
                {
                    ConstructFFmpegCommand(true);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"读取质量预设时出错: {ex.Message}");
                _qualityPresetParams = string.Empty;
                if (!_isCommandManuallyEdited)
                {
                    ConstructFFmpegCommand(true);
                }
            }
        }

        private async Task LoadQualityPresetParamsAsync()
        {
            if (string.IsNullOrEmpty(SelectedEncoder))
                return;

            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "QualityPresets.json");
                if (!File.Exists(jsonPath))
                {
                    ShowErrorMessage("QualityPresets.json 文件不存在");
                    _qualityPresetParams = string.Empty;
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var jsonDoc = JsonDocument.Parse(jsonContent);

                if (!jsonDoc.RootElement.TryGetProperty(SelectedEncoder, out var encoderElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 没有对应的质量预设");
                    _qualityPresetParams = string.Empty;
                    return;
                }

                if (!encoderElement.TryGetProperty("presets", out var presetsElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 的预设配置不完整");
                    _qualityPresetParams = string.Empty;
                    return;
                }

                if (!presetsElement.TryGetProperty(SelectedQualityPreset, out var presetElement))
                {
                    ShowErrorMessage($"编码器 {SelectedEncoder} 没有 {SelectedQualityPreset} 质量预设");
                    _qualityPresetParams = string.Empty;
                    return;
                }

                _qualityPresetParams = presetElement.GetString() ?? string.Empty;
                OnPropertyChanged(nameof(QualityPresetParams));
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"读取质量预设时出错: {ex.Message}");
                _qualityPresetParams = string.Empty;
            }
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

        private void ConstructFFmpegCommand(bool rebuildFromScratch)
        {
            if (string.IsNullOrEmpty(SourceFilePath))
            {
                _ffmpegCommand = string.Empty;
                OnPropertyChanged(nameof(FFmpegCommand));
                return;
            }

            try
            {
                var outputPath = GetOutputFilePath();
                var commandParts = new List<string>();

                // 基础命令部分
                commandParts.AddRange(new[] { "ffmpeg", "-i", $"\"{SourceFilePath}\"" });

                // 视频编码参数
                AddVideoEncodingParameters(commandParts);

                // 音频编码参数
                commandParts.AddRange(new[] { "-c:a", "copy" });

                // 输出路径
                commandParts.Add($"\"{outputPath}\"");

                _ffmpegCommand = string.Join(' ', commandParts);
                OnPropertyChanged(nameof(FFmpegCommand));
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"构造FFmpeg命令时出错: {ex.Message}");
                _ffmpegCommand = string.Empty;
                OnPropertyChanged(nameof(FFmpegCommand));
            }
        }

        private string GetOutputFilePath()
        {
            var outputPath = Path.ChangeExtension(SourceFilePath, "." + SelectedFormat.ToLower());
            
            // 避免输出路径与源文件相同
            if (outputPath == SourceFilePath)
            {
                var directory = Path.GetDirectoryName(SourceFilePath) ?? string.Empty;
                var fileName = Path.GetFileNameWithoutExtension(SourceFilePath);
                outputPath = Path.Combine(directory, $"{fileName}_output.{SelectedFormat.ToLower()}");
            }
            
            return outputPath;
        }

        private void AddVideoEncodingParameters(List<string> commandParts)
        {
            // 视频编码器
            commandParts.AddRange(new[] { "-c:v", SelectedEncoder });

            // 质量预设参数
            if (!string.IsNullOrEmpty(_qualityPresetParams))
            {
                var presetParams = _qualityPresetParams.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                commandParts.AddRange(presetParams);
            }

            // 高级参数
            if (ShowAdvanced)
            {
                AddAdvancedParameters(commandParts);
            }
        }

        private void AddAdvancedParameters(List<string> commandParts)
        {
            // 分辨率
            if (_resolution != "原始" && !string.IsNullOrEmpty(_resolution))
            {
                commandParts.AddRange(new[] { "-s", _resolution });
            }

            // 比特率
            if (_bitrate != "原始" && !string.IsNullOrEmpty(_bitrate))
            {
                commandParts.AddRange(new[] { "-b:v", $"{_bitrate}k" });
            }

            // 帧率
            if (_frameRate != "原始" && !string.IsNullOrEmpty(_frameRate))
            {
                commandParts.AddRange(new[] { "-r", _frameRate });
            }

            // 色彩空间
            if (!string.IsNullOrEmpty(_colorSpace) && _colorSpace != "BT.709")
            {
                commandParts.AddRange(new[] { "-colorspace", _colorSpace });
            }
        }

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
                SourceFilePath = file.Path;
            }
        }

        private async Task LoadVideoInfoAsync()
        {
            if (string.IsNullOrEmpty(SourceFilePath))
            {
                VideoInfo = null;
                return;
            }

            IsLoadingVideoInfo = true;
            try
            {
                VideoInfo = await _ffmpegService.GetVideoInfoAsync(SourceFilePath);
            }
            finally
            {
                IsLoadingVideoInfo = false;
            }
        }

        private void CancelTranscode()
        {
            // 清空选中的源文件
            SourceFilePath = string.Empty;

            // 重置手动编辑标志和参数状态
            _isCommandManuallyEdited = false;
            _resolution = "原始";
            _bitrate = "原始";
            _frameRate = "原始";
            _colorSpace = "BT.709";
            
            // 通知UI参数已更改
            OnPropertyChanged(nameof(Resolution));
            OnPropertyChanged(nameof(Bitrate));
            OnPropertyChanged(nameof(FrameRate));
            OnPropertyChanged(nameof(ColorSpace));

            // 使用 CancellationTokenSource 请求取消执行中的转换
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
                            // 某些平台不支持 entireProcessTree 参数，尝试非参数版本
                            try { proc.Kill(); } catch { }
                        }
                        try { proc.WaitForExit(); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"停止转换时出错: {ex.Message}");
                }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }
            }

            // 更新当前任务状态
            if (_currentTask != null)
            {
                _currentTask.Status = "已取消";
                _currentTask.Progress = 0;
                _currentTask.CompleteTime = DateTime.Now;
                _currentTask = null;
            }

            // 释放并清理 CancellationTokenSource
            var cts = Interlocked.Exchange(ref _ffmpegCts, null);
            try { cts?.Dispose(); } catch { }
        }

        private async Task StartTranscodeAsync()
        {
            if (string.IsNullOrEmpty(FFmpegCommand))
            {
                ShowErrorMessage("FFmpeg命令为空，无法开始转换");
                return;
            }

            string effectiveCommand = FFmpegCommand;

            // 先尝试从命令文本中解析输出路径（回退到自动生成的输出路径）
            string? outputPathFromCommand = ExtractOutputPathFromCommand(FFmpegCommand) ?? GetOutputFilePath();

            try
            {
                if (!string.IsNullOrEmpty(outputPathFromCommand) && File.Exists(outputPathFromCommand))
                {
                    var dialog = new ContentDialog
                    {
                        Title = "输出文件已存在",
                        Content = $"输出文件 \"{Path.GetFileName(outputPathFromCommand)}\" 已存在。是否覆盖？",
                        PrimaryButtonText = "覆盖",
                        CloseButtonText = "取消",
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        // 插入 -y 参数用于覆盖输出
                        effectiveCommand = InsertOverwriteFlag(FFmpegCommand);
                    }
                    else
                    {
                        // 用户选择不覆盖，停止执行
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"检查输出文件时出错: {ex.Message}");
                return;
            }

            try
            {
                // 创建新任务记录
                var task = new TranscodeTask
                {
                    SourceFile = Path.GetFileName(SourceFilePath),
                    OutputFile = Path.GetFileName(GetOutputFilePath()),
                    Status = "转换中",
                    Progress = 0,
                    CreateTime = DateTime.Now,
                    FFmpegCommand = FFmpegCommand
                };

                Tasks.Insert(0, task);
                _currentTask = task;

                // 执行FFmpeg命令，可能使用已注入 -y 的 effectiveCommand
                await ExecuteFFmpegCommandAsync(task, effectiveCommand);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"开始转换时出错: {ex.Message}");
                if (_currentTask != null)
                {
                    _currentTask.Status = "失败";
                    _currentTask.Progress = 0;
                    _currentTask.CompleteTime = DateTime.Now;
                }
            }
        }

        // 将 -y 插入到命令的输出参数之前，保持原始引号和顺序
        private static string InsertOverwriteFlag(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return command;

            // 如果已经包含 -y，则直接返回
            if (command.Contains(" -y") || command.Contains("-y ") || command.Trim().EndsWith("-y"))
                return command;

            var tokens = TokenizeCommand(command);
            if (tokens.Count == 0)
                return command;

            // 在最后一个参数（通常为输出文件）之前插入 -y
            int insertIndex = Math.Max(1, tokens.Count - 1);
            tokens.Insert(insertIndex, "-y");

            return string.Join(' ', tokens);
        }

        // 简单的命令行分词，保留引号内容
        private static List<string> TokenizeCommand(string command)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(command))
                return tokens;

            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(c);
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens;
        }

        // 从 FFmpeg 命令中解析最后一个参数作为输出文件路径（去除引号）
        private static string? ExtractOutputPathFromCommand(string command)
        {
            var tokens = TokenizeCommand(command);
            if (tokens.Count == 0)
                return null;

            var last = tokens.Last().Trim();
            if (last.StartsWith("\"") && last.EndsWith("\""))
            {
                last = last.Substring(1, last.Length - 2);
            }
            return last;
        }

        private async Task ExecuteFFmpegCommandAsync(TranscodeTask task, string? overrideCommand = null)
        {
            var commandToUse = overrideCommand ?? FFmpegCommand;

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = commandToUse.Replace("ffmpeg ", "").Trim(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // 使用局部变量引用 Process，避免并发造成字段为 null 的竞态
            var process = new System.Diagnostics.Process { StartInfo = startInfo };

            // 将共享字段设置为当前实例，CancelTranscode 会交换为 null
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
                    UpdateProgressFromOutput(e.Data, task);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    UpdateProgressFromOutput(e.Data, task);
                    
                    // 收集错误信息
                    if (e.Data.Contains("Error") || e.Data.Contains("error") || e.Data.Contains("failed") || e.Data.Contains("Invalid"))
                    {
                        errorMessages.AppendLine(e.Data);
                    }
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 等待进程退出（可被 CancellationToken 取消）
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 取消时将任务标记为已取消
                    task.Status = "已取消";
                    task.Progress = 0;
                    task.CompleteTime = DateTime.Now;
                    return;
                }

                // 进程正常结束，检查退出码（使用本地变量避免竞态）
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
                        ShowErrorMessage($"FFmpeg转换失败 (退出码: {process.ExitCode}):\n\n{errorMessage}");
                    }
                    else
                    {
                        ShowErrorMessage($"FFmpeg转换失败 (退出码: {process.ExitCode})");
                    }
                }

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

        private void UpdateProgressFromOutput(string output, TranscodeTask task)
        {
            // 简单的进度解析逻辑，可以根据实际FFmpeg输出调整
            if (output.Contains("time="))
            {
                // 尝试解析时间进度
                var timeMatch = System.Text.RegularExpressions.Regex.Match(output, @"time=([0-9:.]+)");
                if (timeMatch.Success && VideoInfo != null && VideoInfo.Duration.TotalSeconds > 0)
                {
                    var currentTime = TimeSpan.Parse(timeMatch.Groups[1].Value);
                    var progress = (int)((currentTime.TotalSeconds / VideoInfo.Duration.TotalSeconds) * 100);
                    progress = Math.Min(100, Math.Max(0, progress));

                    // 在UI线程上更新进度
                    var uiDispatcher = FFmpegStudio.App.MainWindow?.DispatcherQueue;
                    if (uiDispatcher != null)
                    {
                        uiDispatcher.TryEnqueue(() => task.Progress = progress);
                    }
                    else
                    {
                        task.Progress = progress;
                    }
                }
            }
        }
    }
}
