using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace 코빚노_프로젝트
{
    public static class ReviewApi
    {
        private const string BASE_URL = "https://localhost:7089";

        // 리뷰 작성
        public static async Task<bool> AddReviewAsync(ReviewRequest request)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = BASE_URL + "/api/reviews";

                string json = JsonConvert.SerializeObject(request);

                StringContent content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

                HttpResponseMessage response = await client.PostAsync(url, content);

                string responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(responseJson, "리뷰 등록 API 오류");
                    return false;
                }

                ApiResponse<object> result =
                    JsonConvert.DeserializeObject<ApiResponse<object>>(responseJson);

                if (result == null)
                    return false;

                return result.Success;
            }
        }

        // 마이페이지: 내가 쓴 리뷰 조회
        public static async Task<List<ReviewResponse>> GetReviewsByMemberAsync(int memberId)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = BASE_URL + "/api/reviews/member/" + memberId;

                HttpResponseMessage response = await client.GetAsync(url);

                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(json, "내 리뷰 조회 API 오류");
                    return new List<ReviewResponse>();
                }

                ApiResponse<List<ReviewResponse>> result =
                    JsonConvert.DeserializeObject<ApiResponse<List<ReviewResponse>>>(json);

                if (result == null || result.Data == null)
                    return new List<ReviewResponse>();

                return result.Data;
            }
        }

        // 관광지 상세: 특정 관광지 리뷰 목록 조회
        public static async Task<List<ReviewResponse>> GetReviewsByTargetAsync(string targetType, int targetId)
        {
            using (HttpClient client = new HttpClient())
            {
                string url =
                    BASE_URL +
                    "/api/reviews?targetType=" +
                    Uri.EscapeDataString(targetType) +
                    "&targetId=" +
                    targetId;

                HttpResponseMessage response = await client.GetAsync(url);

                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(json, "대상 리뷰 조회 API 오류");
                    return new List<ReviewResponse>();
                }

                ApiResponse<List<ReviewResponse>> result =
                    JsonConvert.DeserializeObject<ApiResponse<List<ReviewResponse>>>(json);

                if (result == null || result.Data == null)
                    return new List<ReviewResponse>();

                return result.Data;
            }
        }
    }
}