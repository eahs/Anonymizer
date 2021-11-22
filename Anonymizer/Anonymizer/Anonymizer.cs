using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Anonymizer.Models;
using Catalyst;
using Catalyst.Models;
using Mosaik.Core;
using Pluralize.NET;
using P = Catalyst.PatternUnitPrototype;
using System.Text.Json;

namespace Anonymizer
{
    public class Anonymizer
    {
        private readonly IPluralize _pluralizer;
        private Pipeline? _nlp;
        private Dictionary<string, string> _genderMap;
        Dictionary<string, string> _auxVerbs = new Dictionary<string, string>
        {
            // singular , plural form
            { "is", "are" },
            { "has", "have" },
            { "was", "were" },
            { "does", "do"}
        };

        public Anonymizer()
        {
            _pluralizer = new Pluralizer();
            _genderMap = new Dictionary<string,string>();
                
            // Initialize Catalyst NLP
            InitializeNLP().GetAwaiter().GetResult();

            var wordData = File.ReadAllText(Path.Combine(@".\Data", "gendered_words.json"));
            var words = JsonSerializer.Deserialize<List<GenderWord>>(wordData).Where(x => x.Gender != "n").ToList();

            foreach (var word in words)
            {
                string key = word.Word.Replace("_", " ");
                string value = word.GenderMap?.Word ?? word.Word;
                value = value.Replace("_", " ");

                if (word.GenderMap != null && !_genderMap.ContainsKey(key))
                    _genderMap.Add(key, value);
            }
        }

        public RecommendationDocument ProcessRecommendation(string recommendation, string firstName, string lastName, string middleName = "")
        {
            RecommendationDocument doc = new RecommendationDocument
            {
                Body = recommendation,
                AnonymousBody = "",
                FirstName = firstName,
                MiddleName = middleName,
                LastName = lastName
            };

            Sanitize(doc);

            doc.Document = new Document(doc.Body, Language.English);
            _nlp.ProcessSingle(doc.Document);

            Transform(doc);

            return doc;
        }

        private void Transform(RecommendationDocument doc)
        {
            List<string> output = new List<string>();
            string accum = "";
            var tokens = doc.Document.SelectMany(x => x.Tokens).ToList();
            var entities = doc.Document.SelectMany(span => span.GetEntities()).ToList();

            int index = 0;

            for (int i = 0; i < entities.Count; i++)
            {
                switch (entities[i].EntityType.Type)
                {
                    case "PossessiveHer":
                        var find = entities[i].Children.First();
                        //var target = tokens.FirstOrDefault(x => x.Begin == token.Begin);
                        //target.Replacement = MatchCase(target.Value, "their");
                        RewriteToken(tokens, find, "their");
                        break;

                    case "SheIs":
                        find = entities[i].Children.First();
                        RewriteToken(tokens, find, "they");

                        var verb = entities[i].Children.First(x => x.POS == PartOfSpeech.VERB || x.POS == PartOfSpeech.AUX);
                        RewriteToken(tokens, verb, Pluralize(verb));
                        break;

                    case "AuxShe":
                        verb = entities[i].Children.First();
                        
                        if (_auxVerbs.ContainsKey(verb.Value.ToLower()))
                            RewriteToken(tokens, verb, _auxVerbs[verb.Value.ToLower()]);

                        break;
                }
            }

            for (int i = 0; i < doc.Body.Length; i++)
            {
                if (index < tokens.Count - 1 && i == tokens[index].Begin)
                {
                    ProcessToken(doc, tokens[index]);

                    if (index > 0)
                    {
                        string span = doc.Body.Substring(tokens[index].Begin, tokens[index].End - tokens[index].Begin + 1);
                        int quoteCheck = span.IndexOf("'"); // doc.Body.Substring(tokens[index].Begin, 1);

                        if (quoteCheck != -1)
                        {
                            if (tokens[index - 1].Replacement is not null
                                && tokens[index - 1].Replacement != "Student"
                                && tokens[index - 1].Replacement != "Candidate")
                                accum = " ";

                            //if (tokens[index - 1].Value == "I")
                            //    accum = " ";
                        }
                    }

                    output.Add(accum);
                    output.Add(tokens[index].Replacement ?? tokens[index].Value);
                    accum = "";

                    i += tokens[index].Length - 1;
                    index++;
                }
                else
                {
                    accum += doc.Body[i];
                }
            }
            output.Add(accum);

            doc.AnonymousBody = String.Join("", output);

        }

