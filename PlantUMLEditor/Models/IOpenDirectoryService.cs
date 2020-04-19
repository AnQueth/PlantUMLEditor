using System;
using System.Collections.Generic;
using System.Text;

namespace PlantUMLEditor.Models
{
   public interface IOpenDirectoryService
    {
        string GetDirectory();

        string NewFile(string directory);
    }
}
