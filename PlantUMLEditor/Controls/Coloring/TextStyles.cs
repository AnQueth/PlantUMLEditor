using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace PlantUMLEditor.Controls.Coloring
{
	internal static class TextStyles
	{
		// Predefined styles.  This could eventually be broken out to a file and parsed if desired.
		private static Dictionary<TokenType, TextStyle> _mappings = new()
        {
			{ TokenType.Normal, new TextStyle() },
			{ TokenType.System, new TextStyle { Color = Colors.Coral } },
			{ TokenType.Comment, new TextStyle { Color = Colors.Gray, IsItalics = true } },
			{ TokenType.Note, new TextStyle { Color = Colors.Gray } },
			{ TokenType.Message, new TextStyle { Color = Colors.Firebrick } },
			{ TokenType.Keyword, new TextStyle { Color = Colors.Blue } },
			{ TokenType.Entity, new TextStyle { Color = Colors.Green } }
		};

		public static TextStyle GetStyleForTokenType(TokenType tokenType)
		{
			if (!_mappings.TryGetValue(tokenType, out TextStyle result))
			{
				result = _mappings[TokenType.Normal];
			}

			return result;
		}
	}
}
