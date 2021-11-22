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
        Anonymizer anonymizer = new Anonymizer();

        string text = File.ReadAllText("Letter9.txt"); //"His family is here. He himself owns his car, which is red. That car is his to do with what he pleases. That is his car.";
        string first = "Alex", last = "Bloom", middle = "Adel";

        var doc = anonymizer.ProcessRecommendation(text, first, last, middle);

        Console.WriteLine(doc.AnonymousBody);
        Console.WriteLine();
        
        //Console.WriteLine(doc.Document.ToJson());
        
        //Console.WriteLine("chooses -> " + "chooses".Pluralize(inputIsKnownToBeSingular: false));
        //IPluralize pluralizer = new Pluralizer();

        //Console.WriteLine("explained -> " + pluralizer.Pluralize("chooses"));

//        Console.WriteLine(Anonymize(doc, text, first, last));

    }
    
}


