using FFmpegStudio.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FFmpegStudio.Views
{
    public sealed partial class TranscodePage : Page
    {
        public TranscodePage()
        {
            InitializeComponent();
            this.DataContext = new TranscodeViewModel();
        }
    }
}
