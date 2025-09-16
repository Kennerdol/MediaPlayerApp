using MediaPlayerApp.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Windows.UI.Xaml.Controls;
using Page = System.Windows.Controls.Page;

namespace MediaPlayerApp.Views
{
    public partial class PlaylistPage : Page
    {
        private readonly ObservableCollection<PlaylistModel> _playlist = new();

        public PlaylistPage()
        {
            InitializeComponent();
            PlaylistListView.ItemsSource = _playlist;
        }

        // 🔹 Event to notify MainWindow when a song is chosen
        public event EventHandler<PlaylistModel>? ItemSelected;

        private void PlaylistListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistListView.SelectedItem is PlaylistModel item)
            {
                ItemSelected?.Invoke(this, item);
            }
        }

        

        private void PlaylistListView_Drop(object sender, DragEventArgs e)
        {
            // TODO: Handle drag/drop of media files
        }

        private void PlaylistListView_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }
}
