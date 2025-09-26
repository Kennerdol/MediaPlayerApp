using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace MediaPlayerApp.Views
{
    public partial class AboutWindowView : Window
    {
        public AboutWindowView()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Open license link in default browser
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
