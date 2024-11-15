using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    public static class MDColorCodingConfig
    {
        public static Color HeadingColor { get; set; } = Colors.Blue;
        public static Color BoldColor { get; set; } = Colors.Black;
        public static Color ItalicColor { get; set; } = Colors.Black;
        public static Color ListColor { get; set; } = Colors.Green;
        public static Color LinkColor { get; set; } = Colors.DarkGreen;
        public static Color CodeColor { get; set; } = Colors.DarkOrange;

        public static void SaveToSettings()
        {
            var settings = new Dictionary<string, uint>
            {
                { nameof(HeadingColor), HeadingColor.ToUint() },
                { nameof(BoldColor), BoldColor.ToUint() },
                { nameof(ItalicColor), ItalicColor.ToUint() },
                { nameof(ListColor), ListColor.ToUint() },
                { nameof(LinkColor), LinkColor.ToUint() },
                { nameof(CodeColor), CodeColor.ToUint() }
            };

            string json = JsonSerializer.Serialize(settings);
            AppSettings.Default.MDColorSettings = json;
            AppSettings.Default.Save();
        }

        public static void LoadFromSettings()
        {
            string json = AppSettings.Default.MDColorSettings;
            if (!string.IsNullOrEmpty(json))
            {
                var settings = JsonSerializer.Deserialize<Dictionary<string, uint>>(json);
                if (settings != null)
                {
                    HeadingColor = settings[nameof(HeadingColor)].ToColor();
                    BoldColor = settings[nameof(BoldColor)].ToColor();
                    ItalicColor = settings[nameof(ItalicColor)].ToColor();
                    ListColor = settings[nameof(ListColor)].ToColor();
                    LinkColor = settings[nameof(LinkColor)].ToColor();
                    CodeColor = settings[nameof(CodeColor)].ToColor();
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
