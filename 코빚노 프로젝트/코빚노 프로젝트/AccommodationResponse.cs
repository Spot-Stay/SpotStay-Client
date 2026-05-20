namespace 코빚노_프로젝트
{
    public class AccommodationResponse
    {
        public int AccomId { get; set; }

        public string Name { get; set; }

        public string Address { get; set; }

        public string AccomType { get; set; }

        public string Phone { get; set; }

        public string ImageUrl { get; set; }

        // 위도
        public double Latitude { get; set; }

        // 경도
        public double Longitude { get; set; }

        public string BookingUrl { get; set; }

        public double DistanceKm { get; set; }
    }
}