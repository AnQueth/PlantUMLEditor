using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantUMLEditor.Controls
{
    public interface IIndenter
    {
        int GetIndentLevelForLine(string text, int line) => 0;

        string Process(string text, bool removeLines) => text;
    }
}
