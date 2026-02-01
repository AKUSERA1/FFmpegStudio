using FFmpegStudio.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FFmpegStudio.Views
{
    public sealed partial class VideoExtractPage : Page
    {
        public VideoExtractPage()
        {
            InitializeComponent();
            this.DataContext = new VideoExtractViewModel();
        }
    }
}
