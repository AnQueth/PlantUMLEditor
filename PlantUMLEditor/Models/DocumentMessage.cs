using System.Windows.Input;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class MissingMethodDocumentMessage : DocumentMessage
    {
        public MissingMethodDocumentMessage(string fileName, string relativeFileName, int lineNumber, string text,
          bool warning, string missingMethodText, string missingMethodDataTypeId, UMLSequenceDiagram diagram, bool isMissingMethod) :
      base(fileName, relativeFileName, lineNumber, text, warning)
        {
            MissingMethodText = missingMethodText;
            MissingMethodDataTypeId = missingMethodDataTypeId;
            Diagram = diagram;
            IsMissingMethod = isMissingMethod;

        }

        public string MissingMethodDataTypeId
        {
            get; init;
        }
        public string MissingMethodText
        {
            get; init;
        }

        public UMLSequenceDiagram? Diagram
        {
            get; init;
        }

        public bool IsMissingMethod
        {
            get; init;
        }

        public override bool IsFixable => true;

    }

    public class MissingDataTypeMessage : DocumentMessage
    {
        public MissingDataTypeMessage(string fileName, string relativeFileName, int lineNumber,
            string text, bool warning, string missingDataTypeName, bool isMissingDataType) :
       base(fileName, relativeFileName, lineNumber, text, warning)
        {
            Warning = warning;
            MissingDataTypeName = missingDataTypeName;
            IsMissingDataType = isMissingDataType;
        }
        public string MissingDataTypeName
        {
            get; init;
        }

        public bool IsMissingDataType
        {
            get; init;
        }



        public override bool IsFixable => true;
    }

    public class DocumentMessage
    {
        public DocumentMessage(string fileName, string relativeFileName, int lineNumber, string text, bool warning = false)
        {
            FileName = fileName;
            RelativeFileName = relativeFileName;
            LineNumber = lineNumber;
            Text = text;
            Warning = warning;
        }








        public string FileName
        {
            get; init;
        }

        public string RelativeFileName
        {
            get; init;
        }

        public ICommand? FixingCommand
        {
            get;
            set;
        }

        public virtual bool IsFixable => false;


        public int LineNumber
        {
            get; init;
        }


        public string Text
        {
            get; init;
        }

        public bool Warning
        {
            get; init;
        }
    }
}