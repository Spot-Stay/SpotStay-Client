using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 코빚노_프로젝트
{
    public class TourSpotItem
    {
        public int No { get; set; }

        public string ContentId { get; set; }
        public string ContentTypeId { get; set; }

        public string Icon { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }

        public string Category { get; set; }
        public string SubCategory { get; set; }
        public string Rating { get; set; }

        public string Region { get; set; }
        public string ImageUrl { get; set; }

        public double MapX { get; set; }
        public double MapY { get; set; }
        public bool IsFavorite { get; set; }
        public int DistanceMeters { get; set; }
        public string DistanceText { get; set; }
        public string Link { get; set; }

    }
}
