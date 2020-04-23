using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Documents;

namespace PlantUMLEditor
{
    public interface IColorCoder
    {
        Span Create(string text);
    }
}
