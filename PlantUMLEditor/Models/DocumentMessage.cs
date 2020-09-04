using PlantUML;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Input;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class DocumentMessage
    {
        public DocumentMessage()
        {
        }

        public ICommand FixingCommand
        {
            get;
            set;
        }

        public string MissingMethodDataTypeId { get; set; }

        public UMLSequenceDiagram Diagram { get; set; }

        public string FileName { get; set; }

        public int LineNumber { get; set; }

        public bool IsMissingMethod
        {
            get; set;
        }

        public string MissingMethodText { get; set; }

        public string Text { get; set; }

        public bool Warning { get; set; }
        public bool IsMissingDataType { get;   set; }


        public bool IsFixable => IsMissingDataType || IsMissingMethod;
    }
}