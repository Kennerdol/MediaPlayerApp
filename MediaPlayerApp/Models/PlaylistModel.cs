using System.ComponentModel;

namespace MediaPlayerApp.Models
{
    public class PlaylistModel : INotifyPropertyChanged
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? FilePath { get; set; }
        public string? Duration { get; set; }
        public string? Thumbnail { get; set; }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnPropertyChanged(nameof(IsPlaying));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
