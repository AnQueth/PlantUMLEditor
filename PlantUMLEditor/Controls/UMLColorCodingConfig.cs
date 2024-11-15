using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    public static class UMLColorCodingConfig
    {
        public static Color StartEndColor { get; set; } = Colors.Coral;
        public static Color DirectionColor { get; set; } = Colors.MediumPurple;
        public static Color KeywordColor { get; set; } = Colors.Blue;
        public static Color PortColor { get; set; } = Colors.Chocolate;
        public static Color ControlFlowColor { get; set; } = Colors.Blue;
        public static Color GroupKeywordColor { get; set; } = Colors.Green;
        public static Color SingleGroupColor { get; set; } = Colors.Firebrick;
        public static Color BracketColor { get; set; } = Colors.Green;
        public static Color ParenthesisColor { get; set; } = Colors.Black;
        public static Color NoteColor { get; set; } = Colors.Gray;
        public static Color CommentColor { get; set; } = Colors.DarkGreen;

        public static void SaveToSettings()
        {
            var settings = new Dictionary<string, uint>
            {
                { nameof(StartEndColor), StartEndColor.ToUint() },
                { nameof(DirectionColor), DirectionColor.ToUint() },
                { nameof(KeywordColor), KeywordColor.ToUint() },
                { nameof(PortColor), PortColor.ToUint() },
                { nameof(ControlFlowColor), ControlFlowColor.ToUint() },
                { nameof(GroupKeywordColor), GroupKeywordColor.ToUint() },
                { nameof(SingleGroupColor), SingleGroupColor.ToUint() },
                { nameof(BracketColor), BracketColor.ToUint() },
                { nameof(ParenthesisColor), ParenthesisColor.ToUint() },
                { nameof(NoteColor), NoteColor.ToUint() },
                { nameof(CommentColor), CommentColor.ToUint() }
            };

            string json = JsonSerializer.Serialize(settings);
            AppSettings.Default.ColorSettings = json;
            AppSettings.Default.Save();
        }

        public static void LoadFromSettings()
        {
            string json = AppSettings.Default.ColorSettings;
            if (!string.IsNullOrEmpty(json))
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, uint>>(json);
                if (settings != null)
                {
                    StartEndColor = settings[nameof(StartEndColor)].ToColor();
                    DirectionColor = settings[nameof(DirectionColor)].ToColor();
                    KeywordColor = settings[nameof(KeywordColor)].ToColor();
                    PortColor = settings[nameof(PortColor)].ToColor();
                    ControlFlowColor = settings[nameof(ControlFlowColor)].ToColor();
                    GroupKeywordColor = settings[nameof(GroupKeywordColor)].ToColor();
                    SingleGroupColor = settings[nameof(SingleGroupColor)].ToColor();
                    BracketColor = settings[nameof(BracketColor)].ToColor();
                    ParenthesisColor = settings[nameof(ParenthesisColor)].ToColor();
                    NoteColor = settings[nameof(NoteColor)].ToColor();
                    CommentColor = settings[nameof(CommentColor)].ToColor();
                }
            }
        }

        private static uint ToUint(this Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }

        private static Color ToColor(this uint argb)
        {
            return Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }
    }
}
