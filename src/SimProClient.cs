using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace User.SimagicBridge {
    public interface ISimProClient : IDisposable {
        Task<JToken> CallAsync(string method, JObject body);
    }
    public sealed class SimProClient : ISimProClient {
        private readonly HttpClient http;
        public SimProClient() {
            http = new HttpClient(new HttpClientHandler { UseProxy = false });
            http.Timeout = TimeSpan.FromSeconds(3);
            http.BaseAddress = new Uri("http://127.0.0.1:4010/simpro/api/v3/");
        }
        public async Task<JToken> CallAsync(string method, JObject body) {
            if (!new[] { "get_device_list", "preset_get_selected_dev_config", "preset_set_dev_config" }.Contains(method))
                throw new InvalidOperationException("Unsupported SimPro operation.");
            using (var content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json"))
            using (var response = await http.PostAsync(method, content).ConfigureAwait(false)) {
                response.EnsureSuccessStatusCode();
                var envelope = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                if ((int?)envelope["status"] != 200)
                    throw new InvalidOperationException("SimPro: " + (string)envelope["message"]);
                return envelope["result"];
            }
        }
        public void Dispose() { http.Dispose(); }
    }
}
