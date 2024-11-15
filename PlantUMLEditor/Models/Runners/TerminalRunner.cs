using System;
using System.ComponentModel;
using System.Diagnostics;

namespace PlantUMLEditor.Models.Runners
{
    internal static class TerminalRunner
    {
        internal static void Run(string? folderBase)
        {
            if (string.IsNullOrEmpty(folderBase))
            {
                return;
            }

            try
            {
                ProcessStartInfo psi = new()
                {
                    UseShellExecute = true,
                    FileName = "wt",
                    WorkingDirectory = folderBase
                };
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(folderBase);

                Process.Start(psi);
            }
            catch (Win32Exception)
            {
                ProcessStartInfo psi = new()
                {
                    UseShellExecute = true,
                    FileName = "cmd",
                    WorkingDirectory = folderBase
                };

                Process.Start(psi);
            }
        }
    }
}