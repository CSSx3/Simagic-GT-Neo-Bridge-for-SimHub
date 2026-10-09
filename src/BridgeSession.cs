using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace User.SimagicBridge {
    public sealed class BridgeSession {
        private readonly ISimProClient client;
        private readonly string journalPath;
        private JObject identity;
        private string presetId;
        private JObject originals;
        private readonly Dictionary<string, JObject> sent = new Dictionary<string, JObject>();
        private readonly HashSet<string> touched = new HashSet<string>();
        public bool Connected { get; private set; }
        public string WheelFirmware { get; private set; }
        public string BaseName { get; private set; }
        public long Writes { get; private set; }
        public int LastRpcMilliseconds { get; private set; }
        public bool HasRecovery { get { return File.Exists(journalPath); } }
        public BridgeSession(ISimProClient client, string journalPath) { this.client = client; this.journalPath = journalPath; }
        public async Task ConnectAsync() {
            var devices = await client.CallAsync("get_device_list", new JObject()).ConfigureAwait(false) as JArray;
            var wheels = devices == null ? new JObject[0] : devices.OfType<JObject>().Where(x =>
                (string)x["product_type"] == "wheel" && String.Equals((string)x["product_name"], "GT NEO", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (wheels.Length != 1) { Connected = false; throw new InvalidOperationException("GT Neo is missing or more than one was detected. Check SimPro."); }
            var wheel = wheels[0];
            var foundIdentity = JsonData.Object("device_uuid", wheel["device_uuid"], "product_uuid", wheel["product_uuid"]);
            if(identity != null && (originals != null || HasRecovery) &&
               (!JToken.DeepEquals(identity, foundIdentity) || WheelFirmware != (string)wheel["firmware_version"]))
                throw new InvalidOperationException("Wheel or firmware changed. Restore the saved lights first.");
            identity = foundIdentity;
            WheelFirmware = (string)wheel["firmware_version"];
            BaseName = String.Join(", ", devices.OfType<JObject>().Where(x => (string)x["product_type"] == "base").Select(x => (string)x["product_name"]));
            Connected = true;
        }
        private Task<JToken> ReadPresetAsync() { return client.CallAsync("preset_get_selected_dev_config", (JObject)identity.DeepClone()); }
        public async Task BeginAsync() {
            if (originals != null) return;
            if (HasRecovery) await RecoverAsync().ConfigureAwait(false);
            var preset = (JObject)await ReadPresetAsync().ConfigureAwait(false);
            var config = preset["config"] as JObject;
            originals = new JObject();
            foreach (string group in new[] { "led_buttons", "encoders", "rpm_lights" }) {
                var parts = config == null ? null : config[group] as JObject;
                int expected = group == "led_buttons" ? 10 : group == "encoders" ? 4 : 1;
                if (parts == null || parts.Properties().Count() != expected) { originals = null; throw new InvalidOperationException("GT Neo light groups do not match. Light control remains off."); }
                foreach (var part in parts.Properties()) {
                    int id;
                    if (!Int32.TryParse(part.Name, out id) || id < 1 || id > expected || !(part.Value is JObject)) { originals = null; throw new InvalidOperationException("Unbekannter GT-Neo-Lichtkanal."); }
                    originals[group + ":" + part.Name] = part.Value.DeepClone();
                }
            }
            presetId = (string)preset["preset_uuid"];
            if (String.IsNullOrEmpty(presetId)) { originals = null; throw new InvalidOperationException("No SimPro profile found."); }
            sent.Clear(); touched.Clear();
            // Durable original snapshot BEFORE any output command. This is never a preset-save request.
            try { SaveJournal(JsonData.Object("identity", identity, "preset_uuid", presetId, "originals", originals, "touched", new JArray())); }
            catch { originals = null; throw; }
        }
        public async Task CheckPresetAsync() {
            if (originals == null) return;
            var current = (JObject)await ReadPresetAsync().ConfigureAwait(false);
            if ((string)current["preset_uuid"] != presetId) throw new InvalidOperationException("SimPro profile changed. Select the original profile to restore its lights.");
        }
        private void SaveJournal(JObject data) {
            Directory.CreateDirectory(Path.GetDirectoryName(journalPath));
            string temporary = journalPath + ".tmp";
            File.WriteAllText(temporary, data.ToString(Formatting.Indented));
            if (File.Exists(journalPath)) File.Replace(temporary, journalPath, null);
            else File.Move(temporary, journalPath);
        }
        private async Task SetPartAsync(string key, JObject part) {
            var split = key.Split(':');
            var body = (JObject)identity.DeepClone();
            body["preset_uuid"] = presetId; body["part_type"] = split[0]; body["part_id"] = Int32.Parse(split[1]); body["config"] = part.DeepClone();
            await WriteAndVerifyAsync(body, key, part).ConfigureAwait(false);
        }
        private async Task WriteAndVerifyAsync(JObject body, string key, JToken expected) {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await client.CallAsync("preset_set_dev_config", body).ConfigureAwait(false);
            LastRpcMilliseconds = (int)watch.ElapsedMilliseconds; Writes++;
            // A successful RPC only queues the change. Sending the next part too soon
            // can overwrite it with SimPro's old preset cache. Wait for each commit.
            string[] split = key.Split(':');
            for(int attempt = 0; attempt < 30; attempt++) {
                await Task.Delay(100).ConfigureAwait(false);
                var current = (JObject)await ReadPresetAsync().ConfigureAwait(false);
                if((string)current["preset_uuid"] != (string)body["preset_uuid"])
                    throw new InvalidOperationException("SimPro profile changed. Light control stopped.");
                if(JToken.DeepEquals(expected, current["config"][split[0]][split[1]])) return;
            }
            throw new InvalidOperationException("SimPro did not confirm light channel " + key + ". Light control stopped.");
        }
        private async Task SendChangedAsync(string key, JObject patch) {
            JObject previous;
            if (sent.TryGetValue(key, out previous) && JToken.DeepEquals(previous, patch)) return;
            if (touched.Add(key)) SaveJournal(JsonData.Object("identity", identity, "preset_uuid", presetId,
                "originals", originals, "touched", new JArray(touched.OrderBy(x => x))));
            await SetPartAsync(key, patch).ConfigureAwait(false);
            sent[key] = (JObject)patch.DeepClone();
        }
        public async Task ApplyAsync(LightFrame frame, Func<bool> stillEnabled) {
            if (originals == null) throw new InvalidOperationException("Bridge session is not active.");
            var patches = new List<KeyValuePair<string,JObject>>();
            for (int i = 0; i < 10; i++) { string key = "led_buttons:" + (i + 1); patches.Add(new KeyValuePair<string,JObject>(key, PresetPatches.Button((JObject)originals[key], frame.Buttons[i]))); }
            for (int i = 0; i < 4; i++) { string key = "encoders:" + (i + 1); patches.Add(new KeyValuePair<string,JObject>(key, PresetPatches.Encoder((JObject)originals[key], frame.Encoders[i]))); }
            patches.Add(new KeyValuePair<string,JObject>("rpm_lights:1", PresetPatches.Rpm((JObject)originals["rpm_lights:1"], frame.Rpm)));
            foreach (var patch in patches) { if (!stillEnabled()) break; await SendChangedAsync(patch.Key, patch.Value).ConfigureAwait(false); }
        }
        public async Task TestButtonAsync() {
            await BeginAsync().ConfigureAwait(false);
            await SendChangedAsync("led_buttons:1", PresetPatches.Button((JObject)originals["led_buttons:1"], Color.Cyan)).ConfigureAwait(false);
            await Task.Delay(3000).ConfigureAwait(false);
            await RestoreAsync().ConfigureAwait(false);
        }
        public async Task RestoreAsync() {
            if (!HasRecovery) { originals = null; sent.Clear(); touched.Clear(); return; }
            await RecoverAsync().ConfigureAwait(false);
            originals = null; sent.Clear(); touched.Clear();
        }
        public async Task RecoverAsync() {
            if (!HasRecovery) return;
            var journal = JObject.Parse(File.ReadAllText(journalPath));
            if (!JToken.DeepEquals(journal["identity"], identity)) throw new InvalidOperationException("The backup belongs to a different wheel. Restore remains pending.");
            var current = (JObject)await ReadPresetAsync().ConfigureAwait(false);
            if ((string)current["preset_uuid"] != (string)journal["preset_uuid"]) throw new InvalidOperationException("Select the backed-up SimPro profile, then restore the lights.");
            var backup = (JObject)journal["originals"];
            var keys = ((JArray)journal["touched"]).Values<string>().ToArray();
            foreach (string key in keys) {
                string[] split = key.Split(':');
                if (split.Length != 2 || !new[] {"led_buttons","encoders","rpm_lights"}.Contains(split[0])) throw new InvalidOperationException("The backup contains an invalid light channel.");
                var body = (JObject)identity.DeepClone(); body["preset_uuid"] = journal["preset_uuid"].DeepClone();
                body["part_type"] = split[0]; body["part_id"] = Int32.Parse(split[1]); body["config"] = backup[key].DeepClone();
                await WriteAndVerifyAsync(body, key, backup[key]).ConfigureAwait(false);
            }
            // SimPro's configuration cache updates asynchronously after a successful RPC.
            for (int attempt = 0; attempt < 12; attempt++) {
                await Task.Delay(150).ConfigureAwait(false);
                current = (JObject)await ReadPresetAsync().ConfigureAwait(false);
                bool equal = (string)current["preset_uuid"] == (string)journal["preset_uuid"];
                foreach (string key in keys) { string[] split = key.Split(':'); equal &= JToken.DeepEquals(backup[key], current["config"][split[0]][split[1]]); }
                if (equal) { File.Delete(journalPath); return; }
            }
            throw new InvalidOperationException("SimPro did not confirm the restore. The backup has been kept.");
        }
        public void MarkDisconnected() { Connected = false; }
    }
}
