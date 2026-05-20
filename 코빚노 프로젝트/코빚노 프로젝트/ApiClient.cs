using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace 코빚노_프로젝트
{
    internal class ApiClient
    {
        private static readonly HttpClient http = new HttpClient();

        static ApiClient()
        {
            string baseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"];
            http.BaseAddress = new Uri(baseUrl);
        }

        public async Task<string> PostAsync(string url, object data)
        {
            string json = JsonConvert.SerializeObject(data);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage res = await http.PostAsync(url, content);
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                throw new Exception(body);

            return body;
        }
    }
}
