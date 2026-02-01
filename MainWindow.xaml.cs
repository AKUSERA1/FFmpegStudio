using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using FFmpegStudio.Views;

namespace FFmpegStudio
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(HomePage));
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
            {
                NavigateToPage(tag);
            }
        }

        private void NavigateToPage(string tag)
        {
            Type? pageType = tag switch
            {
                "home" => typeof(HomePage),
                "transcode" => typeof(TranscodePage),
                "framesynth" => typeof(FrameSequenceSynthPage),
                "videoextract" => typeof(VideoExtractPage),
                "frametool" => typeof(FrameSequenceToolPage),
                _ => null
            };

            if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = NavView.SettingsItem;
            }
            else
            {
                string? tag = e.SourcePageType.Name switch
                {
                    "HomePage" => "home",
                    "TranscodePage" => "transcode",
                    "FrameSequenceSynthPage" => "framesynth",
                    "VideoExtractPage" => "videoextract",
                    "FrameSequenceToolPage" => "frametool",
                    _ => null
                };

                if (tag != null)
                {
                    foreach (NavigationViewItem item in NavView.MenuItems)
                    {
                        if (item.Tag?.ToString() == tag)
                        {
                            NavView.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }
    }
}
