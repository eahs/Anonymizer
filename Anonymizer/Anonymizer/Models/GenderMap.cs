using System.Text.Json.Serialization; 
using System.Collections.Generic; 
namespace Anonymizer{ 

    public class GenderMap
    {
        [JsonPropertyName("f")]
        public List<AlternateWord> FemaleAlternates { get; set; }

        [JsonPropertyName("m")]
        public List<AlternateWord> MaleAlternates { get; set; }

        [JsonPropertyName("n")]
        public List<AlternateWord> NeutralAlternates { get; set; }

        /// <summary>
        /// Word can be null - Be careful
        /// </summary>
        public string? Word
        {
            get
            {
                var neut = NeutralAlternates?.Select(x => x.Word).FirstOrDefault();
                if (neut != null)
                {
                    return neut;
                }

                // It's not right to default to male alternates, but it is currently more common
                // than the opposite (male and female actors)
                return MaleAlternates?.Select(x => x.Word).FirstOrDefault();
            }
        }
    }

}