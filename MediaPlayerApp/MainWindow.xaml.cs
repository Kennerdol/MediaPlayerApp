using MediaPlayerApp.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;

namespace MediaPlayerApp
{
    public partial class MainWindow : Window
    {

        private readonly ObservableCollection<PlaylistModel> _playlist = new();
        private readonly Random random = new();
        private readonly Stack<int> playbackHistory = new();
        private DispatcherTimer? timer;
        private int currentTrackIndex = -1;
        private DispatcherTimer? hidePlaylistTimer;

        private bool isDraggingSlider = false;
        private bool isShuffling = false;
        private ICollectionView? _playlistView;

        // fullscreen state
        private bool _isFullScreen = false;
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private ResizeMode _previousResizeMode;

        private DispatcherTimer hideControlsTimer;


        public MainWindow()
        {
            InitializeComponent();

            InitializePlaylist();
            SetupTimer();
            hideControlsTimer = new DispatcherTimer();
            hideControlsTimer.Interval = TimeSpan.FromSeconds(3); // hide controls after 3 seconds
            hideControlsTimer.Tick += (s, e) => HideControls();

            media_Element.MediaOpened += media_Element_MediaOpened;

            hidePlaylistTimer = new DispatcherTimer();
            hidePlaylistTimer.Interval = TimeSpan.FromSeconds(3);
            hidePlaylistTimer.Tick += (s, e) => HidePlaylist();


            media_Element.Volume = VolumeSlider.Value;
            media_Element.MediaEnded += media_Element_MediaEnded;

            // Assign PlacementTarget in code
            volumePopup.PlacementTarget = speakerButton;

            // Show popup on mouse enter
            speakerButton.MouseEnter += (s, e) => volumePopup.IsOpen = true;

            // Hide popup on mouse leave (optional: add delay if needed)
            volumePopup.MouseLeave += (s, e) => volumePopup.IsOpen = false;

            // Update FullScreen button
            UpdateFullScreenButton();

            ApplyTheme("Dark");
        }

        private void ShowControls()
        {
            PlayerControlsGrid.Visibility = Visibility.Visible;
            var fadeIn = (Storyboard)this.FindResource("FadeInControls");
            fadeIn.Begin();

            hideControlsTimer.Stop();
            hideControlsTimer.Start();
        }



        // Method to hide controls
        private void HideControls()
        {
            var fadeOut = (Storyboard)this.FindResource("FadeOutControls");
            fadeOut.Completed += (s, e) => PlayerControlsGrid.Visibility = Visibility.Collapsed;
            fadeOut.Begin();

            hideControlsTimer.Stop();
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

        private void ShowPlaylist()
        {
            PlaylistListView.Visibility = Visibility.Visible;
            var fadeIn = (Storyboard)this.FindResource("FadeInPlaylist");
            fadeIn.Begin();

            hidePlaylistTimer?.Stop();
            hidePlaylistTimer?.Start();
        }

        private void HidePlaylist()
        {
            var fadeOut = (Storyboard)this.FindResource("FadeOutPlaylist");
            fadeOut.Completed += (s, e) => PlaylistListView.Visibility = Visibility.Collapsed;
            fadeOut.Begin();

            hidePlaylistTimer?.Stop();
        }


        // ======================== TOGGLE PLAYLIST ============================

        private GridLength _lastPlaylistWidth = new GridLength(2, GridUnitType.Star);

        //private void TogglePlaylist_Click(object sender, RoutedEventArgs e)
        //{
        //    if (PlaylistGrid.Visibility == Visibility.Visible)
        //    {
        //        // Save current playlist width before hiding
        //        _lastPlaylistWidth = PlaylistColumn.Width;

        //        PlaylistGrid.Visibility = Visibility.Collapsed;
        //        GridPlit.Visibility = Visibility.Collapsed;   // hide splitter too
        //        PlaylistColumn.Width = new GridLength(0);     // collapse playlist column
        //        SplitterColumn.Width = new GridLength(0);     // collapse splitter column
        //    }
        //    else
        //    {
        //        PlaylistGrid.Visibility = Visibility.Visible;
        //        GridPlit.Visibility = Visibility.Visible;
        //        SplitterColumn.Width = new GridLength(5);     // restore splitter width
        //        PlaylistColumn.Width = _lastPlaylistWidth;    // restore playlist width
        //    }
        //}

 

        private void TogglePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistGrid.Visibility == Visibility.Visible)
            {
                // Save current playlist width before hiding
                _lastPlaylistWidth = PlaylistColumn.Width;

                PlaylistGrid.Visibility = Visibility.Collapsed;
                GridPlit.Visibility = Visibility.Collapsed;

                PlaylistColumn.Width = new GridLength(0);         // collapse playlist
                SplitterColumn.Width = new GridLength(0);         // collapse splitter
                MediaColumn.Width = new GridLength(1, GridUnitType.Star); // fill 100%
            }
            else
            {
                PlaylistGrid.Visibility = Visibility.Visible;
                GridPlit.Visibility = Visibility.Visible;

                SplitterColumn.Width = new GridLength(5);         // restore splitter
                PlaylistColumn.Width = _lastPlaylistWidth;        // restore playlist
                MediaColumn.Width = new GridLength(3, GridUnitType.Star); // restore ratio
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
                TimeSlider.Value = media_Element.Position.TotalSeconds;
                CurrentTime.Text = media_Element.Position.ToString(@"hh\:mm\:ss");
                TotalTime.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
            }
        }

