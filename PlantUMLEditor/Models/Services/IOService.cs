using PlantUMLEditor.Models;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PlantUMLEditor.Models.Services
{
    internal class IOService : IIOService
    {
        public string? GetDirectory()
        {
            FolderBrowserDialog ofd = new();

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                return ofd.SelectedPath;
            }
            return null;
        }

        public string? GetFile(params string[] extensions)
        {
            OpenFileDialog ofd = new();
            ofd.Filter = "Files|" + string.Join(";", extensions.Select(ext => "*" + ext));
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                return ofd.FileName;
            }
            return null;
        }
        public string? GetSaveFile(string filter, string defaultExt)
        {
            SaveFileDialog sfd = new()
            {
                Filter = filter,
                DefaultExt = defaultExt
            };
            if (sfd.ShowDialog().GetValueOrDefault())
            {
                return sfd.FileName;
            }
            return null;
        }

        public string? NewFile(string directory, string fileExtension)
        {
            SaveFileDialog ofd = new()
            {
                InitialDirectory = directory,
                DefaultExt = fileExtension,
                Filter = "UML|*" + fileExtension
            };

            if (ofd.ShowDialog().GetValueOrDefault())
            {
                File.CreateText(ofd.FileName).Dispose();
                return ofd.FileName;
            }

            return null;
        }
    }
}