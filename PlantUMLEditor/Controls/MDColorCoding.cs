using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    internal class MDColorCoding : IColorCodingProvider
    {
        private static readonly Regex _hs = new Regex("^[#\\*]+", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _bolds = new Regex("[\\*]{2}\\w+[\\*]{2}", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _italics = new Regex("(?<!\\*)[\\*]{1}\\w+(?<!\\*)[\\*]{1}|[_]{1}\\w+[_]{1}", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _lists = new Regex("^ *([#\\*\\-]|[\\d]+)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _links = new Regex("\\[.+\\)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex _code = new Regex("`{3}[.\\w\\W]*?`{3}", RegexOptions.Compiled | RegexOptions.Multiline);

        private readonly Brush _headingBrush;
        private readonly Brush _boldBrush;
        private readonly Brush _italicBrush;
        private readonly Brush _listBrush;
        private readonly Brush _linkBrush;
        private readonly Brush _codeBrush;

        public MDColorCoding()
        {
            Brush CreateFrozen(Color c)
            {
                var b = new SolidColorBrush(c);
                if (b.CanFreeze)
                {
                    b.Freeze();
                }
                return b;
            }

            _headingBrush = CreateFrozen(MDColorCodingConfig.HeadingColor);
            _boldBrush = CreateFrozen(MDColorCodingConfig.BoldColor);
            _italicBrush = CreateFrozen(MDColorCodingConfig.ItalicColor);
            _listBrush = CreateFrozen(MDColorCodingConfig.ListColor);
            _linkBrush = CreateFrozen(MDColorCodingConfig.LinkColor);
            _codeBrush = CreateFrozen(MDColorCodingConfig.CodeColor);
        }

        public List<FormatResult> FormatText(string text)
        {
            List<FormatResult> results = new List<FormatResult>();

            for (Match m = _bolds.Match(text); m.Success; m = m.NextMatch())
            {
                results.Add(new FormatResult(_boldBrush, m.Index, m.Length, FontWeights.Bold, m.Value));
            }

            for (Match m = _lists.Match(text); m.Success; m = m.NextMatch())
            {
                results.Add(new FormatResult(_listBrush, m.Index, m.Length, FontWeights.Normal, m.Value));
            }

            for (Match m = _italics.Match(text); m.Success; m = m.NextMatch())
            {
                results.Add(new FormatResult(_italicBrush, m.Index, m.Length, FontWeights.Normal, m.Value, true));
            }

            for (Match m = _links.Match(text); m.Success; m = m.NextMatch())
            {
                results.Add(new FormatResult(_linkBrush, m.Index, m.Length, FontWeights.Normal, m.Value, true));
            }

            for (Match m = _code.Match(text); m.Success; m = m.NextMatch())
            {
                results.Add(new FormatResult(_codeBrush, m.Index, m.Length, FontWeights.Normal, m.Value));
            }

            for (Match m = _hs.Match(text); m.Success; m = m.NextMatch())
            {
                results.Add(new FormatResult(_headingBrush, m.Index, m.Length, FontWeights.Normal, m.Value));
            }

            return results;
        }
    }
}