        private void ClearPlaylist_Click(object sender, EventArgs e)
        {
            Stop_Click(null!, null!);
            _playlist.Clear();
            UpdateFullScreenButton();
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

                // Optionally update the button icon
                FullScreenImage.Source = new BitmapImage(new Uri("/Icons/exit-fullscreen.png", UriKind.Relative));

                _isFullScreen = true;
                TopMenu.Visibility = Visibility.Collapsed;
                Status.Visibility = Visibility.Collapsed;
                PlayerControlsGrid.Visibility = Visibility.Collapsed;
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
                TopMenu.Visibility = Visibility.Visible;
                Status.Visibility = Visibility.Visible;
                PlayerControlsGrid.Visibility = Visibility.Visible;
            }
            TogglePlaylist_Click(null!, null!);
        }


        // Call this method whenever the playlist changes
        private void UpdateFullScreenButton()
        {
            if (PlaylistListView.Items.Count == 0)
            {
                FullScreen.IsEnabled = false;
            }
            else
            {
                FullScreen.IsEnabled = true;
                PlaylistColumn.IsEnabled = true;
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

        //private void media_Element_MediaOpened(object sender, RoutedEventArgs e)
        //{
        //    if (media_Element.NaturalDuration.HasTimeSpan)
        //    {
        //        TimeSlider.Maximum = media_Element.NaturalDuration.TimeSpan.TotalSeconds;
        //        TimeSlider.SmallChange = 1;
        //        TimeSlider.LargeChange = Math.Max(1, media_Element.NaturalDuration.TimeSpan.TotalSeconds / 10);
        //        TotalTime.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
        //    }

        //    // Detect if media has video
        //    bool hasVideo = media_Element.NaturalVideoWidth > 0 && media_Element.NaturalVideoHeight > 0;

        //    if (hasVideo)
        //    {
        //        // Hide album art/cover panel
        //        SongInfoPanel.Visibility = Visibility.Collapsed;
        //        AlbumThumbnail.Visibility = Visibility.Collapsed;
        //    }
        //    else
        //    {
        //        // Show album art/cover panel
        //        SongInfoPanel.Visibility = Visibility.Visible;
        //        AlbumThumbnail.Visibility = Visibility.Visible;

        //        if (currentTrackIndex >= 0 && currentTrackIndex < _playlist.Count)
        //        {
        //            var track = _playlist[currentTrackIndex];
        //            AlbumThumbnail.Source = new BitmapImage(new Uri("Icons\\musical-note.png", UriKind.Relative));
        //            CurrentTitle.Text = track.Title;
        //            SongArtist.Text = track.Artist;
        //        }
        //    }
        //}



        private void media_Element_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (media_Element.NaturalDuration.HasTimeSpan)
            {
                TimeSlider.Maximum = media_Element.NaturalDuration.TimeSpan.TotalSeconds;
                TimeSlider.SmallChange = 1;
                TimeSlider.LargeChange = Math.Max(1, media_Element.NaturalDuration.TimeSpan.TotalSeconds / 10);
                TotalTime.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");

                // Update current track duration
                if (currentTrackIndex >= 0 && currentTrackIndex < _playlist.Count)
                {
                    var track = _playlist[currentTrackIndex];
                    track.Duration = media_Element.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
                }
            }

            bool hasVideo = media_Element.NaturalVideoWidth > 0 && media_Element.NaturalVideoHeight > 0;

            if (hasVideo)
            {
                SongInfoPanel.Visibility = Visibility.Collapsed;
                AlbumThumbnail.Visibility = Visibility.Collapsed;
            }
            else
            {
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
                    // Skip if already in playlist
                    if (_playlist.Any(p => string.Equals(p.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                        continue;

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
                // your reorder logic stays the same...
            }

            // Update FullScreen button
            UpdateFullScreenButton();
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
            if (_playlist.Count == 0 || currentTrackIndex < 0 || !isPlaying)
                return; // nothing to stop

            media_Element.Stop();
            //media_Element.Source = null;

            // Reset UI
            PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/play.png", UriKind.Relative));
            VisualizerPanel.Visibility = Visibility.Hidden;
            CurrentTime.Text = "00:00:00";
            TimeSlider.Value = 0;
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


        // ======= TimeSlider Drag/Change =====

        private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDraggingSlider)
            {
                CurrentTime.Text = TimeSpan.FromSeconds(TimeSlider.Value).ToString(@"hh\:mm\:ss");
            }
        }


        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //media_Element.Volume = VolumeSlider.Value;
            if (media_Element != null)
            {
                media_Element.Volume = VolumeSlider.Value / 100.0; // scale 0-100 → 0.0-1.0
            }

            // Optional: update icon
            VolumSpeaker.Source = VolumeSlider.Value == 0
                ? new BitmapImage(new Uri("/Icons/mute.png", UriKind.Relative))
                : new BitmapImage(new Uri("/Icons/speaker.png", UriKind.Relative));
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            media_Element.SpeedRatio = SpeedSlider.Value;
        }


        private void speakerButton_Click(object sender, RoutedEventArgs e)
        {
            volumePopup.IsOpen = !volumePopup.IsOpen;
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
                _playlist.Clear();
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
            UpdateFullScreenButton();
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
            UpdateNowPlayingStatus($"{trackToPlay.Title} — {trackToPlay.Artist}");

            PlayMedia();

            // Scroll into view
            PlaylistListView.SelectedIndex = index;
            PlaylistListView.ScrollIntoView(PlaylistListView.SelectedItem);
        }


        private void PlayMedia()
        {
            if (currentTrackIndex < 0 || currentTrackIndex >= _playlist.Count)
                return; // no track loaded, do nothing

            media_Element.Play();
            PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/pause.png", UriKind.Relative));
            isPlaying = true;
            timer?.Start(); // <- start updating the slider/time

            var track = _playlist[currentTrackIndex];
            UpdateNowPlayingStatus($"{track.Title} — {track.Artist}");

            // optionally: VisualizerPanel.Visibility = Visibility.Visible;

        }


        private void PauseMedia()
        {
            media_Element.Pause();
            PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/play.png", UriKind.Relative));
            isPlaying = false;
            timer?.Stop(); // <- stop updating time while paused
        }


        private void LoadPlaylist_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "All Files|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _playlist.Clear();
                foreach (string file in openFileDialog.FileNames)
                {
                    _playlist.Add(new PlaylistModel
                    {
                        FilePath = file,
                        Title = Path.GetFileNameWithoutExtension(file),
                        Artist = "Unknown Artist",
                        Duration = "--:--",
                        Thumbnail = "Images/default_thumbnail.png"
                    });
                }

                PlayTrackByIndex(0);
            }
        }

        private void PlaylistListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistListView.SelectedIndex >= 0)
            {
                PlayTrackByIndex(PlaylistListView.SelectedIndex);

                // Prevent this event from reaching the window
                e.Handled = true;
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

        private bool isPlaylistVisible = true;
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
            switch (currentRepeatMode)
            {
                case RepeatMode.Single:
                    // Replay the same track
                    PlayTrackByIndex(currentTrackIndex);
                    break;

                case RepeatMode.All:
                    int nextIndex;
                    if (isShuffling)
                    {
                        // Pick a random track (not the same one if possible)
                        do
                        {
                            nextIndex = random.Next(_playlist.Count);
                        } while (_playlist.Count > 1 && nextIndex == currentTrackIndex);
                    }
                    else
                    {
                        // Sequential with wrap-around
                        nextIndex = (currentTrackIndex + 1) % _playlist.Count;
                    }

                    PlayTrackByIndex(nextIndex);
                    break;

                case RepeatMode.Off:
                default:
                    // Normal playback, just go to next if available, otherwise stop
                    if (isShuffling)
                    {
                        // Shuffle until playlist ends naturally
                        if (_playlist.Count > 1)
                        {
                            int shuffleIndex;
                            do
                            {
                                shuffleIndex = random.Next(_playlist.Count);
                            } while (shuffleIndex == currentTrackIndex);

                            PlayTrackByIndex(shuffleIndex);
                        }
                        else
                        {
                            Stop_Click(null!, null!);
                        }
                    }
                    else
                    {
                        if (currentTrackIndex < _playlist.Count - 1)
                        {
                            PlayTrackByIndex(currentTrackIndex + 1);
                        }
                        else
                        {
                            Stop_Click(null!, null!); // reached end
                        }
                    }
                    break;
            }
        }


        //private void media_Element_BufferingStarted(object sender, RoutedEventArgs e)
        //{
        //    BufferingProgressBar.Visibility = Visibility.Visible;
        //}

        //private void media_Element_BufferingEnded(object sender, RoutedEventArgs e)
        //{
        //    BufferingProgressBar.Visibility = Visibility.Hidden;
        //}


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

            // Load Themes.xaml
            var themeDict = new ResourceDictionary
            {
                Source = new Uri("/Resources/Themes.xaml", UriKind.Relative)
            };

            // Extract the right sub-dictionary
            if (themeDict[theme + "Theme"] is ResourceDictionary selectedTheme)
            {
                Resources.MergedDictionaries.Add(selectedTheme);
            }
        }


        private void Settings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Visible;
        private void DarkTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("Dark");
        private void LightTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("Light");
        private void LightGreyTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("LightGrey");
        private void CloseSettings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Hidden;


    }
}