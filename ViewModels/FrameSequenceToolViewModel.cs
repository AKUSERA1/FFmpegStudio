using System.Collections.ObjectModel;

namespace FFmpegStudio.ViewModels
{
    public class FrameSequenceToolViewModel : ViewModelBase
    {
        private string _selectedTool = string.Empty;

        public string SelectedTool
        {
            get => _selectedTool;
            set => SetProperty(ref _selectedTool, value);
        }

        public ObservableCollection<ToolItem> Tools { get; } = new();

        public FrameSequenceToolViewModel()
        {
            InitializeTools();
        }

        private void InitializeTools()
        {
            Tools.Add(new ToolItem { Name = "重命名工具", Icon = "Rename", Description = "批量重命名帧序列文件" });
            Tools.Add(new ToolItem { Name = "帧序列格式转换", Icon = "Convert", Description = "转换帧序列格式" });
            Tools.Add(new ToolItem { Name = "rife插帧", Icon = "PlaybackRate", Description = "使用RIFE进行帧插值" });
        }
    }

    public class ToolItem
    {
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? Description { get; set; }
    }
}
