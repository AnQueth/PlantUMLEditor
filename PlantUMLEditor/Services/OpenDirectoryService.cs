using Microsoft.Win32;
using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PlantUMLEditor.Services
{
    class OpenDirectoryService : IOpenDirectoryService
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

        public string NewFile(string directory)
        {
            SaveFileDialog ofd = new SaveFileDialog();
            ofd.InitialDirectory = directory;
            ofd.DefaultExt = ".puml";
           
            if (ofd.ShowDialog().GetValueOrDefault())
            {
                return ofd.FileName;
            }

            return null;
        }
    }
}
