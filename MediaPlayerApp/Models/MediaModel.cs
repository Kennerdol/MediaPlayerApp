using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaPlayerApp.Models
{
    class MediaModel
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Duration { get; set; }
        public string? Genre { get; set; }
        public int Year { get; set; }
        public string? AlbumCover { get; set; } // path to album image
    }
}
