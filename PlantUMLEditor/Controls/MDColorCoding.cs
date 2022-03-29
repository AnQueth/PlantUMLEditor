using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    internal class MDColorCoding : IColorCodingProvider
    {

        private static readonly Regex _hs = new Regex("^(?<hs>[#\\*]+)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _bolds = new Regex("(?<hs>[\\*]{2}\\w+[\\*]{2})", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _italics = new Regex("(?<hs>(?<!\\*)[\\*]{1}\\w+(?<!\\*)[\\*]{1}|[_]{1}\\w+[_]{1})", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _lists = new Regex("^ *(?<hs>[#\\*\\-\\d])", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _links = new Regex("(?<hs>\\[.+\\))", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _code = new Regex("`{3}[.\\w\\W]*?`{3}", RegexOptions.Compiled | RegexOptions.Multiline);

        //[![process.seq](process.seq.png)](process.seq.png)

        public List<FormatResult> FormatText(string text)
        {
            List<FormatResult> results = new List<FormatResult>();






            MatchCollection? bolds = _bolds.Matches(text);

            foreach (Match m in bolds)
            {


                results.Add(new FormatResult(new SolidColorBrush(Colors.Black), m.Groups["hs"].Index, m.Groups["hs"].Length, FontWeights.Bold, m.Groups["hs"].Value));
            }

            MatchCollection? lists = _lists.Matches(text);

            foreach (Match m in lists)
            {


                results.Add(new FormatResult(new SolidColorBrush(Colors.Green), m.Groups["hs"].Index, m.Groups["hs"].Length, FontWeights.Normal, m.Groups["hs"].Value));
            }

            MatchCollection? italics = _italics.Matches(text);

            foreach (Match m in italics)
            {


                results.Add(new FormatResult(new SolidColorBrush(Colors.Black), m.Groups["hs"].Index, m.Groups["hs"].Length, FontWeights.Normal, m.Groups["hs"].Value, true));
            }

            MatchCollection? links = _links.Matches(text);

            foreach (Match m in links)
            {


                results.Add(new FormatResult(new SolidColorBrush(Colors.DarkGreen), m.Groups["hs"].Index, m.Groups["hs"].Length, FontWeights.Normal, m.Groups["hs"].Value, true));
            }


            MatchCollection? code = _code.Matches(text);

            foreach (Match m in code)
            {


                results.Add(new FormatResult(new SolidColorBrush(Colors.DarkOrange), m.Index, m.Length, FontWeights.Normal, m.Value));
            }

            MatchCollection? ms = _hs.Matches(text);

            foreach (Match m in ms)
            {


                results.Add(new FormatResult(new SolidColorBrush(Colors.Blue), m.Groups["hs"].Index, m.Groups["hs"].Length, FontWeights.Normal, m.Groups["hs"].Value));
            }
            return results;
        }
    }
}
