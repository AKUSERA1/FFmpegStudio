using FFmpegStudio.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FFmpegStudio.Views
{
    public sealed partial class FrameSequenceToolPage : Page
    {
        public FrameSequenceToolPage()
        {
            InitializeComponent();
            this.DataContext = new FrameSequenceToolViewModel();
        }
    }
}
