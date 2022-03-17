using System;
using System.Diagnostics;

namespace PlantUMLEditor.Models
{
    internal class GitSupport
    {
        public GitSupport()
        {
        }

        internal void CommitAndSync(string folderBase)
        {
            if (string.IsNullOrEmpty(folderBase))
            {
                return;
            }

            ProcessStartInfo info = new("git", "add *")
            {
                WorkingDirectory = folderBase
            };
            var p = Process.Start(info);

            p.WaitForExit();

            info = new("git", $"commit -m \"{DateTimeOffset.Now}\"")
            {
                WorkingDirectory = folderBase
            };
            p = Process.Start(info);

            p.WaitForExit();


            info = new("git", "push")
            {
                WorkingDirectory = folderBase
            };
            p = Process.Start(info);

            p.WaitForExit();


        }
    }
}