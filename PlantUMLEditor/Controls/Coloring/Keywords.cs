using System.Linq;

namespace PlantUMLEditor.Controls.Coloring
{
	/// <summary>
	/// List of keywords PlantUML supports, organized by type.  Note that some types overlap.
	/// </summary>
	internal static class Keywords
	{
		// Full set of supported keywords
		private static string[] _keywords = new string[] 
		{
			"actor", "alt", "as", "break", "boundary", "catch", "class", "collections", "component", "control", "database", "else", "end",
			"entity", "enum", "group", "interface", "loop", "of", "opt", "par", "participant", "package", "queue", "rectangle", "title",
			"together", "try"
		};

		// System declaration keywords
		private static string[] _systemKeywords = new string[] { "@startuml", "@enduml" };

		// Anything that indicates a line of some sort:  arrow types and line types.
		private static char[] _lineIndicators = new char[] { '>', '<', '/', '\\', '^', '#', '*', '+', '{', '}', '(', ')', '.', '-' };

		// Anything that indicates a line type
		private static char[] _lineTypes = new char[] { '.', '-' };

		// Keywords that indicate entity declarations
		private static string[] _entityKeywords = new string[]
		{
			"actor", "boundary", "class", "collections", "component", "control", "database", "entity", "enum", "interface", "package",
			"participant", "rectangle"
		};

		/// <summary>
		/// Determines if a string is a generic PlantUML keyword.
		/// </summary>
		/// <param name="candidate">The candidate string.</param>
		/// <returns>True if it's a keyword; otherwise false.</returns>
		/// <remarks>
		///		There is overlap between keywords.  For example, "participant" is both a general keyword and an entity
		///		declaration keyword.
		///	</remarks>
		public static bool IsKeyword(string candidate)
		{
			return _keywords.Any(kw => kw == candidate);
		}

		/// <summary>
		/// Determines if a string is a system PlantUML keyword.
		/// </summary>
		/// <param name="candidate">The candidate string.</param>
		/// <returns>True if it's a keyword; otherwise false.</returns>
		/// <remarks>
		///		System keywords are used to indicate the start and end of a PlantUML document.
		///		System keywords are not included in the generic keyword list.
		///	</remarks>
		public static bool IsSystemKeyword(string candidate)
		{
			return _systemKeywords.Any(kw => kw == candidate);
		}

		/// <summary>
		/// Determines if a string is a arrow.
		/// </summary>
		/// <param name="candidate">The candidate string.</param>
		/// <returns>True if it's an arrow; otherwise false.</returns>
		/// <remarks>
		///		Arrows are the defined here as the lines between entities in various documents.
		///	</remarks>
		public static bool IsArrowIndicator(string candidate)
		{
			if (candidate.Length == 0) return false;
			if (_lineIndicators.Any(at => at == candidate[0] || at == candidate.Last()) 
				&& _lineTypes.Any(lt => candidate.Contains(lt)))
			{
				return true;
			}

			// x and o are special cases
			if (candidate.StartsWith('x') || candidate.StartsWith('o'))
			{
				// It's just a stray x or o
				if (candidate.Length == 1) return false;

				if (_lineIndicators.Any(at => at == candidate[1]))
				{
					return true;
				}
			}

			if (candidate.EndsWith('x') || candidate.EndsWith('o'))
			{
				// Stray x or o
				if (candidate.Length == 1) return false;

				if (_lineIndicators.Any(at => at == candidate[candidate.Length - 2]))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Determines if a string is an entity declaration keyword.
		/// </summary>
		/// <param name="candidate">The candidate string.</param>
		/// <returns>True if it's a declaration keyword; otherwise false.</returns>
		/// <remarks>
		///		There is overlap between keywords.  For example, "participant" is both a general keyword and an entity 
		///		declaration keyword.
		///	</remarks>

		public static bool IsDeclaration(string candidate)
		{
			return _entityKeywords.Any(ek => ek == candidate);
		}
	}
}
