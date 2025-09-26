using MediaPlayerApp.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MediaPlayerApp
{
    public partial class MainWindow : Window
    {

        private readonly ObservableCollection<PlaylistModel> _playlist = new();
        private readonly Random random = new();
        private readonly Stack<int> playbackHistory = new();
        private DispatcherTimer? timer;
        private int currentTrackIndex = -1;
        private Point _lastMousePosition;


        // Controls timer
        private readonly DispatcherTimer controlsHideTimer = new DispatcherTimer();

        private bool isDraggingSlider = false;
        private bool isShuffling = false;
        private ICollectionView? _playlistView;

        // fullscreen state
        private bool _isFullScreen = false;
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private ResizeMode _previousResizeMode;



        public MainWindow()
        {
            InitializeComponent();

            InitializePlaylist();
            UpdateFullScreenButton();
            SetupTimer();


            // Auto-hide controls after 3s
            controlsHideTimer.Interval = TimeSpan.FromSeconds(3);
            controlsHideTimer.Tick += (s, e) =>
            {
                HideControls();
                controlsHideTimer.Stop();
            };

            media_Element.MouseMove += media_Element_MouseMove;

            media_Element.MediaOpened += media_Element_MediaOpened;

            media_Element.Volume = VolumeSlider.Value;
            media_Element.MediaEnded += media_Element_MediaEnded;

            // Assign PlacementTarget in code
            volumePopup.PlacementTarget = speakerButton;

            // Show popup on mouse enter
            speakerButton.MouseEnter += (s, e) => volumePopup.IsOpen = true;

            // Hide popup on mouse leave (optional: add delay if needed)
            volumePopup.MouseLeave += (s, e) => volumePopup.IsOpen = false;

            // Update FullScreen button
            //UpdateFullScreenButton();

            // Apply last saved theme or default to Light
            string lastTheme = Properties.Settings.Default.LastTheme;
            if (string.IsNullOrEmpty(lastTheme))
                lastTheme = "Light";

            ApplyTheme(lastTheme);
        }

        private void InitializePlaylist()
        {
            PlaylistListView.ItemsSource = _playlist;
            _playlistView = CollectionViewSource.GetDefaultView(_playlist);
            _playlistView.Filter = PlaylistFilter;
            PlaylistListView.ItemsSource = _playlistView;
            _playlist.CollectionChanged += Playlist_CollectionChanged;
        }

        private void Playlist_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateTrackCount();
        }

        private void UpdateTrackCount()
        {
            TrackCountText.Content = $"Tracks: {_playlist.Count}";
        }


        private bool PlaylistFilter(object obj)
        {
            if (obj is PlaylistModel item)
            {
                string filter = SearchTextBox.Text?.ToLower() ?? "";
                return string.IsNullOrWhiteSpace(filter) ||
                       (item.Title?.ToLower().Contains(filter) ?? false) ||
                       (item.Artist?.ToLower().Contains(filter) ?? false);
            }
            return false;
        }



        // ======================== TOGGLE PLAYLIST ============================

        private GridLength _lastSplitterWidth;
        private GridLength _lastPlaylistWidth;

        private void TogglePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistGrid.Visibility == Visibility.Visible)
            {

                // Save current playlist width before hiding
                _lastPlaylistWidth = PlaylistColumn.Width;
                _lastSplitterWidth = SplitterColumn.Width;

                PlaylistGrid.Visibility = Visibility.Collapsed;
                GridPlit.Visibility = Visibility.Collapsed;

                PlaylistColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
                MediaColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                PlaylistGrid.Visibility = Visibility.Visible;
                GridPlit.Visibility = Visibility.Visible;

                // Restore previous widths
                SplitterColumn.Width = _lastSplitterWidth;
                PlaylistColumn.Width = _lastPlaylistWidth;
                MediaColumn.Width = new GridLength(3, GridUnitType.Star);
            }
        }


        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _playlistView?.Refresh(); // re-applies the filter
        }

        private void SetupTimer()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (media_Element.NaturalDuration.HasTimeSpan && !isDraggingSlider)
            {
                TimeSlider.Maximum = media_Element.NaturalDuration.TimeSpan.TotalSeconds;
                slider.Maximum = media_Element.NaturalDuration.TimeSpan.TotalSeconds; // Overlay

                TimeSlider.Value = media_Element.Position.TotalSeconds;
                slider.Value = media_Element.Position.TotalSeconds; // Overlay

                CurrentTime.Text = media_Element.Position.ToString(@"hh\:mm\:ss");
                time.Text = media_Element.Position.ToString(@"hh\:mm\:ss"); // Overlay

                TotalTime.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
                TotalDuration.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss"); // Overlay
            }
        }

        private void ClearPlaylist_Click(object sender, EventArgs e)
        {
            // First stop media
            Stop_Click(null!, null!);

            //// Clear the media source so no frame remains on screen
            //media_Element.Source = null;

            // Then clear playlist
            _playlist.Clear();

            // Reset current track index
            currentTrackIndex = -1;

            // Update UI state
            UpdateNowPlayingStatus("Now Playing: No Track Loaded");
            media_Element.Source = null;
            SongInfoPanel.Visibility = Visibility.Visible;
            AlbumThumbnail.Visibility = Visibility.Visible;
            AlbumThumbnail.Source = new BitmapImage(new Uri("/Icons/logo.png", UriKind.Relative));
            CurrentTitle.Text = "";
            SongArtist.Text = "";
            totalTime.Text = "";
            TotalTime.Text = "00:00:00";
            //UpdateFullScreenButton();
        }



        // ==================== Playlist Playback ==========================

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count == 0) return;
            int nextIndex;
            if (isShuffling)
            {
                // Pick a random track (but not the same one if possible)
                do
                {
                    nextIndex = random.Next(_playlist.Count);
                } while (_playlist.Count > 1 && nextIndex == currentTrackIndex);
            }
            else
            {
                // Wrap around to first track if at the end
                nextIndex = (currentTrackIndex + 1) % _playlist.Count;
            }

            PlayTrackByIndex(nextIndex);
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count == 0) return;
            int prevIndex;
            if (isShuffling)
            {
                // Pick a random track (but not the same one if possible)
                do
                {
                    prevIndex = random.Next(_playlist.Count);
                } while (_playlist.Count > 1 && prevIndex == currentTrackIndex);
            }
            else
            {
                // Wrap around to last track if at the first
                prevIndex = (currentTrackIndex - 1 + _playlist.Count) % _playlist.Count;
            }
            PlayTrackByIndex(prevIndex);
        }


        // ============================FULL SCREEN =============================================

        private void FullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (!_isFullScreen)
            {
                // Save current state
                _previousWindowState = this.WindowState;
                _previousWindowStyle = this.WindowStyle;
                _previousResizeMode = this.ResizeMode;

                // Enter fullscreen
                this.WindowStyle = WindowStyle.None;
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowState = WindowState.Maximized;

                media_Element.MouseMove -= media_Element_MouseMove;

                // Optionally update the button icon
                FullScreenImage.Source = new BitmapImage(new Uri("/Icons/exit-fullscreen.png", UriKind.Relative));

                // Hide UI elements
                _isFullScreen = true;
                HideUIs();
                HideControls(); //Show Controls;
                ControlsGrid.Visibility = Visibility.Collapsed;

                // Start hidden overlay behavior
                PlayerControlsGrid.Opacity = 1;
                media_Element.MouseMove += media_Element_MouseMove;

            }
            else
            {
                // Restore window state
                this.WindowStyle = _previousWindowStyle;
                this.ResizeMode = _previousResizeMode;
                this.WindowState = _previousWindowState;

                // Restore the fullscreen icon
                FullScreenImage.Source = new BitmapImage(new Uri("/Icons/full-screen.png", UriKind.Relative));

                _isFullScreen = false;

                // Show UI elements again
                ShowUIs();
                ShowControls();
                ControlsGrid.Visibility = Visibility.Visible;


                media_Element.MouseMove += media_Element_MouseMove;



                // Always visible in windowed mode
                PlayerControlsGrid.Opacity = 0;
                media_Element.MouseMove -= media_Element_MouseMove;
            }
        }


        // Call this method whenever the playlist changes
        private void UpdateFullScreenButton()
        {
            // Disable if playlist empty
            if (PlaylistListView.Items.Count == 0)
            {
                Full_Screen.IsEnabled = false;
            }

            // Check if current media is video
            if (media_Element.NaturalVideoWidth == 0 && media_Element.NaturalVideoHeight == 0)
            {
                // No video, only audio
                Full_Screen.IsEnabled = false;
            }
            else
            {
                // Video is present
                Full_Screen.IsEnabled = true;
            }
        }


        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (_isFullScreen && e.Key == Key.Escape)
            {
                FullScreen_Click(null!, null!); // Exit fullscreen
            }
        }

        // ================================================================================


        private void media_Element_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (media_Element.NaturalDuration.HasTimeSpan)
            {
                TimeSlider.Maximum = media_Element.NaturalDuration.TimeSpan.TotalSeconds;
                TimeSlider.SmallChange = 1;

                slider.Maximum = media_Element.NaturalDuration.TimeSpan.TotalSeconds;
                slider.SmallChange = 1;

                TimeSlider.LargeChange = Math.Max(1, media_Element.NaturalDuration.TimeSpan.TotalSeconds / 10);
                slider.LargeChange = Math.Max(1, media_Element.NaturalDuration.TimeSpan.TotalSeconds / 10);

                TotalTime.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
                TotalDuration.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");

                // Update current track duration
                if (currentTrackIndex >= 0 && currentTrackIndex < _playlist.Count)
                {
                    var track = _playlist[currentTrackIndex];
                    track.Duration = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
                }
            }

            bool hasVideo = media_Element.NaturalVideoWidth > 0 && media_Element.NaturalVideoHeight > 0;

            if (hasVideo)
            {
                // Enable fullscreen if playlist isn’t empty
                Full_Screen.IsEnabled = PlaylistListView.Items.Count > 0;

                SongInfoPanel.Visibility = Visibility.Collapsed;
                AlbumThumbnail.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Disable fullscreen for audio
                Full_Screen.IsEnabled = false;

                SongInfoPanel.Visibility = Visibility.Visible;
                AlbumThumbnail.Visibility = Visibility.Visible;

                if (currentTrackIndex >= 0 && currentTrackIndex < _playlist.Count)
                {
                    var track = _playlist[currentTrackIndex];
                    AlbumThumbnail.Source = new BitmapImage(new Uri("Icons\\musical-note.png", UriKind.Relative));
                    CurrentTitle.Text = track.Title;
                    SongArtist.Text = track.Artist;
                    totalTime.Text = track.Duration;
                }
            }
        }



        // ============================= DRAG AND DROP AND REORDER ========================

        private void PlaylistListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                bool wasEmpty = _playlist.Count == 0;

                foreach (string file in files)
                {
                    //// Skip if already in playlist
                    //if (_playlist.Any(p => string.Equals(p.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                    //    continue;

                    _playlist.Add(new PlaylistModel
                    {
                        FilePath = file,
                        Title = Path.GetFileNameWithoutExtension(file),
                        Artist = "Unknown Artist",
                        Duration = "--:--",
                        Thumbnail = "Images/default_thumbnail.png"
                    });
                }

                // Only auto-play if playlist was empty before
                if (wasEmpty && _playlist.Count > 0)
                {
                    PlayTrackByIndex(0);
                }
            }
            else if (e.Data.GetDataPresent(typeof(PlaylistModel)))
            {
                // ===== Reorder logic =====
                var droppedTrack = e.Data.GetData(typeof(PlaylistModel)) as PlaylistModel;
                var target = ((FrameworkElement)e.OriginalSource).DataContext as PlaylistModel;

                if (droppedTrack == null || target == null || droppedTrack == target)
                    return;

                int oldIndex = _playlist.IndexOf(droppedTrack);
                int newIndex = _playlist.IndexOf(target);

                if (oldIndex != newIndex && oldIndex >= 0 && newIndex >= 0)
                {
                    _playlist.Move(oldIndex, newIndex);
                }
            }

            // Update FullScreen button
            //UpdateFullScreenButton();
        }


        // ===================================== CONTROLS ==================================

        private bool isPlaying = false; // track state manually

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            // If no media loaded, open file dialog
            if (_playlist.Count == 0)
            {
                OpenFile_Click(sender, e); // reuse your existing method
                return; // exit, PlayTrackByIndex will start playback
            }

            // Otherwise, toggle play/pause
            if (isPlaying)
            {
                PauseMedia();
                var Currettrack = _playlist[currentTrackIndex];
                UpdateNowPlayingStatus($"Paused: {Currettrack.Title} — {Currettrack.Artist}");
            }

            else
            {
                PlayMedia();
                var track = _playlist[currentTrackIndex];
                UpdateNowPlayingStatus($"{track.Title} — {track.Artist}");
            }

        }


        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count == 0 || currentTrackIndex < 0)
                return; // nothing to stop

            media_Element.Stop();
            //media_Element.Source = null;

            // Reset UI
            PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/play.png", UriKind.Relative));
            Play_PauseImage.Source = new BitmapImage(new Uri("/Icons/play.png", UriKind.Relative));
            VisualizerPanel.Visibility = Visibility.Hidden;
            CurrentTime.Text = "00:00:00";
            TotalTime.Text = "00:00:00";
            TotalDuration.Text = "00:00:00";
            time.Text = "00:00:00";
            TimeSlider.Value = 0;
            slider.Value = 0; //Overlay
            isPlaying = false;
            timer?.Stop();

            // Update status bar only if a track was playing
            var track = _playlist[currentTrackIndex];
            UpdateNowPlayingStatus($"Stopped");
        }


        // On application start
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateNowPlayingStatus("Now Playing: No Track Loaded");
            originalPlaylistWidth = PlaylistColumn.Width;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            media_Element.MediaOpened -= media_Element_MediaOpened;
            media_Element.MediaEnded -= media_Element_MediaEnded;
            media_Element.Close(); // releases resources
            //media_Element.Stop();
        }


        // ======= TimeSlider Drag/Change =====

        private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDraggingSlider)
            {
                CurrentTime.Text = TimeSpan.FromSeconds(TimeSlider.Value).ToString(@"hh\:mm\:ss");
                time.Text = TimeSpan.FromSeconds(slider.Value).ToString(@"hh\:mm\:ss");
            }
        }


        //private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        //{
        //    //media_Element.Volume = VolumeSlider.Value;
        //    if (media_Element != null)
        //    {
        //        media_Element.Volume = VolumeSlider.Value;
        //    }

        //    // Optional: update icon
        //    VolumeSpeaker.Source = VolumeSlider.Value == 0
        //        ? new BitmapImage(new Uri("/Icons/mute.png", UriKind.Relative))
        //        : new BitmapImage(new Uri("/Icons/speaker.png", UriKind.Relative));
        //}

        //private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        //{
        //    media_Element.Volume = VolumeSlider.Value;

        //    // Optional: Change speaker icon based on volume
        //    if (VolumeSlider.Value == 0)
        //        VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/mute.png", UriKind.Relative));
        //    else if (VolumeSlider.Value <= 0.5)
        //        VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/volume-low.png", UriKind.Relative));
        //    else
        //        VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/volume-high.png", UriKind.Relative));
        //}


        private void ChangeSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem clickedItem && double.TryParse(clickedItem.Tag?.ToString(), out double speed))
            {
                // Set the speed
                media_Element.SpeedRatio = speed;

                // Uncheck all siblings
                if (clickedItem.Parent is MenuItem parent)
                {
                    foreach (var item in parent.Items)
                    {
                        if (item is MenuItem mi)
                            mi.IsChecked = false;
                    }
                }

                // Check the clicked item
                clickedItem.IsChecked = true;
            }
        }


        private void SpeakerButton_Click(object sender, RoutedEventArgs e)
        {
            volumePopup.IsOpen = !volumePopup.IsOpen;
        }

        private void HideControls()
        {
            if (_isFullScreen)
                PlayerControlsGrid.Visibility = Visibility.Collapsed;
        }

        //private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        //{
        //    media_Element.Volume = VolumeSlider.Value;

        //    // Optional: Change speaker icon based on volume
        //    if (VolumeSlider.Value == 0 || Volume_Slider.Value == 0)
        //    {
        //        VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/mute.png", UriKind.Relative));
        //        VolumeIcon.Source = new BitmapImage(new Uri("/Icons/mute.png", UriKind.Relative));
        //    }
                
        //    else if (VolumeSlider.Value < 0.5 || Volume_Slider.Value < 0.5)
        //    {
        //        VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/volume-low.png", UriKind.Relative));
        //        VolumeIcon.Source = new BitmapImage(new Uri("/Icons/volume-low.png", UriKind.Relative));
        //    }

        //    else
        //    {
        //        VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/volume-high.png", UriKind.Relative));
        //        VolumeIcon.Source = new BitmapImage(new Uri("/Icons/volume-high.png", UriKind.Relative));
        //    }
                
        //}


        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (media_Element == null) return;

            // Determine which slider fired
            Slider slider = sender as Slider;
            if (slider == null) return;

            // Update media volume
            media_Element.Volume = slider.Value;

            // Update the corresponding icon
            if (slider == VolumeSlider && VolumeSpeaker != null)
            {
                VolumeSpeaker.Source = GetVolumeIcon(slider.Value);
            }
            else if (slider == Volume_Slider && VolumeIcon != null)
            {
                VolumeIcon.Source = GetVolumeIcon(slider.Value);
            }

            // Optional: keep both sliders in sync
            if (slider != VolumeSlider && VolumeSlider != null)
                VolumeSlider.Value = slider.Value;
            if (slider != Volume_Slider && Volume_Slider != null)
                Volume_Slider.Value = slider.Value;
        }

        // Helper method to get the correct icon
        private BitmapImage GetVolumeIcon(double value)
        {
            string iconPath;
            if (value == 0) iconPath = "/Icons/mute.png";
            else if (value < 0.5) iconPath = "/Icons/volume-low.png";
            else iconPath = "/Icons/volume-high.png";

            return new BitmapImage(new Uri(iconPath, UriKind.Relative));
        }


        private double _lastVolume = 0.5; // default

        private void VolumeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (media_Element.Volume > 0)
            {
                // Save current volume and mute
                _lastVolume = media_Element.Volume;
                media_Element.Volume = 0;
                VolumeSlider.Value = 0;

                VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/mute.png", UriKind.Relative));
            }
            else
            {
                // Restore volume
                media_Element.Volume = _lastVolume;
                VolumeSlider.Value = _lastVolume;

                if (_lastVolume < 0.5)
                    VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/volume-low.png", UriKind.Relative));
                else
                    VolumeSpeaker.Source = new BitmapImage(new Uri("/Icons/volume-high.png", UriKind.Relative));
            }
        }


        private void ShowControls()
        {
            if (_isFullScreen)
                PlayerControlsGrid.Visibility = Visibility.Visible;
        }




        private void HideUIs()
        {
            TopMenu.Visibility = Visibility.Collapsed;
            Status.Visibility = Visibility.Collapsed;
            PlaylistColumn.Width = new GridLength(0);         // collapse playlist
            SplitterColumn.Width = new GridLength(0);         // collapse splitter
            GridPlit.Visibility = Visibility.Collapsed;
        }

        private void ShowUIs()
        {
            TopMenu.Visibility = Visibility.Visible;
            Status.Visibility = Visibility.Visible;
            PlaylistColumn.Width = new GridLength(2, GridUnitType.Star); // restore playlist width
            SplitterColumn.Width = new GridLength(3);         // restore splitter width
            GridPlit.Visibility = Visibility.Visible;
        }



        private void media_Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isFullScreen) return;

            Point currentPos = e.GetPosition(media_Element);

            // Only trigger if mouse really moved
            if (currentPos != _lastMousePosition)
            {
                ShowControls();

                controlsHideTimer.Stop();
                controlsHideTimer.Start();

                _lastMousePosition = currentPos;
            }
        }



        // ───────────────────────────────
        // Slider Seek
        // ───────────────────────────────
        private void TimeSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingSlider = true;
        }

        private void TimeSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingSlider = false;
            media_Element.Position = TimeSpan.FromSeconds(TimeSlider.Value);
            media_Element.Position = TimeSpan.FromSeconds(slider.Value);
        }


        // ───────────────────────────────
        // Playlist Loaders
        // ───────────────────────────────
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Media Files|*.mp4;*.mov;*.wmv;*.mp3;*.wav;*.wma|All Files|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                //_playlist.Clear();
                _playlist.Add(new PlaylistModel
                {
                    FilePath = openFileDialog.FileName,
                    Title = Path.GetFileNameWithoutExtension(openFileDialog.FileName),
                    Artist = "Unknown Artist",
                    Duration = "--:--",
                    Thumbnail = "Images/default_thumbnail.png"
                });

                PlayTrackByIndex(0);
            }
            // Update FullScreen button
            //UpdateFullScreenButton();
        }


        private void UpdateNowPlayingStatus(string status)
        {
            NowPlayingText.Content = $"Now Playing: {status}";
        }

        private void PlayTrackByIndex(int index)
        {
            if (index < 0 || index >= _playlist.Count) return;

            // Reset previous tracks
            foreach (var track in _playlist)
                track.IsPlaying = false;

            // Set current track
            var trackToPlay = _playlist[index];
            trackToPlay.IsPlaying = true;
            currentTrackIndex = index;
            try
            {
                media_Element.Source = new Uri(trackToPlay.FilePath!);
            }
            catch (Exception ex) {
                MessageBox.Show($"Error loading file: {trackToPlay.FilePath}\n\n{ex.Message}",
                        "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Optionally remove the bad track from playlist
                _playlist.Remove(trackToPlay);
            }

            // Update status bar
            UpdateNowPlayingStatus($"{trackToPlay.Title}");

            PlayMedia();

            // Scroll into view
            PlaylistListView.SelectedIndex = index;
            PlaylistListView.ScrollIntoView(PlaylistListView.SelectedItem);
        }

        private void PlayMedia()
        {
            if (currentTrackIndex < 0 || currentTrackIndex >= _playlist.Count)
                return; // no track loaded, do nothing

            var iconPath = "/Icons/pause.png";
            media_Element.Play();

            PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/pause.png", UriKind.Relative));
            Play_PauseImage.Source = new BitmapImage(new Uri("/Icons/pause.png", UriKind.Relative));

            if (PlayPauseItem.Icon == null)
                PlayPauseItem.Icon = new Image();

            ((Image)PlayPauseItem.Icon).Source = new BitmapImage(new Uri(iconPath, UriKind.Relative));


            isPlaying = true;
            timer?.Start(); // <- start updating the slider/time

            var track = _playlist[currentTrackIndex];
            UpdateNowPlayingStatus($"{track.Title}");

             VisualizerPanel.Visibility = Visibility.Visible;

        }


        // ContextMenu
        private void Playlist_Play_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistListView.SelectedItem is PlaylistModel track)
            {
                currentTrackIndex = _playlist.IndexOf(track);
                PlayMedia();
            }
        }

        private void Playlist_Remove_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistListView.SelectedItem is PlaylistModel track)
            {
                Stop_Click(null!, null!);
                _playlist.Remove(track);
                //if (_playlist.Count == 0)
                //{
                //    Stop_Click(null!, null!);
                //}
            }
            //UpdateFullScreenButton();
        }

        private void Playlist_OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistListView.SelectedItem is PlaylistModel track && File.Exists(track.FilePath))
            {
                string? dir = Path.GetDirectoryName(track.FilePath);
                if (dir != null)
                    System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            //UpdateFullScreenButton();
        }


        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit Duducha?",
                "Exit Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }


        private void PauseMedia()
        {
            media_Element.Pause();
            PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/play.png", UriKind.Relative));
            Play_PauseImage.Source = new BitmapImage(new Uri("/Icons/play.png", UriKind.Relative));
            isPlaying = false;
            timer?.Stop(); // <- stop updating time while paused
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            SearchTextBox.Focus();
        }

        private void LoadPlaylist_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Text Files|*.txt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _playlist.Clear();

                var filePaths = File.ReadAllLines(openFileDialog.FileName);

                foreach (var path in filePaths)
                {
                    if (File.Exists(path)) // only add files that exist
                    {
                        _playlist.Add(new PlaylistModel
                        {
                            FilePath = path,
                            Title = Path.GetFileNameWithoutExtension(path),
                            Artist = "Unknown Artist",
                            Duration = "--:--",
                            Thumbnail = "Images/default_thumbnail.png"
                        });
                    }
                }

                if (_playlist.Count > 0)
                    PlayTrackByIndex(0);
                else
                    MessageBox.Show("No valid media files found in the playlist.");
            }
        }


        private void PlaylistListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistListView.SelectedItem is PlaylistModel selectedTrack)
            {
                PlayTrack(selectedTrack); // Use a method that takes the track directly
                e.Handled = true; // Prevents the event from bubbling up
            }


        }


        //private void PlayTrack(PlaylistModel track)
        //{
        //    if (track == null) return;

        //    // Stop current playback
        //    media_Element.Stop();

        //    // Set the source
        //    media_Element.Source = new Uri(track.FilePath);
        //    media_Element.Play();

        //    // Update currentTrackIndex in the original playlist
        //    currentTrackIndex = _playlist.IndexOf(track);

        //    // Update IsPlaying flags for full playlist
        //    foreach (var t in _playlist)
        //        t.IsPlaying = t == track;

        //    // Update UI
        //    CurrentTitle.Text = track.Title;
        //    SongArtist.Text = track.Artist;
        //    totalTime.Text = track.Duration;

        //    // Refresh the ListView so IsPlaying triggers apply
        //    PlaylistListView.Items.Refresh();

        //    // Scroll into view in filtered list
        //    PlaylistListView.ScrollIntoView(track);

        //    // Update status bar
        //    UpdateNowPlayingStatus($"{track.Title} — {track.Artist}");
        //}


        private void PlayTrack(PlaylistModel track)
        {
            if (track == null) return;

            int index = _playlist.IndexOf(track);
            if (index >= 0)
            {
                PlayTrackByIndex(index);
            }
        }



        // ───────────────────────────────
        // Shuffle & Repeat
        // ───────────────────────────────
        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            isShuffling = !isShuffling;
            ShuffleButton.Background = isShuffling ? Brushes.LightBlue : Brushes.Transparent;
            ShuffleModeText.Content = $"Shuffle: {(isShuffling ? "On" : "Off")}";
        }


        private enum RepeatMode
        {
            Off,
            Single,
            All
        }

        private RepeatMode currentRepeatMode = RepeatMode.Off;

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            // Cycle modes: Off → Single → All → Off
            currentRepeatMode = currentRepeatMode switch
            {
                RepeatMode.Off => RepeatMode.Single,
                RepeatMode.Single => RepeatMode.All,
                RepeatMode.All => RepeatMode.Off,
                _ => RepeatMode.Off
            };

            // Update label
            RepeatModeText.Content = currentRepeatMode switch
            {
                RepeatMode.Off => "Repeat: Off",
                RepeatMode.Single => "Repeat: Single",
                RepeatMode.All => "Repeat: All",
                _ => "Repeat: Off"
            };

            // Update button background
            Repeat.Background = currentRepeatMode == RepeatMode.Off ? Brushes.Transparent : Brushes.LightBlue;

            // Update image
            RepeatImage.Source = currentRepeatMode switch
            {
                RepeatMode.Off => new BitmapImage(new Uri("/Icons/repeat.png", UriKind.Relative)),
                RepeatMode.Single => new BitmapImage(new Uri("/Icons/repeat-one.png", UriKind.Relative)),
                RepeatMode.All => new BitmapImage(new Uri("/Icons/repeat-all.png", UriKind.Relative)),
                _ => new BitmapImage(new Uri("/Icons/repeat.png", UriKind.Relative))
            };
        }

        // ===========  Animating the playlist ==============

        private GridLength originalPlaylistWidth;


        // Helper to check if a control is a child of another
        private bool IsDescendantOf(DependencyObject source, DependencyObject parent)
        {
            while (source != null)
            {
                if (source == parent) return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }


        private void MediaGrid_Drop(object sender, DragEventArgs e)
        {
            PlaylistListView_Drop(sender, e); // reuse drop logic

        }


        private void MediaGrid_PreviewDragOver(object sender, DragEventArgs e)
        {
            // This allows the drop operation
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }


        private void media_Element_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count == 0) return;

            switch (currentRepeatMode)
            {
                case RepeatMode.Single:
                    PlayTrackByIndex(currentTrackIndex);
                    break;

                case RepeatMode.All:
                    PlayNextTrack();
                    break;

                case RepeatMode.Off:
                    if (currentTrackIndex < _playlist.Count - 1)
                        PlayNextTrack();
                    else
                        Stop_Click(null!, null!);
                    break;
            }
        }

        private void PlayNextTrack()
        {
            int nextIndex = isShuffling
                ? random.Next(_playlist.Count)
                : (currentTrackIndex + 1) % _playlist.Count;

            PlayTrackByIndex(nextIndex);
        }


        private void PlaylistListView_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }


        // =============================== Themes  ============================
        private void ApplyTheme(string theme)
        {
            // Clear previous theme dictionaries
            Resources.MergedDictionaries.Clear();

            // Load new theme
            var themeDict = new ResourceDictionary
            {
                Source = new Uri($"/MediaPlayerApp;component/Resources/{theme}Theme.xaml", UriKind.RelativeOrAbsolute)
            };
            Resources.MergedDictionaries.Add(themeDict);

            // Save selected theme to user settings
            Properties.Settings.Default.LastTheme = theme;
            Properties.Settings.Default.Save();
        }


        //private void Settings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Visible;
        private void DarkTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("Dark");
        private void LightTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("Light");
        private void LightGreyTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("LightGrey");
        //private void CloseSettings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Hidden;


        private void media_Element_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"Media failed to load: {e.ErrorException?.Message}",
                            "Playback Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
        }

        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickThreshold = 300; // milliseconds

        private void MediaElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var currentTime = DateTime.Now;
            var elapsed = (currentTime - _lastClickTime).TotalMilliseconds;

            if (elapsed <= DoubleClickThreshold)
            {
                // Double click detected → toggle fullscreen
                FullScreen_Click(sender, null!);
            }

            _lastClickTime = currentTime;
        }

        private Point _dragStartPoint;

        private void PlaylistListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void PlaylistListView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {

                if (sender is ListView listView &&
                    listView.SelectedItem is PlaylistModel track)
                {
                    DragDrop.DoDragDrop(listView, track, DragDropEffects.Move);
                }
            }
        }

        private void speakerButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!volumePopup.IsMouseOver)
            {
                volumePopup.IsOpen = true;
            }
        }

        private void speakerButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!volumePopup.IsMouseOver)
            {
                volumePopup.IsOpen = false;
            }
        }

        private void volumePopup_MouseLeave(object sender, MouseEventArgs e) => volumePopup.IsOpen = false;

        private void volumePopup_MouseEnter(object sender, MouseEventArgs e) => volumePopup.IsOpen = true;


       
        private void SavePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (_playlist.Count == 0)
            {
                MessageBox.Show("No tracks in the playlist to save.");
                return;
            }

            SaveFileDialog saveFileDialog = new()
            {
                Filter = "Text Files|*.txt",
                FileName = "MyPlaylist.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Get all file paths from _playlist
                    var filePaths = _playlist.Select(track => track.FilePath).ToList();

                    // Save all paths to the chosen .txt file
                    File.WriteAllLines(saveFileDialog.FileName, filePaths);

                    MessageBox.Show("Playlist saved successfully!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving playlist: {ex.Message}");
                }
            }
        }

    }
}