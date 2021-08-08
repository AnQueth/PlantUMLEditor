using System.Windows.Input;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class DocumentMessage
    {
     
        public string MissingDataTypeName { get; set; }
        public UMLSequenceDiagram Diagram { get; set; }

        public string FileName { get; set; }

        public string RelativeFileName { get; set; }

        public ICommand FixingCommand
        {
            get;
            set;
        }

        public bool IsFixable => IsMissingDataType || IsMissingMethod;
        public bool IsMissingDataType { get; set; }

        public bool IsMissingMethod
        {
            get; set;
        }

        public int LineNumber { get; set; }
        public string? MissingMethodDataTypeId { get; set; }
        public string? MissingMethodText { get; set; }

        public string Text { get; set; }

        public bool Warning { get; set; }
    }
}