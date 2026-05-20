using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace 코빚노_프로젝트
{
    public static class NaverLocalApi
    {
        // 너 서버 포트에 맞춰라.
        // TourApiService가 http://localhost:5064 쓰면 이것도 5064.
        private const string BASE_URL = "http://localhost:5064";

        public static async Task<List<TourSpotItem>> SearchAsTourSpotsAsync(string keyword)
        {
            using (HttpClient client = new HttpClient())
            {
                string url =
                    BASE_URL +
                    "/api/naver/search?keyword=" +
                    Uri.EscapeDataString(keyword);


                string json = await client.GetStringAsync(url);

                ApiResponse<NaverLocalSearchResponse> response =
                    JsonConvert.DeserializeObject<ApiResponse<NaverLocalSearchResponse>>(json);

                if (response == null || response.Success == false || response.Data == null || response.Data.Items == null)
                {
                    return new List<TourSpotItem>();
                }

                List<TourSpotItem> result = new List<TourSpotItem>();

                int no = 1;

                foreach (NaverLocalItem item in response.Data.Items)
                {
                    double mapX = ConvertNaverMapX(item.MapX);
                    double mapY = ConvertNaverMapY(item.MapY);

                    string address = FirstText(item.RoadAddress, item.Address, "주소 정보 없음");
                    string category = GuessCategory(item.Category, item.Title);

                    result.Add(new TourSpotItem
                    {
                        No = no,
                        ContentId = MakeStableContentId(item.Title, address),
                        ContentTypeId = "NAVER",

                        Icon = GuessIcon(category, item.Title),
                        Name = CleanHtml(item.Title),
                        Address = address,

                        Category = category,
                        SubCategory = "네이버",
                        Rating = "★ 4." + (no % 8),

                        Region = GuessRegion(address),
                        ImageUrl = "",

                        Link = item.Link,

                        MapX = mapX,
                        MapY = mapY
                    });

                    no++;
                }

                return result;
            }
        }

        private static string CleanHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            return text
                .Replace("<b>", "")
                .Replace("</b>", "")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");
        }

        private static double ConvertNaverMapX(string mapx)
        {
            // 네이버 지역검색 mapx는 보통 경도 * 10000000 형태
            double value;

            if (double.TryParse(mapx, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return value / 10000000.0;
            }

            return 0;
        }

        private static double ConvertNaverMapY(string mapy)
        {
            // 네이버 지역검색 mapy는 보통 위도 * 10000000 형태
            double value;

            if (double.TryParse(mapy, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return value / 10000000.0;
            }

            return 0;
        }

        private static string MakeStableContentId(string name, string address)
        {
            string text = (name ?? "") + "|" + (address ?? "");

            unchecked
            {
                int hash = 23;

                for (int i = 0; i < text.Length; i++)
                {
                    hash = hash * 31 + text[i];
                }

                if (hash < 0)
                    hash = -hash;

                if (hash == 0)
                    hash = 999999;

                return hash.ToString();
            }
        }

        private static string GuessCategory(string category, string title)
        {
            string text = (category ?? "") + " " + (title ?? "");

            if (text.Contains("카페") || text.Contains("커피") || text.Contains("디저트") || text.Contains("베이커리"))
                return "카페";

            if (text.Contains("음식") || text.Contains("식당") || text.Contains("한식") || text.Contains("중식") ||
                text.Contains("일식") || text.Contains("양식") || text.Contains("분식") || text.Contains("맛집"))
                return "음식점";

            return string.IsNullOrWhiteSpace(category) ? "장소" : category;
        }

        private static string GuessIcon(string category, string title)
        {
            string text = (category ?? "") + " " + (title ?? "");

            if (text.Contains("카페") || text.Contains("커피"))
                return "☕";

            if (text.Contains("음식") || text.Contains("식당") || text.Contains("맛집") || text.Contains("국수") || text.Contains("분식"))
                return "🍜";

            return "📍";
        }

        private static string GuessRegion(string address)
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

        private static string FirstText(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }
    }

    public class NaverLocalSearchResponse
    {
        public string Keyword { get; set; }
        public int Count { get; set; }
        public List<NaverLocalItem> Items { get; set; }
    }

    public class NaverLocalItem
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Telephone { get; set; }
        public string Address { get; set; }
        public string RoadAddress { get; set; }
        public string MapX { get; set; }
        public string MapY { get; set; }
    }
}