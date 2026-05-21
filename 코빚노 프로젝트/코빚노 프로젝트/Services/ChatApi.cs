using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace 코빚노_프로젝트
{
    public static class ChatApi
    {
        private const string BASE_URL = "https://localhost:7089";

        // 채팅방 조회
        public static async Task<ChatRoomResponse> GetRoomAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                string url = BASE_URL + "/api/chats/room";

                HttpResponseMessage response = await client.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(json, "채팅방 조회 API 오류");
                    return null;
                }

                ApiResponse<ChatRoomResponse> result =
                    JsonConvert.DeserializeObject<ApiResponse<ChatRoomResponse>>(json);

                if (result == null || result.Success == false || result.Data == null)
                {
                    MessageBox.Show(result == null ? json : result.Message, "채팅방 조회 실패");
                    return null;
                }

                return result.Data;
            }
        }

        // 채팅 메시지 조회
        public static async Task<List<ChatMessageResponse>> GetMessagesAsync(int chatRoomId)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = BASE_URL + "/api/chats/rooms/" + chatRoomId + "/messages";

                HttpResponseMessage response = await client.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show(json, "채팅 메시지 조회 API 오류");
                    return new List<ChatMessageResponse>();
                }

                ApiResponse<List<ChatMessageResponse>> result =
                    JsonConvert.DeserializeObject<ApiResponse<List<ChatMessageResponse>>>(json);

                if (result == null || result.Success == false || result.Data == null)
                    return new List<ChatMessageResponse>();

                return result.Data;
            }
        }

        // 일반 채팅 메시지 전송
        // 관광지 공유도 TOUR_SHARE_JSON 문자열로 여기로 보냄
        public static async Task<bool> AddMessageAsync(ChatMessageRequest request)
        {
            using (HttpClient client = new HttpClient())
            {
                string url = BASE_URL + "/api/chats/messages";

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
                    MessageBox.Show(responseJson, "채팅 전송 API 오류");
                    return false;
                }

                ApiResponse<object> result =
                    JsonConvert.DeserializeObject<ApiResponse<object>>(responseJson);

                if (result == null)
                    return false;

                return result.Success;
            }
        }

        public static async Task<List<ChatMemberResponse>> GetMembersAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                string url = BASE_URL + "/api/members";

                HttpResponseMessage response = await client.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new List<ChatMemberResponse>();
                }

                ApiResponse<List<ChatMemberResponse>> result =
                    JsonConvert.DeserializeObject<ApiResponse<List<ChatMemberResponse>>>(json);

                if (result == null || result.Success == false || result.Data == null)
                    return new List<ChatMemberResponse>();

                return result.Data;
            }
        }
    }
}