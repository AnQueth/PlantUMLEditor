using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace PlantUMLEditor.Controls.Coloring
{
	internal class Colorizer
	{
		private const int LINE_SEPARATOR_LENGTH = 2; // \r\n

		public void FormatText(string text, FormattedText formattedText)
		{
			int documentOffset = 0;
			var lines = text.Split("\r\n");
			for (int i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				var lineType = DetermineLineType(SplitLine(line));
				switch (lineType)
				{
					case LineType.Comment:
						documentOffset = ProcessComment(formattedText, line, documentOffset);
						break;

					case LineType.Declaration:
						documentOffset = ProcessDeclaration(formattedText, line, documentOffset);
						break;

					case LineType.DiagramDirection:
						documentOffset = ProcessDiagramDirection(formattedText, line, documentOffset);
						break;

					case LineType.Link:
						documentOffset = ProcessLink(formattedText, line, documentOffset);
						break;

					case LineType.Normal:
						documentOffset = ProcessNormal(formattedText, line, documentOffset);
						break;

					case LineType.Note:
						var result = ProcessNote(formattedText, lines, documentOffset, i);
						documentOffset = result.FormattedTextOffset;

						// Notes can span multiple lines, so make sure we skip already-processed lines
						i = result.LastLineUsedIndex;
						break;


					case LineType.System:
						documentOffset = ProcessSystem(formattedText, line, documentOffset);
						break;

					case LineType.Empty:
					default:
						documentOffset += line.Length + LINE_SEPARATOR_LENGTH;
						break;

				}
			}
		}

		private LineType DetermineLineType(string[] tokens)
		{
			if (tokens.Length == 0)
			{
				return LineType.Empty;
			}

			if ((tokens[0] == "left" || tokens[0] == "top") && tokens[1] == "to")
			{
				// top to bottom direction
				// or
				// left to right direction
				return LineType.DiagramDirection;
			}

			if (tokens[0].StartsWith('\''))
			{
				return LineType.Comment;
			}

			if (Keywords.IsSystemKeyword(tokens[0]))
			{
				return LineType.System;
			}

			if (Keywords.IsDeclaration(tokens[0]))
			{
				return LineType.Declaration;
			}

			if (tokens.Length >= 3 && Keywords.IsArrowIndicator(tokens[1]))
			{
				return LineType.Link;
			}

			if (tokens[0] == "note")
			{
				return LineType.Note;
			}

			return LineType.Normal;
		}

		private int ProcessNormal(FormattedText text, string line, int documentOffset)
		{
			// Format: any line that isn't one of the other types.  Can contain a mixture of keywords
			// and non-keyword text.  Probably doesn't contain entities.
			//
			// Parse the line for keywords only

			// Only need to get this if a keyword is actually found.
			TextStyle keywordStyle = null;

			documentOffset += GetIndentLevel(line);
			var parts = new Queue<string>(SplitLine(line));
			while (parts.Count > 0)
			{
				var item = parts.Dequeue();
				if (Keywords.IsKeyword(item))
				{
					if (keywordStyle == null)
					{
						keywordStyle = TextStyles.GetStyleForTokenType(TokenType.Keyword);
					}

					keywordStyle.Apply(text, documentOffset, item.Length);
				}

				documentOffset += item.Length + (parts.Count == 0 ? LINE_SEPARATOR_LENGTH : 1);
			}

			return documentOffset;
		}

		private int ProcessComment(FormattedText text, string line, int offset)
		{
			// Format: line starts with a '
			return ApplyStyleToEntireLine(TokenType.Comment, text, line, offset);
		}

		private int ProcessSystem(FormattedText text, string line, int offset)
		{
			// Format: @startuml or @enduml
			return ApplyStyleToEntireLine(TokenType.System, text, line, offset);
		}

		private (int FormattedTextOffset, int LastLineUsedIndex) ProcessNote(FormattedText text, string[] allLines, int documentOffset, int lineIndex)
		{
			// Format: starts with "note".
			// Single-line note: begins with "note" and has a : in the line
			// Multi-line note: begins with note, and that and all subsequent lines are note until a line appears
			//		that reads "end note".
			bool isMultiLine = false;
			if (allLines[lineIndex].Trim().StartsWith("note") && !allLines[lineIndex].Contains(':'))
			{
				// Single-line note
				isMultiLine = true;
			}

			documentOffset = ApplyStyleToEntireLine(TokenType.Note, text, allLines[lineIndex], documentOffset);

			if (isMultiLine)
			{
				while (lineIndex < allLines.Length)
				{
					// Keep going until we either hit "end note" or run out of lines
					lineIndex++;
					documentOffset = ApplyStyleToEntireLine(TokenType.Note, text, allLines[lineIndex], documentOffset);

					if (allLines[lineIndex] == "end note")
					{
						break;
					}
				}
			}

			return (documentOffset, lineIndex);
		}

		private int ProcessDeclaration(FormattedText text, string line, int documentOffset)
		{
			// TODO: Potential IndexOutOfRange exceptions; needs validation

			// Format: entity_type entity_name as entity_alias {
			// Everything after entity_name is optional

			documentOffset += GetIndentLevel(line);
			var lineParts = new Queue<string>(SplitLine(line.Trim()));
			var entityTypeStyle = TextStyles.GetStyleForTokenType(TokenType.Keyword);
			var entityNameStyle = TextStyles.GetStyleForTokenType(TokenType.Entity);

			// Part 0 is always the entity type
			documentOffset = ApplyStyleToNextItem(entityTypeStyle, lineParts, text, documentOffset);

			// Part 1 is always the entity name
			documentOffset = ApplyStyleToNextItem(entityNameStyle, lineParts, text, documentOffset);

			// We could be done here
			if (lineParts.Count == 0)
			{
				return documentOffset;
			}

			
			if (lineParts.Peek() == "as")
			{
				// If they exist, parts 2 and 3 are always "as" and the alias
				documentOffset = ApplyStyleToNextItem(entityTypeStyle, lineParts, text, documentOffset);
				documentOffset = ApplyStyleToNextItem(entityNameStyle, lineParts, text, documentOffset);
			}

			// Tack the rest on as normal text
			if (lineParts.Count > 0)
			{
				while (lineParts.Any())
				{
					var item = lineParts.Dequeue();
					documentOffset += item.Length + (lineParts.Count == 0 ? LINE_SEPARATOR_LENGTH : 1);
				}
			}

			return documentOffset;
		}

		private int ProcessLink(FormattedText text, string line, int documentOffset)
		{
			// Format: entity_name arrow entity_name : message
			// The message is optional.

			// TODO: Potential IndexOutOfRange errors
			var entityStyle = TextStyles.GetStyleForTokenType(TokenType.Entity);

			documentOffset += GetIndentLevel(line);
			var parts = new Queue<string>(SplitLine(line));

			// Part 0 is always an entity
			documentOffset = ApplyStyleToNextItem(entityStyle, parts, text, documentOffset);

			// Part 1 is always an arrow and no format is necessary
			var currentItem = parts.Dequeue();
			documentOffset += currentItem.Length + (parts.Count == 0 ? LINE_SEPARATOR_LENGTH : 1);

			// Part 2 is always an entity
			documentOffset = ApplyStyleToNextItem(entityStyle, parts, text, documentOffset);

			if (parts.Count > 0)
			{
				// If part 3 is not a colon, just format the rest of the line as normal text
				TextStyle remainingStyle;
				if (parts.Peek() == ":")
				{
					remainingStyle = TextStyles.GetStyleForTokenType(TokenType.Message);
				}
				else
				{
					remainingStyle = TextStyles.GetStyleForTokenType(TokenType.Normal);
				}

				while (parts.Count > 0)
				{
					documentOffset = ApplyStyleToNextItem(remainingStyle, parts, text, documentOffset);
				}
			}

			return documentOffset;
		}

		private int ProcessDiagramDirection(FormattedText text, string line, int documentOffset)
		{
			// Format: either of the following:
			//		left to right direction
			//		top to bottom direction
			return ApplyStyleToEntireLine(TokenType.Keyword, text, line, documentOffset);
		}

		private int ApplyStyleToEntireLine(TokenType tokenType, FormattedText text, string line, int documentOffset)
		{
			var textStyle = TextStyles.GetStyleForTokenType(tokenType);
			textStyle.Apply(text, documentOffset, line.Length);

			return documentOffset + line.Length + LINE_SEPARATOR_LENGTH;
		}

		private int ApplyStyleToNextItem(TextStyle style, Queue<string> itemQueue, FormattedText document, int documentOffset)
		{
			// This code appeared EVERYWHERE, so I broke it into its own method.
			var currentItem = itemQueue.Dequeue();
			style.Apply(document, documentOffset, currentItem.Length);
			return documentOffset + currentItem.Length + (itemQueue.Count == 0 ? LINE_SEPARATOR_LENGTH : 1);
		}

		private int GetIndentLevel(string line)
		{
			int indent = 0;
			while (line[indent] == ' ')
			{
				indent++;
			}

			return indent;
		}

		private string[] SplitLine(string line)
		{
			// Notes:
			// string.Split() is insufficient, as you can have a single part with spaces in it as long as it's inside
			// brackets or quotation marks.
			//
			// Always trim the line before splitting.  If there are leading spaces the colorizer cares about, line processors
			// should handle those.

			line = line.Trim();

			if (string.IsNullOrEmpty(line)) return new string[0];

			var retList = new List<string>();
			var strPos = 0;
			var isInBracketsOrQuotes = false;
			var currentWordStart = 0;
			while (strPos < line.Length)
			{
				var c = line[strPos];
				if (c == '"' && isInBracketsOrQuotes)
				{
					isInBracketsOrQuotes = false;
				}
				else if (c == '"')
				{
					isInBracketsOrQuotes = true;
				}

				if (c == '[')
				{
					isInBracketsOrQuotes = true;
				}

				if (c == ']')
				{
					isInBracketsOrQuotes = false;
				}

				if (c == ' ')
				{
					if (!isInBracketsOrQuotes)
					{
						// Edge case: space starts the line.  Just move the next word start along with strPos.
						// Because we Trim() at the beginning of the method, this should never be hit.  But I'll
						// leave it here for safety.  Shouldn't affect performance.
						if (c == 0)
						{
							currentWordStart++;
						}
						else
						{
							var word = line.Substring(currentWordStart, strPos - currentWordStart);
							retList.Add(word);

							currentWordStart = strPos + 1;
						}
					}
				}

				strPos++;
			}

			// Add the last word in
			var finalWord = line.Substring(currentWordStart, line.Length - currentWordStart);
			retList.Add(finalWord);

			return retList.ToArray();
		}
	}
}
