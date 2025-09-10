using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace MediaPlayerApp.Views
{
    /// <summary>
    /// Interaction logic for MediaControls.xaml
    /// </summary>
    public partial class MediaControls : UserControl
    {
        public MediaControls()
        {
            InitializeComponent();

            // Assign PlacementTarget in code
            volumePopup.PlacementTarget = speakerButton;

            // Show popup on mouse enter
            speakerButton.MouseEnter += (s, e) => volumePopup.IsOpen = true;

            // Hide popup on mouse leave (optional: add delay if needed)
            volumePopup.MouseLeave += (s, e) => volumePopup.IsOpen = false;


        }

    }
}
