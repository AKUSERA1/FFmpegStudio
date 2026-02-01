using FFmpegStudio.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FFmpegStudio.Views
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            this.DataContext = new HomeViewModel();
        }
    }
}
