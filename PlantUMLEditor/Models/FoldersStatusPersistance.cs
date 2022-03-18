using System.Collections.Generic;
using System.IO;

namespace PlantUMLEditor.Models
{
    internal class FoldersStatusPersistance
    {
        internal void SaveClosedFolders(string folder)
        {
            var closedFolders = GetClosedFolders();
            closedFolders.UnionWith(new string[] { folder });

            Save(closedFolders);

        }
        internal void SaveOpenFolders(string fullPath)
        {
            var closedFolders = GetClosedFolders();
            closedFolders.Remove(fullPath);
            Save(closedFolders);
        }

        private void Save(HashSet<string> folders)
        {
            string closedFolders = Path.Combine(Path.GetTempPath(), "closedfolders.dat");
            using (var sr = new StreamWriter(File.OpenWrite(closedFolders)))
            {
                foreach (var f in folders)
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
            using (var sr = new StreamReader(File.OpenRead(closedFolders)))
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
