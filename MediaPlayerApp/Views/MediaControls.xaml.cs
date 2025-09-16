using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace MediaPlayerApp.Views
{
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

            

            // Hook volume slider
            volumeSlider.ValueChanged += (s, e) =>
                VolumeChanged?.Invoke(this, volumeSlider.Value / 100.0);

        }

        // 🎵 Events
        public event EventHandler<double>? VolumeChanged;

    }
}
