using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    internal class GitSupport
    {
        public GitSupport()
        {
        }

        internal Task<string?> CommitAndSync(string folderBase)
        {
            if (string.IsNullOrEmpty(folderBase))
            {
                return Task.FromResult<string?>(null);
            }

            TaskCompletionSource<string?> tcs = new();
            Task.Run(() =>
            {





                try
                {


                    StringBuilder sb = new();

                    ProcessStartInfo info = new("git", "add *")
                    {
                        WorkingDirectory = folderBase,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                    var p = Process.Start(info);
                    if (p != null)
                    {
                        p.WaitForExit();
                        sb.AppendLine("git add *");
                        sb.AppendLine(p.StandardOutput.ReadToEnd());
                        sb.AppendLine(p.StandardError.ReadToEnd());
                    }

                    string s = $"commit -m \"{DateTimeOffset.Now}\"";

                    info = new("git", s)
                    {
                        WorkingDirectory = folderBase,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                    p = Process.Start(info);
                    if (p != null)
                    {
                        p.WaitForExit();
                        sb.Append("git ");
                        sb.AppendLine(s);
                        sb.AppendLine(p.StandardOutput.ReadToEnd());
                        sb.AppendLine(p.StandardError.ReadToEnd());
                    }


                    info = new("git", "push")
                    {
                        WorkingDirectory = folderBase,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    };
                    p = Process.Start(info);
                    if (p != null)
                    {

                        p.WaitForExit();

                        sb.AppendLine("git push");
                        sb.AppendLine(p.StandardOutput.ReadToEnd());
                        sb.AppendLine(p.StandardError.ReadToEnd());
                    }
                    tcs.SetResult(sb.ToString());
                }
                catch (Exception ex)
                {
                    tcs.SetResult(ex.ToString());
                }
            });

            return tcs.Task;
        }
    }
}