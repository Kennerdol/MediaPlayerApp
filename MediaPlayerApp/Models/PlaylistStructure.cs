using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;

namespace MediaPlayerApp.Models
{
    public class PlaylistStructure
    {
        public string PlaylistName { get; set; }
        public List<Track> Tracks { get; set; } = new List<Track>();
        public int LastPlayedIndex { get; set; }
        public int Volume { get; set; }
    }
}
