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

        public ICommand CreateMissingMethodCommand
        {
            get;
            set;
        }

        public string DataTypeId { get; set; }

        public UMLSequenceDiagram Diagram { get; set; }

        public string FileName { get; set; }

        public int LineNumber { get; set; }

        public bool MissingMethod
        {
            get; set;
        }

        public string OffendingText { get; set; }

        public string Text { get; set; }

        public bool Warning { get; set; }
    }
}