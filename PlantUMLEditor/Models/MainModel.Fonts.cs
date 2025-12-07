using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        private static readonly Lazy<ObservableCollection<FontFamily>> _availableFontsLazy =
            new(() =>
            {
                var fonts = Fonts.SystemFontFamilies
                    .OrderBy(f => f.Source, StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

                return new ObservableCollection<FontFamily>(fonts);
            }, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly Lazy<FontFamily> _initialSelectedFontLazy =
            new(() =>
            {
                try
                {
                    string? saved = AppSettings.Default.SelectedFont;
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        var match = Fonts.SystemFontFamilies.FirstOrDefault(f => string.Equals(f.Source, saved, StringComparison.OrdinalIgnoreCase));
                        if (match != null) return match;

                        return new FontFamily(saved);
                    }
                }
                catch
                {
                }

                try
                {
                    if (Application.Current != null && Application.Current.Resources.Contains("FontFamily.Primary"))
                    {
                        if (Application.Current.Resources["FontFamily.Primary"] is FontFamily ff)
                        {
                            var match = Fonts.SystemFontFamilies.FirstOrDefault(f => string.Equals(f.Source, ff.Source, StringComparison.OrdinalIgnoreCase));
                            return match ?? ff;
                        }
                    }
                }
                catch
                {
                }

                var seg = Fonts.SystemFontFamilies.FirstOrDefault(f => f.Source.IndexOf("Segoe UI", StringComparison.InvariantCultureIgnoreCase) >= 0);
                return seg ?? Fonts.SystemFontFamilies.OrderBy(f => f.Source).FirstOrDefault() ?? new FontFamily("Segoe UI, Consolas, Lucida Sans");
            }, LazyThreadSafetyMode.ExecutionAndPublication);

        private FontFamily? _selectedFont;

        public ObservableCollection<FontFamily> AvailableFonts => _availableFontsLazy.Value;

        public FontFamily SelectedFont
        {
            get
            {
                if (_selectedFont == null)
                {
                    _selectedFont = _initialSelectedFontLazy.Value;
                }
                return _selectedFont!;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                if (string.Equals(_selectedFont?.Source, value.Source, StringComparison.Ordinal))
                    return;

                SetValue(ref _selectedFont, value);

                try
                {
                    AppSettings.Default.SelectedFont = value.Source;
                    AppSettings.Default.Save();
                }
                catch
                {
                }

                // Update resource in application and window resource dictionaries so controls that merged Styles.xaml pick it up
                try
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        UpdateResourceRecursive(Application.Current.Resources, "FontFamily.Primary", new FontFamily(value.Source));

                        // Also update any open windows' resources and their merged dictionaries
                        foreach (Window? win in Application.Current.Windows)
                        {
                            try
                            {
                                if (win?.Resources != null)
                                {
                                    UpdateResourceRecursive(win.Resources, "FontFamily.Primary", new FontFamily(value.Source));
                                }
                            }
                            catch
                            {
                                // ignore individual window errors
                            }
                        }
                    });
                }
                catch
                {
                }
            }
        }

        // Recursively search the provided ResourceDictionary and its merged dictionaries; if key exists, set it. If not found, add to the top level dictionary.
        private static void UpdateResourceRecursive(ResourceDictionary dict, string key, object value)
        {
            if (dict == null) return;

            // If the dictionary contains the key, update and return
            if (dict.Contains(key))
            {
                dict[key] = value;
                return;
            }

            // Otherwise search merged dictionaries
            foreach (var md in dict.MergedDictionaries)
            {
                try
                {
                    UpdateResourceRecursive(md, key, value);
                    // If merged dictionary contained the key it would have been set; check again
                    if (md.Contains(key)) return;
                }
                catch
                {
                }
            }

            // If not found in any nested dictionaries and this is the application resources, set it here so DynamicResource can resolve
            // We treat dictionaries that are the top level (no parent) as a place to add.
            try
            {
                // Add to this dictionary if it's the application resources or if key still not present anywhere
                if (!dict.Contains(key))
                {
                    dict[key] = value;
                }
            }
            catch
            {
            }
        }
    }
}
