using Newtonsoft.Json.Linq;
namespace User.SimagicBridge {
    internal static class JsonData {
        internal static JObject Object(params object[] pairs) {
            var result = new JObject();
            for(int i=0;i<pairs.Length;i+=2) result[(string)pairs[i]] = pairs[i+1] is JToken ? ((JToken)pairs[i+1]).DeepClone() : JToken.FromObject(pairs[i+1]);
            return result;
        }
    }
}
