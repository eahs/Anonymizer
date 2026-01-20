using System.Text.Json.Serialization;
namespace TextUtils
{

    public class AlternateWord
    {
        [JsonPropertyName("parts_of_speech")]
        public string PartsOfSpeech { get; set; }

        [JsonPropertyName("word")]
        public string Word { get; set; }
    }

}