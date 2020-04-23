using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PlantUMLEditor.Models
{
    public class PreviewDiagramModel : BindingBase
    {
        private BitmapSource image;
        private string title;

        public string Title
        {
            get { return title; }
            set { SetValue(ref title, value); }
        }

        public BitmapSource Image
        {
            get { return image; }
            set { SetValue(ref image, value); }
        }


        public async Task ShowImage(string jar, string path)
        {

            await Task.Run(() =>
            {


                Process p = new Process();

                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName = "java.exe";
                p.StartInfo.Arguments = $"-jar {jar} \"{path}\"";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                p.Start();
                p.WaitForExit();

                string l = p.StandardOutput.ReadToEnd();
                string e = p.StandardError.ReadToEnd();
            });

            var b = new BitmapImage( new Uri( Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".png")));
            Image = b;
            
        }
    }
}
