using System;
using System.ComponentModel;
using Microsoft.UI.Dispatching;

namespace FFmpegStudio.Models
{
    public class TranscodeTask : INotifyPropertyChanged
    {
        private string _status = "等待中";
        private int _progress = 0;
        private string _progressText = "0%";

        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string SourceFile { get; set; } = string.Empty;
        
        public string OutputFile { get; set; } = string.Empty;
        
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }
        
        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    ProgressText = $"{_progress}%";
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }
        
        public string ProgressText
        {
            get => _progressText;
            private set
            {
                if (_progressText != value)
                {
                    _progressText = value;
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }
        
        public DateTime CreateTime { get; set; } = DateTime.Now;
        
        public DateTime? CompleteTime { get; set; }
        
        public string FFmpegCommand { get; set; } = string.Empty;
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            // 使用Dispatcher确保在UI线程上执行属性更改通知
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue != null)
            {
                dispatcherQueue.TryEnqueue(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
            else
            {
                // 如果没有DispatcherQueue（可能在后台线程），直接调用
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}