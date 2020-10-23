using System.Windows;
using System.Windows.Media;

namespace PlantUMLEditor.Controls.Coloring
{
	internal class TextStyle
	{
		public TextStyle()
		{
			Color = Colors.Black;
		}

		public Color Color { get; set; }

		public bool IsBold { get; set; }

		public bool IsItalics { get; set; }

		public void Apply(FormattedText text, int start, int length)
		{
			// Only do these if they differ from the default; that should save a bunch of work
			// formatting normal text to normal text.

			if (Color != Colors.Black)
			{
				text.SetForegroundBrush(new SolidColorBrush(Color), start, length);
			}

			if (IsBold)
			{
				text.SetFontWeight(FontWeights.Bold, start, length);
			}

			if (IsItalics)
			{
				text.SetFontStyle(FontStyles.Italic, start, length);
			}
		}
	}
}
