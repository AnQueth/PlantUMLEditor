using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    internal class TextDecorarations : Adorner
    {
        private Dictionary<Regex, Color> _colorCodes = new Dictionary<Regex, Color>()
        {
            {new Regex("^(\\{|\\})") , Colors.Purple},

                {new Regex("^(class|\\{\\w+\\}|interface|package|alt|opt|loop|try|group|catch|break|par|participant|actor|database|component)$"), Colors.Blue}
        };

        private Dictionary<Regex, (Color, bool)> _mcolorCodes = new Dictionary<Regex, (Color, bool)>()
        {
            {new Regex("(participant|actor|database|component|class|interface)\\s+\\w+\\s+$"), (Colors.Green, false ) },
               {new Regex("(\\:.+)$"), (Colors.DarkBlue, true) }
        };

        private Regex tab = new Regex("^(class|\\{\\w+\\}|interface|package|alt|opt|loop|try|group|catch|break|par)$");

        private Regex tabReset = new Regex("else\\s?.*");

        private Regex tabStop = new Regex("\\}|end$");

        public TextDecorarations(UIElement adornedElement) : base(adornedElement)
        {
        }

        private FormattedText FormatWord(string previous, string word, ref int indentLevel)
        {
            Brush foreGround = Brushes.Black;
            FontStyle fontStyle = FontStyles.Normal;

            bool processed = false;
            foreach (var item in _colorCodes)
            {
                if (item.Key.IsMatch(word))
                {
                    foreGround = new SolidColorBrush(item.Value);

                    processed = true;
                }
            }

            string p = previous + " " + word;

            foreach (var item in _mcolorCodes)
            {
                if (item.Key.IsMatch(p))
                {
                    foreGround = new SolidColorBrush(item.Value.Item1);
                    if (item.Value.Item2)
                        fontStyle = FontStyles.Italic;

                    processed = true;
                }
            }

            Typeface tf = new Typeface(new FontFamily("Calibri"), fontStyle, FontWeights.Normal, FontStretches.Normal);

            FormattedText ft = new FormattedText(word, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 14, foreGround,
                VisualTreeHelper.GetDpi(AdornedElement).PixelsPerDip);

            return ft;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var txt = (TextBox)this.AdornedElement;

            Regex reg = new Regex("\\r\\n");
            Regex wordsRegex = new Regex("\\s");

            int indentLevel = 0;

            List<FormattedText> list = new List<FormattedText>();

            double currentX = 0;
            double currentY = 0;

            Typeface tf = new Typeface(new FontFamily("Calibri"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            FormattedText indentText = new FormattedText("    ", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, 14, Brushes.Black,
                VisualTreeHelper.GetDpi(AdornedElement).PixelsPerDip);

            foreach (var line in reg.Split(txt.Text))
            {
                string[] words = wordsRegex.Split(line.Trim());
                if (words.Length < 0)
                    continue;

                if (tabStop.IsMatch(words[0].Trim()))
                {
                    indentLevel--;
                }
                if (tabReset.IsMatch(words[0].Trim()))
                {
                    indentLevel--;
                }

                for (int i = 0; i < indentLevel; i++)
                {
                    drawingContext.DrawText(indentText, new Point(currentX, currentY));
                    currentX += indentText.WidthIncludingTrailingWhitespace;
                }

                for (var x = 0; x < words.Length; x++)
                {
                    var ft = FormatWord(x > 0 ? words[x - 1].Trim() : string.Empty, words[x].Trim() + " ", ref indentLevel);

                    if (tabReset.IsMatch(words[x].Trim()))
                    {
                        indentLevel++;
                    }
                    if (tab.IsMatch(words[x].Trim()))
                    {
                        indentLevel++;
                    }

                    if (indentLevel < 0)
                        indentLevel = 0;

                    drawingContext.DrawText(ft, new Point(currentX, currentY));

                    currentX += ft.WidthIncludingTrailingWhitespace;
                }
                currentY += indentText.Height;
                currentX = 0;
            }
        }
    }
}