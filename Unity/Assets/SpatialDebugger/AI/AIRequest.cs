using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SpatialDebugger.AI
{
    /// <summary>Body for <c>POST /ask</c>.</summary>
    public class AIAskRequest
    {
        public string Question;
        public string Context;

        /// <summary>Force a specific offline scenario. Null means let the backend choose.</summary>
        public string Scenario;

        public string ToJson()
        {
            var json = new JObject { ["question"] = Question ?? string.Empty };
            if (!string.IsNullOrEmpty(Context)) json["context"] = Context;
            if (!string.IsNullOrEmpty(Scenario)) json["scenario"] = Scenario;
            return json.ToString(Formatting.None);
        }
    }

    /// <summary>Body for <c>POST /analyze</c>.</summary>
    public class AIAnalyzeRequest
    {
        public string Context;
        public string Question;
        public string Scenario;
        public string TargetLabel;

        /// <summary>
        /// Reserved for the future passthrough-camera pipeline. Nothing sends
        /// this today; the backend accepts and ignores it.
        /// </summary>
        public string ImageBase64;

        public string ToJson()
        {
            var json = new JObject();
            if (!string.IsNullOrEmpty(Context)) json["context"] = Context;
            if (!string.IsNullOrEmpty(Question)) json["question"] = Question;
            if (!string.IsNullOrEmpty(Scenario)) json["scenario"] = Scenario;
            if (!string.IsNullOrEmpty(TargetLabel)) json["target_label"] = TargetLabel;
            if (!string.IsNullOrEmpty(ImageBase64)) json["image_base64"] = ImageBase64;
            return json.ToString(Formatting.None);
        }
    }

    internal static class RequestEncoding
    {
        public static byte[] Utf8(string body) => Encoding.UTF8.GetBytes(body ?? "{}");
    }
}
