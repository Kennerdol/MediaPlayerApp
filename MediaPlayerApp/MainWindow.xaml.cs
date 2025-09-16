using MediaPlayerApp.Mesc;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.UI.Xaml.Controls;
using C = System.Windows.Controls;

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
        private ICollectionView _playlistView;

        // Field to track fullscreen state
        private bool isFullscreen = false;
        // Timer to hide controls when mouse is inactive
        private DispatcherTimer hideControlsTimer;


        public MainWindow()
        {
            InitializeComponent();
            InitializePlaylist();
            SetupTimer();
            hideControlsTimer = new DispatcherTimer();
            hideControlsTimer.Interval = TimeSpan.FromSeconds(3); // hide controls after 3 seconds
            hideControlsTimer.Tick += (s, e) => HideControls();

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

        private void PlayerControlsGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            if (isFullscreen)
                ShowControls();
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


        private void SearchTextBox_TextChanged(object sender, C.TextChangedEventArgs e)
        {
            _playlistView.Refresh(); // re-applies the filter
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



        private void EnterFullscreen()
        {
            this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Maximized;
            isFullscreen = true;

            PlaylistListView.Visibility = Visibility.Collapsed;
            HideControls(); // Hide controls initially
        }

        private void ExitFullscreen()
        {
            this.WindowStyle = WindowStyle.SingleBorderWindow;
            this.WindowState = WindowState.Normal;
            isFullscreen = false;

            PlaylistListView.Visibility = Visibility.Visible;
            ShowControls(); // Show controls when exiting fullscreen
        }

        // ============================= DRAG AND DROP AND REORDER ========================

  

        //private void PlaylistListView_Drop(object sender, DragEventArgs e)
        //{
        //    if (e.Data.GetDataPresent(DataFormats.FileDrop))
        //    {
        //        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

        //        foreach (string file in files)
        //        {
        //            // Skip if already in playlist
        //            if (_playlist.Any(p => string.Equals(p.FilePath, file, StringComparison.OrdinalIgnoreCase)))
        //                continue;

        //            _playlist.Add(new PlaylistModel
        //            {
        //                FilePath = file,
        //                Title = Path.GetFileNameWithoutExtension(file),
        //                Artist = "Unknown Artist",
        //                Duration = "--:--",
        //                Thumbnail = "Images/default_thumbnail.png"
        //            });
        //        }
        //    }
        //}


        private void PlaylistListView_Drop(object sender, DragEventArgs e)
        {
            // Case 1: Dropping new files from Explorer
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

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
            }
            // Case 2: Reordering existing playlist items
            else if (e.Data.GetDataPresent(typeof(PlaylistModel)))
            {
                var droppedData = e.Data.GetData(typeof(PlaylistModel)) as PlaylistModel;
                var target = ((FrameworkElement)e.OriginalSource).DataContext as PlaylistModel;

                if (droppedData == null || target == null || droppedData == target) return;

                int removedIdx = _playlist.IndexOf(droppedData);
                int targetIdx = _playlist.IndexOf(target);

                if (removedIdx < targetIdx)
                {
                    _playlist.Insert(targetIdx + 1, droppedData);
                    _playlist.RemoveAt(removedIdx);
                }
                else
                {
                    int remIdx = removedIdx + 1;
                    if (_playlist.Count + 1 > remIdx)
                    {
                        _playlist.Insert(targetIdx, droppedData);
                        _playlist.RemoveAt(remIdx);
                    }
                }
            }
        }



        // ===================================== Controls ==================================

        private bool isPlaying = false; // track state manually

        //private void PlayPause_Click(object sender, RoutedEventArgs e)
        //{
        //    if (isPlaying)
        //        PauseMedia();
        //    else
        //        PlayMedia();
        //}


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
                PauseMedia();
            else
                PlayMedia();
        }




        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            media_Element.Stop();

            // Reset UI
            PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/play.png", UriKind.Relative));
            VisualizerPanel.Visibility = Visibility.Hidden;
            CurrentTime.Text = "00:00:00";
            TimeSlider.Value = 0;
            isPlaying = false;
            timer.Stop();
        }


        // ======= TimeSlider Drag/Change =====

        private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDraggingSlider)
            {
                CurrentTime.Text = TimeSpan.FromSeconds(TimeSlider.Value).ToString(@"hh\:mm\:ss");
            }
        }


        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            media_Element.IsMuted = !media_Element.IsMuted;

            // Update icon
            MuteImage.Source = media_Element.IsMuted
                ? new BitmapImage(new Uri("/Icons/mute.png", UriKind.Relative))
                : new BitmapImage(new Uri("/Icons/speaker.png", UriKind.Relative));
        }


        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            media_Element.Volume = VolumeSlider.Value;
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
        }



        private void PlayTrackByIndex(int index)
        {
            if (index < 0 || index >= _playlist.Count) return;

            // Only push currentTrackIndex to history if a different track is about to play
            if (currentTrackIndex != -1 && currentTrackIndex != index)
                playbackHistory.Push(currentTrackIndex);

            // Reset previous tracks
            foreach (var track in _playlist)
                track.IsPlaying = false;

            // Set current track
            var trackToPlay = _playlist[index];
            trackToPlay.IsPlaying = true;

            currentTrackIndex = index;
            media_Element.Source = new Uri(trackToPlay.FilePath);
            PlayMedia();

            // Scroll into view
            PlaylistListView.SelectedIndex = index;
            PlaylistListView.ScrollIntoView(PlaylistListView.SelectedItem);
        }



        //private void PlayMedia()
        //{
        //    media_Element.Play();
        //    PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/pause.png", UriKind.Relative));
        //    isPlaying = true;
        //}


        private void PlayMedia()
        {
            media_Element.Play();
            PlayPauseImage.Source = new BitmapImage(new Uri("/Icons/pause.png", UriKind.Relative));
            isPlaying = true;
            timer?.Start(); // <- start updating the slider/time
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Save the original width (2*)
            originalPlaylistWidth = PlaylistColumn.Width;
        }

        //private void TogglePlaylist_Click(object sender, RoutedEventArgs e)
        //{
        //    if (isPlaylistVisible)
        //    {
        //        var animation = new GridLengthAnimation
        //        {
        //            From = new GridLength(PlaylistColumn.ActualWidth),
        //            To = new GridLength(0),
        //            Duration = TimeSpan.FromMilliseconds(300),
        //            FillBehavior = FillBehavior.Stop
        //        };
        //        animation.Completed += (s, ev) => PlaylistColumn.Width = new GridLength(0);
        //        PlaylistColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);


        //        // Animate splitter
        //        var splitterAnimation = new GridLengthAnimation
        //        {
        //            From = new GridLength(SplitterColumn.ActualWidth), // wrap in GridLength
        //            To = new GridLength(0),
        //            Duration = TimeSpan.FromMilliseconds(300),
        //            FillBehavior = FillBehavior.Stop
        //        };
        //        splitterAnimation.Completed += (s, ev) => SplitterColumn.Width = new GridLength(0);
        //        SplitterColumn.BeginAnimation(ColumnDefinition.WidthProperty, splitterAnimation);

        //    }
        //    else
        //    {
        //        // Restore width
        //        PlaylistColumn.Width = originalPlaylistWidth;
        //        SplitterColumn.Width = new GridLength(5); // restore splitter
        //    }

        //    isPlaylistVisible = !isPlaylistVisible;
        //}


        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Don't toggle fullscreen if double-click came from the playlist
            if (e.OriginalSource is DependencyObject source &&
                !IsDescendantOf(source, PlaylistListView))
            {
                if (this.WindowState == WindowState.Normal)
                {
                    this.WindowStyle = WindowStyle.None;
                    this.WindowState = WindowState.Maximized;
                    isFullscreen = true;
                }
                else
                {
                    this.WindowStyle = WindowStyle.SingleBorderWindow;
                    this.WindowState = WindowState.Normal;
                    isFullscreen = false;
                }
            }
        }



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



        private void MediaGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isFullscreen) return;

            Point position = e.GetPosition(this);

            double controlsTop = this.ActualHeight - PlayerControlsGrid.ActualHeight - 50;

            if (position.Y >= controlsTop || position.Y <= this.ActualHeight) // near bottom or anywhere
                ShowControls();
        }


        //private void media_Element_MediaOpened(object sender, RoutedEventArgs e)
        //{
        //    // Example: update slider max when media is loaded
        //    if (media_Element.NaturalDuration.HasTimeSpan)
        //    {
        //        TimeSlider.Maximum = media_Element.NaturalDuration.TimeSpan.TotalMilliseconds;
        //        TotalTime.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
        //    }
        //}

        private void media_Element_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (media_Element.NaturalDuration.HasTimeSpan)
            {
                // Use seconds everywhere (consistent with Timer_Tick which uses TotalSeconds)
                TimeSlider.Maximum = media_Element.NaturalDuration.TimeSpan.TotalSeconds;
                TimeSlider.SmallChange = 1;
                TimeSlider.LargeChange = Math.Max(1, media_Element.NaturalDuration.TimeSpan.TotalSeconds / 10);
                TotalTime.Text = media_Element.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
            }
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
                            Stop_Click(null, null);
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
                            Stop_Click(null, null); // reached end
                        }
                    }
                    break;
            }
        }



        private void media_Element_BufferingStarted(object sender, RoutedEventArgs e)
        {
            BufferingProgressBar.Visibility = Visibility.Visible;
        }

        private void media_Element_BufferingEnded(object sender, RoutedEventArgs e)
        {
            BufferingProgressBar.Visibility = Visibility.Hidden;
        }


        


        private void PlaylistListView_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }


        private void PlayerControlsGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isFullscreen)
            {
                HideControls();
            }
        }


        
        // =============================== Themes  ============================

        private void ApplyTheme(string theme)
        {
            switch (theme)
            {
                case "Dark":
                    Resources["PrimaryBackgroundColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 33, 33));
                    Resources["SecondaryBackgroundColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 44, 44));
                    Resources["Foreground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                    Resources["AccentColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 209, 128));
                    break;

                case "Light":
                    Resources["PrimaryBackgroundColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
                    Resources["SecondaryBackgroundColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray);
                    Resources["Foreground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
                    Resources["AccentColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DodgerBlue);
                    break;

                case "LightGrey":
                    Resources["PrimaryBackgroundColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220));
                    Resources["SecondaryBackgroundColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200));
                    Resources["Foreground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
                    Resources["AccentColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkOrange);
                    break;
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Visible;
        private void DarkTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("Dark");
        private void LightTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("Light");
        private void LightGreyTheme_Click(object sender, RoutedEventArgs e) => ApplyTheme("LightGrey");
        private void CloseSettings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Hidden;


    }
}