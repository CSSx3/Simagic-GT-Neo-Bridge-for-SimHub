using System;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace User.SimagicBridge {
    public static class PresetPatches {
        // Clone the complete part, so button codes, absolute modes and calibration survive.
        public static JObject Button(JObject original, Color color) {
            var part = (JObject)original.DeepClone();
            var led = part["led"] as JObject;
            if (led == null) throw new InvalidOperationException("GT Neo button schema changed.");
            led["color"] = LightFrame.Hex(color);
            SetStatic(led, new[] { color });
            return part;
        }
        public static JObject Encoder(JObject original, Color color) {
            var part = (JObject)original.DeepClone();
            var group = part["light_group"] as JObject;
            if (group == null) throw new InvalidOperationException("GT Neo encoder schema changed.");
            group["is_on"] = true; group["is_cycle"] = false;
            SetStatic(group, new[] { color });
            return part;
        }
        private static void SetStatic(JObject container, Color[] colors) {
            var items = container["items"] as JArray;
            if (items == null) throw new InvalidOperationException("GT Neo light events missing.");
            var selected = items.OfType<JObject>().FirstOrDefault(x => (string)x["key"] == "STATIC_EFFECT");
            if (selected == null) throw new InvalidOperationException("GT Neo static effect missing.");
            // SimPro's UI renumbers event rows before sending them. Stored presets can
            // contain duplicate indices; without this, the encoder update is ignored.
            int index = 1;
            foreach (var item in items.OfType<JObject>()) {
                item["index"] = index++;
                item["enabled"] = Object.ReferenceEquals(item, selected);
            }
            var light = selected["light"] as JObject;
            var configs = light == null ? null : light["configs"] as JObject;
            var mono = configs == null ? null : configs["mono"] as JObject;
            if (mono == null) throw new InvalidOperationException("GT Neo mono effect missing.");
            light["active_effect"] = "mono";
            mono["foregroud_color"] = new JArray(colors.Select(LightFrame.Hex));
            mono["backgroud_color"] = "#000000";
            mono["brightness"] = 100; mono["interval"] = 0; mono["speed_level"] = "zero";
        }
        public static JObject Rpm(JObject original, Color[] colors) {
            if (colors.Length != 15) throw new ArgumentException("15 RPM LEDs required.");
            var part = (JObject)original.DeepClone();
            var lights = part["lights"] as JArray;
            var first = lights == null ? null : lights.FirstOrDefault() as JObject;
            if (first == null) throw new InvalidOperationException("GT Neo RPM schema changed.");
            var entry = (JObject)first.DeepClone();
            entry["mode"] = "X"; entry["is_on"] = true;
            entry["color"] = new JArray(colors.Select(LightFrame.Hex));
            // SimPro treats 0 as unused. 200 = 1% RPM; game telemetry must be active in SimPro.
            entry["value"] = new JArray(colors.Select(c => c.R == 0 && c.G == 0 && c.B == 0 ? 0 : 200));
            entry["rpm_redlines"] = new JArray();
            var redline = entry["redline"] as JObject;
            if (redline != null) redline["enabled"] = false;
            part["lights"] = new JArray(entry);
            part["rpm_mode"] = 0; part["selected_mode"] = 0; part["max_rpm_source"] = 0;
            part["is_on"] = true; part["brightness"] = 100; part["sync_to_wheel"] = true;
            return part;
        }
    }
}
