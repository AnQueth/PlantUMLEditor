using PlantUMLEditor.Models;
using System.IO;
using System.Windows.Forms;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PlantUMLEditor.Services
{
    internal class OpenDirectoryService : IOpenDirectoryService
    {
        public string GetDirectory()
        {
            FolderBrowserDialog ofd = new FolderBrowserDialog();

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                return ofd.SelectedPath;
            }
            return null;
        }

        public string NewFile(string directory, string fileExtension)
        {
            SaveFileDialog ofd = new SaveFileDialog();
            ofd.InitialDirectory = directory;
            ofd.DefaultExt = fileExtension;
            ofd.Filter = "UML|*" + fileExtension;
           

            if (ofd.ShowDialog().GetValueOrDefault())
            {
                File.CreateText(ofd.FileName);
                return ofd.FileName;
            }

            return null;
        }
    }
}