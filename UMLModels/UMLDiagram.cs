using System.Collections.Generic;

namespace UMLModels
{
    public abstract class UMLDiagram
    {
        private readonly List<UMLNoteConnection> _noteConnections = new();
        public IReadOnlyCollection<UMLNoteConnection> NoteConnections => _noteConnections;
        public UMLDiagram(string title, string fileName)
        {
            Title = title;
            FileName = fileName;
        }
        public string FileName
        {
            get; set;
        }

        public string Title
        {
            get; set;
        }
        public void AddNoteConnection(UMLNoteConnection uMLNoteConnection)
        {
            _noteConnections.Add(uMLNoteConnection);
        }
        public void AddLineError(string text, int lineNumber)
        {
            if (string.IsNullOrEmpty(text) || text == "}" || text == "@startuml" || text == "@enduml")
            {
                return;
            }

            LineErrors.Add(new LineError(text, lineNumber));
        }


        public List<LineError> LineErrors
        {
            get;
        } = new();
    }
}