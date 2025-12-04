using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models.Runners
{
    static class DOCFXRunner
    {
        internal static string? FindDocFXConfig(string folder)
        {
            foreach (var file in Directory.EnumerateFiles(folder, "docfx.json"))
            {
                return folder;
            }

            foreach (string dir in Directory.EnumerateDirectories(folder))
            {
                string? found = FindDocFXConfig(dir);
                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }

            return null;
        }
        internal static void Run(string? folderBase)
        {
            if (string.IsNullOrEmpty(folderBase))
            {
                return;
            }

            string? folderWithDocFXConfig = FindDocFXConfig(folderBase);

            if (string.IsNullOrEmpty(folderWithDocFXConfig))
            {
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "CMD.EXE";

            psi.UseShellExecute = false;
            psi.ArgumentList.Add("/K");
            psi.ArgumentList.Add(AppSettings.Default.DocFXEXE);
            psi.ArgumentList.Add("--serve");
            psi.WorkingDirectory = folderWithDocFXConfig;

            var p = Process.Start(psi);
        }
    }
}
