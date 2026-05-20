using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace 코빚노_프로젝트
{
    public static class AccommodationApi
    {
        private const string BaseUrl = "http://apis.data.go.kr/B551011/KorService2";

        // 관광공사 API 서비스키
        // TourApiService.cs에 있던 키랑 같은 거 사용
        private const string ServiceKey = "823ec144fa6335d234a78fd7e0bec1b1aa5b877def8339b3c7d11aa9b67a6125";

        public static async Task<List<AccommodationResponse>> GetNearbyAsync(double latitude, double longitude, int count = 4)
        {
            if (latitude == 0 || longitude == 0)
                return new List<AccommodationResponse>();

            // 관광공사 위치기반 API
            // contentTypeId=32 : 숙박
            // arrange=E : 거리순
            // radius=20000 : 반경 20km
            string url =
                BaseUrl + "/locationBasedList2" +
                "?serviceKey=" + Uri.EscapeDataString(ServiceKey) +
                "&MobileOS=ETC" +
                "&MobileApp=Spot Stay" +
                "&_type=xml" +
                "&numOfRows=" + count +
                "&pageNo=1" +
                "&arrange=E" +
                "&contentTypeId=32" +
                "&mapX=" + longitude.ToString(CultureInfo.InvariantCulture) +
                "&mapY=" + latitude.ToString(CultureInfo.InvariantCulture) +
                "&radius=20000";

            using (HttpClient client = new HttpClient())
            {
                string xml = await client.GetStringAsync(url);

                if (xml.Contains("SERVICE_KEY_IS_NOT_REGISTERED_ERROR") ||
                    xml.Contains("SERVICE ERROR") ||
                    xml.Contains("OpenAPI_ServiceResponse"))
                {
                    throw new Exception("숙소 관광공사 API 호출 실패\n\n" + xml);
                }

                XDocument doc = XDocument.Parse(xml);

                List<XElement> items = doc.Descendants("item").ToList();

                List<AccommodationResponse> result = new List<AccommodationResponse>();

                int no = 1;

                foreach (XElement item in items)
                {
                    string title = GetValue(item, "title");
                    string addr1 = GetValue(item, "addr1");
                    string tel = GetValue(item, "tel");
                    string firstImage = GetValue(item, "firstimage");
                    string firstImage2 = GetValue(item, "firstimage2");
                    string contentId = GetValue(item, "contentid");

                    double mapX = ToDouble(GetValue(item, "mapx")); // 경도
                    double mapY = ToDouble(GetValue(item, "mapy")); // 위도
                    double distMeter = ToDouble(GetValue(item, "dist"));

                    if (mapX == 0 || mapY == 0)
                        continue;

                    string imageUrl = firstImage;

                    if (string.IsNullOrWhiteSpace(imageUrl))
                        imageUrl = firstImage2;

                    result.Add(new AccommodationResponse
                    {
                        AccomId = ToInt(contentId),
                        Name = title,
                        Address = addr1,
                        AccomType = "숙박",
                        Phone = tel,
                        ImageUrl = imageUrl,

                        Latitude = mapY,
                        Longitude = mapX,

                        DistanceKm = Math.Round(distMeter / 1000.0, 2),

                        BookingUrl = "https://search.naver.com/search.naver?query="
                                     + Uri.EscapeDataString(title + " 예약")
                    });

                    no++;
                }

                return result;
            }
        }

        private static string GetValue(XElement item, string name)
        {
            XElement el = item.Element(name);

            if (el == null)
                return "";

            return el.Value.Trim();
        }

        private static double ToDouble(string value)
        {
            double result;

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                return result;

            return 0;
        }

        private static int ToInt(string value)
        {
            int result;

            if (int.TryParse(value, out result))
                return result;

            return 0;
        }
    }
}