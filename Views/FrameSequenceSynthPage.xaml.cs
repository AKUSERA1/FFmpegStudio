using FFmpegStudio.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FFmpegStudio.Views
{
    public sealed partial class FrameSequenceSynthPage : Page
    {
        public FrameSequenceSynthPage()
        {
            InitializeComponent();
            this.DataContext = new FrameSequenceSynthViewModel();
        }
    }
}
