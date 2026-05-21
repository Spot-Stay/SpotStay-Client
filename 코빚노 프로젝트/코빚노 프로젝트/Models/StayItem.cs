using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 코빚노_프로젝트
{
    public class StayItem
    {
        public int No { get; set; }

        public string ContentId { get; set; }
        public string ContentTypeId { get; set; }

        public string Icon { get; set; } = "🏨";
        public string Name { get; set; }
        public string Address { get; set; }

        public string HotelType { get; set; } = "숙소";

        public string Distance { get; set; }
        public int DistanceMeters { get; set; }

        public string Rating { get; set; } = "★ 4.5";
        public string ReviewText { get; set; } = "리뷰 128개";

        public string Price { get; set; } = "요금정보 없음";

        public string Tag1 { get; set; } = "무료주차";
        public string Tag2 { get; set; } = "Wi-Fi";
        public string Tag3 { get; set; } = "예약가능";

        public string BookingUrl { get; set; }

        public string ImageUrl { get; set; }

        public double MapX { get; set; }
        public double MapY { get; set; }

    }
}
