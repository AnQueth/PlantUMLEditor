using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models.Runners
{
    /// <summary>
    ///  PlantUML output path notes (Windows):
    /// - The -o option must point to a valid directory.
    /// - Path must be the same directory as the diagram file, or a relative subfolder.
    /// - Works: -o "C:\Users\aaron\AppData\Local\Temp" (no trailing backslash).
    /// - Fails: -o "C:\Users\aaron\AppData\Local\Temp\" because \" is parsed as an escaped quote,
    ///   so Java/PlantUML misreads the argument and no diagram is generated.
    /// - If you need a trailing backslash, escape it as "\\", e.g. -o "C:\Users\aaron\AppData\Local\Temp\\"
    /// - Rule of thumb: avoid single backslash at the end of quoted Windows paths,
    ///   and keep output directory aligned with the diagram’s location.
    /// </summary>
    internal class PlantUMLImageGenerator
    {
        private readonly string jarLocation;
        private readonly string path;
        private readonly string dir;
                private readonly bool svg;

        public PlantUMLImageGenerator(string jarLocation, string path, string dir, bool svg)
        {
            this.jarLocation = jarLocation;
            this.path = path;
            this.dir = dir;
            this.svg = svg;
        }

        internal record UMLImageCreateRecord(string fileName, string normal, string errors);

        internal Task<UMLImageCreateRecord> Create()
        {
            TaskCompletionSource<UMLImageCreateRecord> taskCompletionSource = new TaskCompletionSource<UMLImageCreateRecord>();

            Task.Run(() =>
            {


                string fn = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) +
                    (this.svg ?  FileExtension.SVG : FileExtension.PNG));

                var svgString = this.svg ? "-tsvg" : string.Empty;

                Process p = new();

                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName = "java.exe";
                p.StartInfo.Arguments = $"-Xmx1024m -DPLANTUML_LIMIT_SIZE=20000 -jar {jarLocation} {svgString} -o \"{dir}\" \"{path}\"";
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                p.Start();
                p.WaitForExit();

                string? normal = p.StandardOutput.ReadToEnd();
                string? errors = p.StandardError.ReadToEnd();

                taskCompletionSource.SetResult(new UMLImageCreateRecord(fn, normal, errors));
            });
            return taskCompletionSource.Task;
        }
    }
}