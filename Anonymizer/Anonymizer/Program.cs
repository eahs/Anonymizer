using Catalyst;
using Catalyst.Models;
using Mosaik.Core;
using System.Text.RegularExpressions;
using Humanizer;
using P = Catalyst.PatternUnitPrototype;
using Pluralize.NET;

namespace Anonymizer;

public class Program
{
    static IPluralize pluralizer = new Pluralizer();

    static async Task Main(string[] args)
    {
        Catalyst.Models.English.Register(); //You need to pre-register each language (and install the respective NuGet Packages)

        Storage.Current = new DiskStorage("catalyst-models");
        var nlp = await Pipeline.ForAsync(Language.English);
        var possessivePattern = new PatternSpotter(Language.English, 0, tag: "possessive-pattern", captureTag: "PossessiveHer");
        possessivePattern.NewPattern(
            "Her+Noun",
            mp => mp.Add(
                new PatternUnit(P.Single().WithTokens(new string[] { "her", "his", "Her", "His" }).WithPOS(PartOfSpeech.PRON,PartOfSpeech.DET)),
                new PatternUnit(P.MultipleOptional().WithPOS(PartOfSpeech.ADV, PartOfSpeech.ADJ)),
                new PatternUnit(P.Single().WithPOS(PartOfSpeech.NOUN, PartOfSpeech.PROPN))
        ));
        

        var sheIsPattern = new PatternSpotter(Language.English, 0, tag: "sp2-patter", captureTag: "SheIs");
        sheIsPattern.NewPattern(
            "She+Verb",
            mp => mp.Add(
                new PatternUnit(P.Single().WithTokens(new string[] { "he", "she", "He", "She" }).WithPOS(PartOfSpeech.PRON)),
                new PatternUnit(P.MultipleOptional().WithPOS(PartOfSpeech.ADV, PartOfSpeech.ADJ)),
                new PatternUnit(P.Single().WithPOS(PartOfSpeech.VERB))
        ));

        nlp.Add(possessivePattern);
        nlp.Add(sheIsPattern);

        string text = File.ReadAllText("Letter4.txt"); //"His family is here. He himself owns his car, which is red. That car is his to do with what he pleases. That is his car.";
        string first = "Sara", last = "Stewart";

        text = Sanitize(text, first, last);

        
        var doc = new Document(text, Language.English);
        nlp.ProcessSingle(doc);
        
        Console.WriteLine(doc.ToJson());
        
        //Console.WriteLine("chooses -> " + "chooses".Pluralize(inputIsKnownToBeSingular: false));
        IPluralize pluralizer = new Pluralizer();

        Console.WriteLine("explained -> " + pluralizer.Pluralize("chooses"));

        Console.WriteLine(Anonymize(doc, text, first, last));

    }

    static string Sanitize(string text, string first, string last)
    {
        text = text.Replace('’', '\'');
        text = Regex.Replace(text, "[Mm](r|s|iss)\\.?\\s+" + last, "Mx. " + last);
        return text;
    }


    static string Anonymize(Document doc, string text, string first, string last)
    {
        List<string> output = new List<string>();
        string accum = "";
        var tokens = doc.SelectMany(x => x.Tokens).ToList();
        var entities = doc.SelectMany(span => span.GetEntities()).ToList();

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

                    var verb = entities[i].Children.First(x => x.POS == PartOfSpeech.VERB);
                    RewriteToken(tokens, verb, Pluralize(verb));
                    break;
            }
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (index < tokens.Count - 1 && i == tokens[index].Begin)
            {
                ProcessToken(tokens[index], first, last);

                if (index < tokens.Count - 2)
                {
                    int diff = tokens[index + 1].Begin - tokens[index].End;
                    if (diff == 0)  
                        accum = " ";
                }

                output.Add(accum);
                output.Add(tokens[index].Replacement ?? tokens[index].Value);
                accum = "";
                i += tokens[index].Length - 1;
                index++;
            }
            else
            {
                accum += text[i];
            }
        }
        output.Add(accum);

        return String.Join("", output);
    }

    static string Pluralize(IToken input)
    {
        Dictionary<string, string> verbs = new Dictionary<string, string>
        {
            // singular , plural form
            { "is", "are" },
            { "has", "have" }
        };

        if (verbs.ContainsKey(input.Value.ToLower()))
        {
            return MatchCase(input.Value, verbs[input.Value.ToLower()]);
        }

        return pluralizer.Singularize(input.Value);
    }

    static void RewriteToken(List<IToken> tokens, IToken find, string newValue)
    {
        var target = tokens.FirstOrDefault(x => x.Begin == find.Begin);

        if (target != null)
            target.Replacement = MatchCase(target.Value, newValue);
    }

    static void ProcessToken(IToken token, string first, string last)
    {
        if (token.Value == first + "'s") token.Replacement = "Student's";
        if (token.Value == last + "'s") token.Replacement = "Candidate's";

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

                if (tokval == first.ToLower())
                    token.Replacement = "Student";

                else if (tokval == last.ToLower())
                    token.Replacement = "Candidate";

                break;

            case PartOfSpeech.X: 
                
                // Unknown POS if we land here

                if (tokval == first.ToLower())
                    token.Replacement = "Student";

                else if (tokval == last.ToLower())
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
    }

    static string MatchCase(string original, string replacement)
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


