using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models.Runners
{
    static class ExplorerRunner
    {
        public static void Run(string? folderBase)
        {
            if (string.IsNullOrEmpty(folderBase))
            {
                return;
            }

            ProcessStartInfo psi = new()
            {
                UseShellExecute = true,
                WorkingDirectory = folderBase,
                FileName = folderBase
            };
            Process.Start(psi);
        }
    }
}
