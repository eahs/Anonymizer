using System.Text.Json.Serialization;
namespace TextUtils
{

    public class GenderWord
    {
        [JsonPropertyName("word")]
        public string Word { get; set; }

        [JsonPropertyName("wordnet_senseno")]
        public string WordnetSenseno { get; set; }

        [JsonPropertyName("gender")]
        public string Gender { get; set; }

        [JsonPropertyName("gender_map")]
        public GenderMap GenderMap { get; set; }
    }

}