using System.Diagnostics;
using System.IO;

namespace PlantUMLEditor.Models
{
    internal class PlantUMLImageGenerator
    {
        private readonly string jarLocation;
        private readonly string path;
        private readonly string dir;

        public PlantUMLImageGenerator()
        {
        }

        public PlantUMLImageGenerator(string jarLocation, string path, string dir)
        {
            this.jarLocation = jarLocation;
            this.path = path;
            this.dir = dir;
        }

        internal string Create(out string normal, out string errors)
        {
            string fn = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + ".png");

            Process p = new();

            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "java.exe";
            p.StartInfo.Arguments = $"-Xmx1024m -DPLANTUML_LIMIT_SIZE=20000 -jar {jarLocation} \"{path}\"";
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            p.Start();
            p.WaitForExit();

            normal = p.StandardOutput.ReadToEnd();
            errors = p.StandardError.ReadToEnd();

            return fn;
        }
    }
}