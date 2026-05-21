using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace 코빚노_프로젝트
{
    public class FavoriteApiResult<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonProperty("totalCount")]
        public int? TotalCount { get; set; }
    }

    public class FavoriteRequest
    {
        [JsonProperty("memberId")]
        public int MemberId { get; set; }

        [JsonProperty("targetType")]
        public string TargetType { get; set; }

        [JsonProperty("targetId")]
        public int TargetId { get; set; }
    }

    public class FavoriteResponse
    {
        [JsonProperty("favoriteId")]
        public int FavoriteId { get; set; }

        [JsonProperty("memberId")]
        public int MemberId { get; set; }

        [JsonProperty("targetType")]
        public string TargetType { get; set; }

        [JsonProperty("targetId")]
        public int TargetId { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        // 서버 Favorite 모델에 있으면 자동으로 들어옴
        [JsonProperty("targetName")]
        public string TargetName { get; set; }
    }

    public static class FavoriteApi
    {
        private static readonly HttpClient client = new HttpClient();

        private const string BASE_URL = "https://localhost:7089";

        // 즐겨찾기 추가
        public static async Task<bool> AddFavoriteAsync(FavoriteRequest request)
        {
            try
            {
                string url = BASE_URL + "/api/favorites";

                string json = JsonConvert.SerializeObject(request);

                StringContent content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseJson = await response.Content.ReadAsStringAsync();

                FavoriteApiResult<object> result =
                    JsonConvert.DeserializeObject<FavoriteApiResult<object>>(responseJson);

                return result != null && result.Success;
            }
            catch
            {
                return false;
            }
        }

        // 회원별 즐겨찾기 조회
        public static async Task<List<FavoriteResponse>> GetFavoritesByMemberAsync(int memberId)
        {
            try
            {
                string url = BASE_URL + "/api/favorites?memberId=" + memberId;

                HttpResponseMessage response = await client.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new List<FavoriteResponse>();
                }

                FavoriteApiResult<List<FavoriteResponse>> result =
                    JsonConvert.DeserializeObject<FavoriteApiResult<List<FavoriteResponse>>>(json);

                if (result == null || result.Success == false || result.Data == null)
                    return new List<FavoriteResponse>();

                return result.Data;
            }
            catch
            {
                return new List<FavoriteResponse>();
            }
        }

        // 즐겨찾기 삭제
        public static async Task<bool> DeleteFavoriteAsync(int favoriteId)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = BASE_URL + "/api/favorites/" + favoriteId;

                HttpResponseMessage response = await client.DeleteAsync(url);

                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Windows.MessageBox.Show(json, "즐겨찾기 삭제 API 오류");
                    return false;
                }

                ApiResponse<object> result =
                    Newtonsoft.Json.JsonConvert.DeserializeObject<ApiResponse<object>>(json);

                if (result == null)
                    return false;

                return result.Success;
            }
        }

    }
}