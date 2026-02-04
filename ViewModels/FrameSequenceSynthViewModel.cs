using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using FFmpegStudio.Models;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Threading;

namespace FFmpegStudio.ViewModels
{
    public class FrameSequenceSynthViewModel : ViewModelBase
    {
        private readonly Services.SettingsService _settingsService;
        private readonly Services.FFmpegService _ffmpegService;

        private string _frameSequencePath = string.Empty;
        private string _audioFilePath = string.Empty;

        private string _selectedFormat = "MP4";
        private string _selectedCodecCategory = "H.264";
        private string _selectedEncoder = "libx264";
        private string _resolution = "原始";
        private string _bitrate = "5000";
        private string _frameRate = "30";
        private string _selectedQualityPreset = "medium";
        private string _qualityPresetParams = string.Empty;

        private string _selectedAudioCodec = "MP3";
        private string _ffmpegCommand = string.Empty;
        private TranscodeTask? _currentTask;
        private System.Diagnostics.Process? _ffmpegProcess;
        private CancellationTokenSource? _ffmpegCts;

        public FrameSequenceSynthViewModel()
        {
            _settingsService = Services.SettingsService.Instance;
            _ffmpegService = Services.FFmpegService.Instance;

            BrowseFrameSequenceCommand = new RelayCommand(async _ => await BrowseFrameSequenceAsync());
            BrowseAudioCommand = new RelayCommand(async _ => await BrowseAudioAsync());
            StartSynthesisCommand = new RelayCommand(async _ => await StartSynthesisAsync());
            CancelSynthesisCommand = new RelayCommand(_ => CancelSynthesis());

            _ = LoadEncodersAsync();
        }

        #region 公共属性

        public string FrameSequencePath
        {
            get => _frameSequencePath;
            set
            {
                if (SetProperty(ref _frameSequencePath, value))
                {
                    ConstructFFmpegCommand();
                }
            }
        }

