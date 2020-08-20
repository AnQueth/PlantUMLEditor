using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using UMLModels;

namespace PlantUMLEditor.Models
{
    public class DocumentMessageGenerator
    {
        private IEnumerable<UMLDiagram> documents;
        private ObservableCollection<DocumentMessage> messages;

        public DocumentMessageGenerator(IEnumerable<UMLDiagram> documents, ObservableCollection<DocumentMessage> messages)
        {
            this.documents = documents;
            this.messages = messages;
        }

        public void Generate()
        {
            List<DocumentMessage> newMessages = new List<DocumentMessage>();

            foreach (var doc in this.documents)
            {
                if(doc is UMLComponentDiagram f)
                {
                    foreach (var e in f.Errors) {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = f.FileName,
                            Text = e.Line,
                            LineNumber = e.LineNumber,

                            Warning = false
                        });
                    }
                }
                if (doc is UMLClassDiagram f2)
                {
                    foreach (var e in f2.Errors)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = f2.FileName,
                            Text = e.Value,
                            LineNumber = e.LineNumber,

                            Warning = false
                        });
                    }
                }
                if (doc is UMLSequenceDiagram o)
                {
                    var items = from z in o.LifeLines
                                where z.Warning != null
                                select new { o = doc, f = z };

                    foreach (var i in items)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = i.o.FileName,
                            Text = i.f.Warning,
                            LineNumber = i.f.LineNumber,

                            Warning = true
                        });
                    }

                    CheckEntities(o.FileName, o.Entities, o);
                }
            }

            List<DocumentMessage> removals = new List<DocumentMessage>();
            foreach (var item in messages)
            {
                DocumentMessage m;
                if ((m = newMessages.FirstOrDefault(z => z.FileName == item.FileName && z.Text == item.Text)) == null)
                {
                    removals.Add(item);
                }
            }

            removals.ForEach(p => messages.Remove(p));

            foreach (var item in newMessages)
            {
                DocumentMessage m;
                if ((m = messages.FirstOrDefault(z => z.FileName == item.FileName && z.Text == item.Text)) == null)
                {
                    messages.Add(item);
                }
            }

            void CheckEntities(string fileName, List<UMLOrderedEntity> entities, UMLSequenceDiagram o)
            {
                foreach (var g in entities)
                {
                    if (g.Warning != null && g is UMLSequenceConnection c)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = fileName,
                            LineNumber = g.LineNumber,
                            Text = g.Warning,
                            OffendingText = c.Action?.Signature,
                            DataTypeId = c.To?.DataTypeId,
                            Diagram = o,
                            MissingMethod = true,
                            Warning = true
                        });
                    }

                    if (g is UMLSequenceBlockSection s)
                    {
                        CheckEntities(fileName, s.Entities, o);
                    }
                }
            }
        }
    }
}