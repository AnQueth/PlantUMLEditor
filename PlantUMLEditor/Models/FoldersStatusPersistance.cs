using System.Collections.Generic;
using System.IO;

namespace PlantUMLEditor.Models
{
    internal class FoldersStatusPersistance
    {
        internal void SaveClosedFolders(string folder)
        {
            HashSet<string>? closedFolders = GetClosedFolders();
            closedFolders.UnionWith(new string[] { folder });

            Save(closedFolders);

        }
        internal void SaveOpenFolders(string fullPath)
        {
            HashSet<string>? closedFolders = GetClosedFolders();
            closedFolders.Remove(fullPath);
            Save(closedFolders);
        }

        private void Save(HashSet<string> folders)
        {
            string closedFolders = Path.Combine(Path.GetTempPath(), "closedfolders.dat");
            using (StreamWriter? sr = new StreamWriter(File.OpenWrite(closedFolders)))
            {
                foreach (string? f in folders)
                {
                    sr.WriteLine(f);
                }
            }
        }

        public HashSet<string> GetClosedFolders()
        {
            HashSet<string> closedFoldersList = new();
            string closedFolders = Path.Combine(Path.GetTempPath(), "closedfolders.dat");
            if (!File.Exists(closedFolders))
            {
                return closedFoldersList;
            }

            Read(closedFoldersList, closedFolders);

            return closedFoldersList;
        }

        private static void Read(HashSet<string> closedFoldersList, string closedFolders)
        {
            using (StreamReader? sr = new StreamReader(File.OpenRead(closedFolders)))
            {
                string? line;
                while ((line = sr.ReadLine()) != null)
                {

                    closedFoldersList.Add(line);
                }
            }

        }


    }
}
