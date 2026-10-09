using System.Linq;
using Newtonsoft.Json.Linq;

namespace User.SimagicBridge {
    public static class BridgeDefaultProfiles {
        public static JToken Prepare(JToken settings) {
            if (!(settings is JObject)) return settings;
            var result = (JObject)settings.DeepClone();
            var module = result["ledModuleSettings"] as JObject;
            if(module != null) module["ForceRefreshWithNoChanges"] = true;
            var leds = result["leds"] as JObject;
            var profiles = leds == null ? null : leds["Profiles"] as JArray;
            if(profiles == null) return result;
            // Seed only the untouched empty default. Keep imported profiles and car overrides.
            var profile = profiles.OfType<JObject>().FirstOrDefault(p =>
                (string)p["Name"] == "Default Profile" &&
                ((p["LedContainers"] as JArray) == null || !((JArray)p["LedContainers"]).Any()) &&
                ((p["CarChoices"] as JArray) == null || !((JArray)p["CarChoices"]).Any()) &&
                (p["CarChoice"] == null || p["CarChoice"].Type == JTokenType.Null));
            if(profile != null && !profiles.OfType<JObject>().Any(p => (p["LedContainers"] as JArray) != null && ((JArray)p["LedContainers"]).Any())) {
                profile["LedContainers"] = CreateRpmContainers();
                SimHub.Logging.Current.Info("[SimagicBridge] Empty default RPM profile filled with 15 telemetry LEDs.");
            }
            return result;
        }
        public static JArray CreateRpmContainers() {
            var rpm = JsonData.Object("ContainerType", "RPM", "Description", "RPM from vehicle telemetry",
                "RpmMode", 1, "OverrideMaxRPMWithRedlineValue", true, "PercentMin", 0.0, "PercentMax", 100.0,
                "RPMMin", 0.0, "RPMMax", 8000.0, "StartColor", "Lime", "EndColor", "Red",
                "LedCount", 15, "StartPosition", 1, "UseLedDimming", false, "RelativeToRedline", false,
                "BlinkOnLastGear", false, "BlinkDelay", 1000);
            return new JArray(JsonData.Object("ContainerType", "Groups.GameRunningGroup", "LedContainers", new JArray(rpm)));
        }
    }
}
