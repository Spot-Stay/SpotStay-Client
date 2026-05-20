using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace 코빚노_프로젝트
{
    public class TourApiService
    {
        private static readonly HttpClient client = new HttpClient();

        private const string BASE_URL = "http://localhost:5064";

        public async Task<List<TourSpotItem>> SearchTourSpotsAsync(string keyword, int numOfRows = 50)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                keyword = "대전";
            }

            string url =
                BASE_URL
                + "/api/tourapi/search?keyword="
                + Uri.EscapeDataString(keyword);

            string json = await client.GetStringAsync(url);

            ApiResponse<List<TouristSpotResponse>> response =
                JsonConvert.DeserializeObject<ApiResponse<List<TouristSpotResponse>>>(json);

            if (response == null || response.Success == false || response.Data == null)
            {
                return new List<TourSpotItem>();
            }

            List<TourSpotItem> result = new List<TourSpotItem>();

            int no = 1;

            foreach (TouristSpotResponse item in response.Data)
            {
                if (item == null)
                    continue;

                if (item.Longitude == 0 || item.Latitude == 0)
                    continue;

                string category = string.IsNullOrWhiteSpace(item.Category)
                    ? "관광지"
                    : item.Category;

                string name = string.IsNullOrWhiteSpace(item.Name)
                    ? "관광지명 없음"
                    : item.Name;

                string address = string.IsNullOrWhiteSpace(item.Address)
                    ? "주소 정보 없음"
                    : item.Address;

                string imageUrl = GetImageUrl(item);

                result.Add(new TourSpotItem
                {
                    No = no,

                    ContentId = item.ContentId.ToString(),
                    ContentTypeId = "12",

                    Icon = GuessIcon(category, name),
                    Name = name,
                    Address = address,

                    Category = category,
                    SubCategory = "관광",

                    // 일단 기본값. 실제 DB 리뷰 요약은 MainWindow에서 다시 덮어씀.
                    Rating = "★ 4." + (no % 9),

                    Region = GuessRegion(address),

                    // 관광지 이미지
                    ImageUrl = imageUrl,

                    // 카카오맵 기준
                    // MapX = 경도, MapY = 위도
                    MapX = item.Longitude,
                    MapY = item.Latitude
                });

                no++;

                if (result.Count >= numOfRows)
                    break;
            }

            return result;
        }

        private string GetImageUrl(TouristSpotResponse item)
        {
            if (item == null)
                return "";

            if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                return item.ImageUrl;

            if (!string.IsNullOrWhiteSpace(item.FirstImage))
                return item.FirstImage;

            if (!string.IsNullOrWhiteSpace(item.FirstImage2))
                return item.FirstImage2;

            if (!string.IsNullOrWhiteSpace(item.Firstimage))
                return item.Firstimage;

            if (!string.IsNullOrWhiteSpace(item.Firstimage2))
                return item.Firstimage2;

            return "";
        }

        private string GuessRegion(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return "기타";

            if (address.Contains("서울")) return "서울특별시";
            if (address.Contains("제주")) return "제주특별자치도";
            if (address.Contains("대전")) return "대전광역시";
            if (address.Contains("강원")) return "강원도";
            if (address.Contains("경남") || address.Contains("경상남도")) return "경상남도";
            if (address.Contains("전남") || address.Contains("전라남도")) return "전라남도";

            return "기타";
        }

        private string GuessIcon(string category, string title)
        {
            string text = (category ?? "") + " " + (title ?? "");

            if (text.Contains("해변") || text.Contains("해수욕장") || text.Contains("바다"))
                return "🏖️";

            if (text.Contains("국립공원") || text.Contains("산") || text.Contains("오름") || text.Contains("봉"))
                return "🏔️";

            if (text.Contains("문화") || text.Contains("궁") || text.Contains("성") || text.Contains("유적") || text.Contains("박물관"))
                return "🏛️";

            if (text.Contains("숙박") || text.Contains("숙소") || text.Contains("호텔") || text.Contains("펜션"))
                return "🏨";

            if (text.Contains("음식") || text.Contains("맛집"))
                return "🍜";

            if (text.Contains("쇼핑"))
                return "🛍️";

            if (text.Contains("축제"))
                return "🎉";

            return "📍";
        }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public T Data { get; set; }

        public int? TotalCount { get; set; }
    }

    public class TouristSpotResponse
    {
        public int ContentId { get; set; }

        public string Name { get; set; }

        public string Address { get; set; }

        public string Category { get; set; }

        public string Description { get; set; }

        public string Phone { get; set; }

        public string Homepage { get; set; }

        // 서버에서 ImageUrl로 내려올 때
        public string ImageUrl { get; set; }

        // 관광공사 원본 필드가 이런 이름으로 내려올 때 대비
        public string FirstImage { get; set; }

        public string FirstImage2 { get; set; }

        public string Firstimage { get; set; }

        public string Firstimage2 { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string RegionSido { get; set; }

        public string RegionSigungu { get; set; }
    }
}