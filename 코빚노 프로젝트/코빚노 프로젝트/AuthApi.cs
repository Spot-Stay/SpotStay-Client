using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace 코빚노_프로젝트
{
    public class AuthApi
    {
        private static readonly HttpClient client = new HttpClient();

        static AuthApi()
        {
            client.BaseAddress = new Uri("https://localhost:7089/");
        }

        public static async Task<bool> LoginAsync(string id, string password)
        {
            var data = new LoginRequest
            {
                Userid = id,
                password = password
            };

            string json = JsonConvert.SerializeObject(data);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("api/Auth/login", content);

            return response.IsSuccessStatusCode;
        }
    }

    public class LoginRequest
    {
        public string Userid { get; set; }
        public string password { get; set; }
    }
}