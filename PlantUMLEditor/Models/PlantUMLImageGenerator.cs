using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    internal class PlantUMLImageGenerator
    {
        private readonly string jarLocation;
        private readonly string path;
        private readonly string dir;


        public PlantUMLImageGenerator(string jarLocation, string path, string dir)
        {
            this.jarLocation = jarLocation;
            this.path = path;
            this.dir = dir;
        }

        internal record UMLImageCreateRecord(string fileName, string normal, string errors);

        internal Task<UMLImageCreateRecord> Create()
        {
            TaskCompletionSource<UMLImageCreateRecord> taskCompletionSource = new TaskCompletionSource<UMLImageCreateRecord>();

            Task.Run(() =>
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

                var normal = p.StandardOutput.ReadToEnd();
                var errors = p.StandardError.ReadToEnd();

                taskCompletionSource.SetResult(new UMLImageCreateRecord(fn, normal, errors));
            });
            return taskCompletionSource.Task;
        }
    }
}