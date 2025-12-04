using Newtonsoft.Json;
using System;

namespace PlantUMLEditor.Models
{

    internal class UISettingsModel
    {
        public int WindowHeight
        {
            get; set;
        }

        public int WindowLeft
        {
            get; set;
        }

        public int WindowTop
        {
            get; set;
        }

        public int WindowWidth
        {
            get; set;
        }
        private readonly Lazy<GridSettings> _gridSettingLoader;

        public GridSettings GridSettings => _gridSettingLoader.Value;

        private void GridSettingsChanged()
        {
            AppSettings.Default.GridSettings = JsonConvert.SerializeObject(GridSettings);
            AppSettings.Default.Save();
        }

        public void UISizeChanged()
        {
            AppSettings.Default.WindowWidth = WindowWidth;
            AppSettings.Default.WindowHeight = WindowHeight;
            AppSettings.Default.WindowTop = WindowTop;
            AppSettings.Default.WindowLeft = WindowLeft;
            AppSettings.Default.Save();
        }
        public UISettingsModel()
        {
            WindowWidth = AppSettings.Default.WindowWidth;
            WindowHeight = AppSettings.Default.WindowHeight;
            WindowTop = AppSettings.Default.WindowTop;
            WindowLeft = AppSettings.Default.WindowLeft;

            _gridSettingLoader = new Lazy<GridSettings>(() =>
            {
                GridSettings l = !string.IsNullOrEmpty(AppSettings.Default.GridSettings) ?
                JsonConvert.DeserializeObject<GridSettings>(AppSettings.Default.GridSettings) ?? new GridSettings() :
                new GridSettings();

                l.ChangedCB = GridSettingsChanged;

                return l;
            });
        }


    }
}