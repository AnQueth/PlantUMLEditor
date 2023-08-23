using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;

namespace PlantUMLEditor.Models
{
    internal static class Statics
    {
        internal static BitmapSource GetClosedFolderIcon()
        {
            var b = new BitmapImage();
            b.BeginInit();
            b.UriSource = new Uri("pack://application:,,,/PlantUMLEditor;component/images/FolderClosed_16x.png");
            b.EndInit();

            return b;
        }

        internal static BitmapSource? GetIcon(string file)
        {


            string? uri = null;

            string ext = Path.GetExtension(file);
            if (file.Contains(".component.puml", StringComparison.OrdinalIgnoreCase))
            {
                uri = @"pack://application:,,,/PlantUMLEditor;component/images/com.png";
            }
            else if (file.Contains(".class.puml", StringComparison.OrdinalIgnoreCase))
            {
                uri = @"pack://application:,,,/PlantUMLEditor;component/images/class.png";
            }
            else if (file.Contains(".seq.puml", StringComparison.OrdinalIgnoreCase))
            {
                uri = @"pack://application:,,,/PlantUMLEditor;component/images/sequence.png";
            }
            else if (FileExtension.MD.Compare(ext))
            {
                uri = @"pack://application:,,,/PlantUMLEditor;component/images/md.png";
            }
            else if (FileExtension.YML.Compare(ext))
            {
                uri = @"pack://application:,,,/PlantUMLEditor;component/images/yml.png";
            }
            else if ((FileExtension.PNG.Compare(ext)) || (FileExtension.JPG.Compare(ext)))
            {
                uri = @"pack://application:,,,/PlantUMLEditor;component/images/emblem_512.png";
            }
            else if (FileExtension.PUML.Compare(ext))
            {

                uri = "pack://application:,,,/PlantUMLEditor;component/images/uml.png";

            }

            else
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(file);
                if (icon != null)
                {
                    return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                return null;
            }

            BitmapImage bi = new BitmapImage();
            bi.BeginInit();

            bi.UriSource = new Uri(uri);
            bi.EndInit();


            return bi;
        }
    }
}