        public string AudioFilePath
        {
            get => _audioFilePath;
            set
            {
                if (SetProperty(ref _audioFilePath, value))
                {
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
                    ConstructFFmpegCommand();
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
                if (SetProperty(ref _resolution, value))
                {
                    ConstructFFmpegCommand();
                }
            }
        }

        public string Bitrate
        {
            get => _bitrate;
            set
            {
                if (SetProperty(ref _bitrate, value))
                {
                    ConstructFFmpegCommand();
                }
            }
        }

        /// <summary>
        /// 帧率控件始终可见，不受 ShowAdvanced 控制
        /// </summary>
        public string FrameRate
        {
            get => _frameRate;
            set
            {
                if (SetProperty(ref _frameRate, value))
                {
                    ConstructFFmpegCommand();
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
                ConstructFFmpegCommand();
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

        public string QualityPresetParams
        {
            get => _qualityPresetParams;
            set => SetProperty(ref _qualityPresetParams, value);
        }

        public string SelectedAudioCodec
        {
            get => _selectedAudioCodec;
            set
            {
                if (SetProperty(ref _selectedAudioCodec, value))
                {
                    ConstructFFmpegCommand();
                }
            }
        }

        public string FFmpegCommand
        {
            get => _ffmpegCommand;
            set => SetProperty(ref _ffmpegCommand, value);
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

        public ObservableCollection<string> Resolutions { get; } = new() { "原始", "1920x1080", "1080x1920", "2560x1440", "1440x2560" };

        public ObservableCollection<string> FrameRates { get; } = new() { "24", "25", "30", "60" };

        public ObservableCollection<string> AudioCodecs { get; } = new() { "MP3", "AAC", "MP2", "FLAC", "PCM" };

        public ICommand BrowseFrameSequenceCommand { get; }

        public ICommand BrowseAudioCommand { get; }

        public ObservableCollection<TranscodeTask> Tasks { get; } = new();

        public ICommand StartSynthesisCommand { get; }

        public ICommand CancelSynthesisCommand { get; }

        #endregion

        #region 浏览文件

        private async Task BrowseFrameSequenceAsync()
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                FrameSequencePath = folder.Path;
            }
        }

        private async Task BrowseAudioAsync()
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
                ViewMode = PickerViewMode.List
            };

            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".aac");
            picker.FileTypeFilter.Add(".m4a");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".flac");
            picker.FileTypeFilter.Add(".ogg");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                AudioFilePath = file.Path;
            }
        }

        #endregion

        #region 加载编码器与质量预设

        private async Task LoadEncodersAsync()
        {
            try
            {
                var encoders = await _ffmpegService.GetEncodersByCodecCategoryAsync(SelectedCodecCategory);
                Encoders.Clear();
                foreach (var encoder in encoders)
                {
                    Encoders.Add(encoder);
                }

                if (Encoders.Count > 0)
                {
                    SelectedEncoder = Encoders[0];
                    await LoadQualityPresetParamsAsync();
                    ConstructFFmpegCommand();
                }
            }
            catch
            {
                // 使用默认编码器，不抛出到UI
                if (!Encoders.Contains(_selectedEncoder))
                {
                    Encoders.Add(_selectedEncoder);
                }
                ConstructFFmpegCommand();
            }
        }

        private async Task LoadQualityPresetParamsAndConstructCommandAsync()
        {
            await LoadQualityPresetParamsAsync();
            ConstructFFmpegCommand();
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
                    _qualityPresetParams = string.Empty;
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var jsonDoc = JsonDocument.Parse(jsonContent);

                if (!jsonDoc.RootElement.TryGetProperty(SelectedEncoder, out var encoderElement))
                {
                    _qualityPresetParams = string.Empty;
                    return;
                }

                if (!encoderElement.TryGetProperty("presets", out var presetsElement))
                {
                    _qualityPresetParams = string.Empty;
                    return;
                }

                if (!presetsElement.TryGetProperty(SelectedQualityPreset, out var presetElement))
                {
                    _qualityPresetParams = string.Empty;
                    return;
                }

                _qualityPresetParams = presetElement.GetString() ?? string.Empty;
                OnPropertyChanged(nameof(QualityPresetParams));
            }
            catch
            {
                _qualityPresetParams = string.Empty;
            }
        }

        #endregion

        #region 构造 FFmpeg 命令

        private void ConstructFFmpegCommand()
        {
            if (string.IsNullOrEmpty(FrameSequencePath) || !Directory.Exists(FrameSequencePath))
            {
                _ffmpegCommand = string.Empty;
                OnPropertyChanged(nameof(FFmpegCommand));
                return;
            }

            var sequenceTemplate = GetFrameSequenceTemplate();
            if (string.IsNullOrEmpty(sequenceTemplate))
            {
                _ffmpegCommand = string.Empty;
                OnPropertyChanged(nameof(FFmpegCommand));
                return;
            }

            try
            {
                var commandParts = new List<string> { "ffmpeg" };

                // 帧率
                if (!string.IsNullOrWhiteSpace(FrameRate))
                {
                    commandParts.AddRange(new[] { "-framerate", FrameRate });
                }

                // 输入帧序列
                commandParts.AddRange(new[] { "-i", $"\"{sequenceTemplate}\"" });

                var hasAudio = !string.IsNullOrWhiteSpace(AudioFilePath);
                if (hasAudio)
                {
                    // 输入音频
                    commandParts.AddRange(new[] { "-i", $"\"{AudioFilePath}\"" });
                }

                // 视频编码器
                if (!string.IsNullOrEmpty(SelectedEncoder))
                {
                    commandParts.AddRange(new[] { "-c:v", SelectedEncoder });
                }

                // 质量预设参数
                if (!string.IsNullOrEmpty(_qualityPresetParams))
                {
                    var presetParams = _qualityPresetParams.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    commandParts.AddRange(presetParams);
                }

                // 高级参数
                if (ShowAdvanced)
                {
                    if (_resolution != "原始" && !string.IsNullOrEmpty(_resolution))
                    {
                        commandParts.AddRange(new[] { "-s", _resolution });
                    }

                    if (!string.IsNullOrEmpty(_bitrate))
                    {
                        commandParts.AddRange(new[] { "-b:v", $"{_bitrate}k" });
                    }
                }

                // 音频编码器
                if (hasAudio)
                {
                    var encoder = GetAudioEncoderName();
                    if (!string.IsNullOrEmpty(encoder))
                    {
                        commandParts.AddRange(new[] { "-c:a ",$"{encoder} -shortest" });
                    }
                }

                // 输出文件路径
                var outputPath = GetOutputFilePath();
                commandParts.Add($"\"{outputPath}\"");

                _ffmpegCommand = string.Join(' ', commandParts);
                OnPropertyChanged(nameof(FFmpegCommand));
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"FFmpeg 命令生成失败: {ex.Message}");
                _ffmpegCommand = string.Empty;
                OnPropertyChanged(nameof(FFmpegCommand));
            }
        }

        private string? GetFrameSequenceTemplate()
        {
            try
            {
                var files = Directory.GetFiles(FrameSequencePath);
                if (files.Length == 0)
                    return null;

                // 解析帧序列模板名
                var groups = new Dictionary<(string Base, string Ext, int Pad), int>();

                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    var m = Regex.Match(name, @"^(?<base>.*?)(?<num>\d+)(?<ext>\.[^.]+)$");
                    if (!m.Success)
                        continue;

                    var key = (m.Groups["base"].Value, m.Groups["ext"].Value, m.Groups["num"].Value.Length);
                    if (groups.ContainsKey(key))
                        groups[key]++;
                    else
                        groups[key] = 1;
                }

                if (groups.Count == 0)
                    return null;

                // 选择最优的模板
                var best = groups.OrderByDescending(g => g.Value).First().Key;
                var templateFileName = $"{best.Base}%0{best.Pad}d{best.Ext}";
                return Path.Combine(FrameSequencePath, templateFileName);
            }
            catch
            {
                return null;
            }
        }

        private string GetOutputFilePath()
        {
            var directory = FrameSequencePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folderName = Path.GetFileName(directory);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = "output";
            }

            var ext = string.IsNullOrWhiteSpace(SelectedFormat) ? "mp4" : SelectedFormat.ToLowerInvariant();
            return Path.Combine(directory, $"{folderName}_output.{ext}");
        }

        private string? GetAudioEncoderName()
        {
            return SelectedAudioCodec switch
            {
                "MP3" => "libmp3lame",
                "AAC" => "aac",
                "MP2" => "mp2",
                "FLAC" => "flac",
                "PCM" => "pcm_s16le",
                _ => null
            };
        }

        private static void ShowErrorMessage(string message)
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

        #endregion

        #region 合成任务控制

        private async Task StartSynthesisAsync()
        {
            if (string.IsNullOrWhiteSpace(FFmpegCommand))
            {
                ShowErrorMessage("FFmpeg 命令为空，无法开始合成");
                return;
            }

            // 检验帧序列路径
            if (string.IsNullOrWhiteSpace(FrameSequencePath) || !Directory.Exists(FrameSequencePath))
            {
                ShowErrorMessage("帧序列文件夹无效");
                return;
            }

            string effectiveCommand = FFmpegCommand;

            // 从命令文本中解析输出路径（回退到自动生成的输出路径）
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
                var task = new TranscodeTask
                {
                    SourceFile = Path.GetFileName(FrameSequencePath),
                    OutputFile = Path.GetFileName(GetOutputFilePath()),
                    Status = "合成中",
                    Progress = 0,
                    CreateTime = DateTime.Now,
                    FFmpegCommand = FFmpegCommand
                };

                Tasks.Insert(0, task);
                _currentTask = task;

                await ExecuteFFmpegCommandAsync(task, effectiveCommand);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"开始合成时出错: {ex.Message}");
                if (_currentTask != null)
                {
                    _currentTask.Status = "失败";
                    _currentTask.Progress = 0;
                    _currentTask.CompleteTime = DateTime.Now;
                }
            }
        }

        private void CancelSynthesis()
        {
            try
            {
                _ffmpegCts?.Cancel();
            }
            catch { }

            var proc = Interlocked.Exchange(ref _ffmpegProcess, null);
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
                            try { proc.Kill(); } catch { }
                        }
                        try { proc.WaitForExit(); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"终止合成时出错: {ex.Message}");
                }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }
            }

            if (_currentTask != null)
            {
                _currentTask.Status = "已取消";
                _currentTask.Progress = 0;
                _currentTask.CompleteTime = DateTime.Now;
                _currentTask = null;
            }

            var cts = Interlocked.Exchange(ref _ffmpegCts, null);
            try { cts?.Dispose(); } catch { }
        }

        private async Task ExecuteFFmpegCommandAsync(TranscodeTask task, string? overrideCommand = null)
        {
            var commandToUse = overrideCommand ?? FFmpegCommand;

            // 获取用户指定的 FFmpeg 路径
            var ffmpegPath = _settingsService.FFmpegPath;
            var executablePath = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = commandToUse.Replace("ffmpeg ", "").Trim(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new System.Diagnostics.Process { StartInfo = startInfo };
            _ffmpegProcess = process;

            var cts = new CancellationTokenSource();
            _ffmpegCts = cts;

            var errorMessages = new StringBuilder();

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

                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    task.Status = "已取消";
                    task.Progress = 0;
                    task.CompleteTime = DateTime.Now;
                    return;
                }

                if (process.ExitCode == 0)
                {
                    task.Status = "完成";
                    task.Progress = 100;
                }
                else
                {
                    task.Status = "失败";
                    task.Progress = 0;

                    var errorMessage = errorMessages.ToString();
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        ShowErrorMessage($"FFmpeg 合成失败 (退出码: {process.ExitCode}):\n\n{errorMessage}");
                    }
                    else
                    {
                        ShowErrorMessage($"FFmpeg 合成失败 (退出码: {process.ExitCode})");
                    }
                }

                task.CompleteTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                task.Status = "失败";
                task.Progress = 0;
                task.CompleteTime = DateTime.Now;
                ShowErrorMessage($"执行 FFmpeg 命令时出错: {ex.Message}");
            }
            finally
            {
                var proc = Interlocked.Exchange(ref _ffmpegProcess, null);
                try { if (proc != null) proc.Dispose(); } catch { }

                var tokenSource = Interlocked.Exchange(ref _ffmpegCts, null);
                try { tokenSource?.Dispose(); } catch { }

                _currentTask = null;
            }
        }

        private static string InsertOverwriteFlag(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return command;

            if (command.Contains(" -y") || command.Contains("-y ") || command.Trim().EndsWith("-y"))
                return command;

            var tokens = TokenizeCommand(command);
            if (tokens.Count == 0)
                return command;

            int insertIndex = Math.Max(1, tokens.Count - 1);
            tokens.Insert(insertIndex, "-y");

            return string.Join(' ', tokens);
        }

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

        private static string? ExtractOutputPathFromCommand(string command)
        {
            var tokens = TokenizeCommand(command);
            if (tokens.Count == 0)
                return null;

            var last = tokens[^1].Trim();
            if (last.StartsWith("\"") && last.EndsWith("\""))
            {
                last = last.Substring(1, last.Length - 2);
            }
            return last;
        }

        private void UpdateProgressFromOutput(string output, TranscodeTask task)
        {
            // 更新进度，从 FFmpeg 输出中提取时间信息
            if (output.Contains("time="))
            {
                var timeMatch = Regex.Match(output, @"time=([0-9:.]+)");
                if (timeMatch.Success)
                {
                    // 解析到时间就更新进度，限制在 95% 以内
                    // 检查退出码，如果为 100 表示成功
                    var current = task.Progress;
                    var next = Math.Min(95, current + 1);
                    
                    // 在UI线程上更新进度
                    var uiDispatcher = App.MainWindow?.DispatcherQueue;
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

        #endregion
    }
}