        private async Task InitializeNLP()
        {
            Catalyst.Models.English.Register(); //You need to pre-register each language (and install the respective NuGet Packages)

            Storage.Current = new DiskStorage("catalyst-models");
            _nlp = await Pipeline.ForAsync(Language.English);
            var possessivePattern = new PatternSpotter(Language.English, 0, tag: "possessive-pattern", captureTag: "PossessiveHer");
            possessivePattern.NewPattern(
                "Her+Noun",
                mp => mp.Add(
                    new PatternUnit(PatternUnitPrototype.Single().WithTokens(new string[] { "her", "his", "Her", "His" }).WithPOS(PartOfSpeech.PRON, PartOfSpeech.DET)),
                    new PatternUnit(PatternUnitPrototype.MultipleOptional().WithPOS(PartOfSpeech.ADV, PartOfSpeech.ADJ)),
                    new PatternUnit(PatternUnitPrototype.Single().WithPOS(PartOfSpeech.NOUN, PartOfSpeech.PROPN))
                ));


            var sheIsPattern = new PatternSpotter(Language.English, 0, tag: "sp2-patter", captureTag: "SheIs");
            sheIsPattern.NewPattern(
                "She+Verb",
                mp => mp.Add(
                    new PatternUnit(PatternUnitPrototype.Single().WithTokens(new string[] { "he", "she", "He", "She" }).WithPOS(PartOfSpeech.PRON)),
                    new PatternUnit(PatternUnitPrototype.MultipleOptional().WithPOS(PartOfSpeech.ADV, PartOfSpeech.ADJ)),
                    new PatternUnit(PatternUnitPrototype.Single().WithPOS(PartOfSpeech.VERB, PartOfSpeech.AUX))
                ));

            var auxShePattern = new PatternSpotter(Language.English, 0, tag: "sp3-patter", captureTag: "AuxShe");
            auxShePattern.NewPattern(
                "Aux+She",
                mp => mp.Add(
                    new PatternUnit(PatternUnitPrototype.Single().WithTokens(new string[] { "does", "Does", "is", "Is", "was", "Was", "has", "Has" })),
                    new PatternUnit(PatternUnitPrototype.MultipleOptional().WithPOS(PartOfSpeech.ADV, PartOfSpeech.ADJ)),
                    new PatternUnit(PatternUnitPrototype.Single().WithTokens(new string[] { "he", "she", "He", "She" }).WithPOS(PartOfSpeech.PRON))
                ));

            _nlp.Add(possessivePattern);
            _nlp.Add(sheIsPattern);
            _nlp.Add(auxShePattern);
        }

        private void Sanitize(RecommendationDocument doc)
        {
            doc.Body = doc.Body.Replace('’', '\'');
            doc.Body = Regex.Replace(doc.Body, "[Mm](r|s|iss)\\.?\\s+" + doc.LastName, "Mx. " + doc.LastName);
            doc.Body = Regex.Replace(doc.Body, "[Mm](r|s|iss)\\.?\\s+" + doc.FirstName, "Mx. " + doc.FirstName);

        }

        private string Pluralize(IToken input)
        {
            if (_auxVerbs.ContainsKey(input.Value.ToLower()))
            {
                return MatchCase(input.Value, _auxVerbs[input.Value.ToLower()]);
            }
            else if (input.Value.StartsWith(input.Lemma) && _pluralizer.IsPlural(input.Value))
            {
                var singular = _pluralizer.Singularize(input.Value);
                return singular;
            }
            else
            {
                return input.Value;
            }


            /*
            else if (input.Value.StartsWith(input.Lemma) && pluralizer.IsPlural(input.Value))
               return pluralizer.Singularize(input.Value);
            else
                return pluralizer.Pluralize(input.Value);
            */
        }

        private void RewriteToken(List<IToken> tokens, IToken find, string newValue)
        {
            var target = tokens.FirstOrDefault(x => x.Begin == find.Begin);

            if (target != null)
                target.Replacement = MatchCase(target.Value, newValue);
        }

        private void ProcessToken(RecommendationDocument doc, IToken token)
        {
            if (token.Value == doc.FirstName + "'s") token.Replacement = "Student's";
            if (token.Value == doc.LastName + "'s") token.Replacement = "Candidate's";

            // Only handle tokens that haven't been processed already
            if (token.Replacement != null)
                return;

            string tokval = token.Value.ToLower();

            switch (token.POS)
            {
                case PartOfSpeech.DET:
                    if (tokval == "his" || tokval == "her")
                        token.Replacement = MatchCase(token.Value, "their");

                    break;

                case PartOfSpeech.PROPN:

                    if (tokval == doc.FirstName.ToLower())
                        token.Replacement = "Student";

                    else if (tokval == doc.LastName.ToLower())
                        token.Replacement = "Candidate";

                    else if (tokval == doc.MiddleName.ToLower())
                        token.Replacement = "";

                    break;

                case PartOfSpeech.X:

                    // Unknown POS if we land here

                    if (tokval == doc.FirstName.ToLower())
                        token.Replacement = "Student";

                    else if (tokval == doc.LastName.ToLower())
                        token.Replacement = "Candidate";

                    break;


                case PartOfSpeech.PRON:

                    if (tokval == "he" || tokval == "she")
                        token.Replacement = MatchCase(token.Value, "they");

                    if (tokval == "him" || tokval == "her")
                        token.Replacement = MatchCase(token.Value, "them");

                    if (tokval == "himself" || tokval == "herself")
                        token.Replacement = MatchCase(token.Value, "themself");

                    break;
            }

            if (_genderMap.ContainsKey(tokval) && token.POS != PartOfSpeech.PROPN && token.Replacement == null)
            {
                token.Replacement = MatchCase(token.Value, _genderMap[tokval]);
            }
        }

        private string MatchCase(string original, string replacement)
        {
            if (!string.IsNullOrEmpty(original) && char.IsUpper(original[0]))
            {
                return replacement.First().ToString().ToUpper() + replacement.Substring(1);
            }
            else
            {
                return replacement.ToLower();
            }
        }
    }
}
