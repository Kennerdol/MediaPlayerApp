using MediaPlayerApp.Views;
using System.Windows;

namespace MediaPlayerApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // By default load the PlaylistPage
            MainFrame.Navigate(new PlaylistPage());
        }


    }
}