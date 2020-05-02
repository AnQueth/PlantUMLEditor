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
        private UMLDocumentCollection documents;
        private ObservableCollection<DocumentMessage> messages;

        public DocumentMessageGenerator(UMLDocumentCollection documents, ObservableCollection<DocumentMessage> messages)
        {
            this.documents = documents;
            this.messages = messages;
        }

        public void Generate()
        {
            List<DocumentMessage> newMessages = new List<DocumentMessage>();

            var items = from o in documents.SequenceDiagrams
                        let w = from z in o.LifeLines
                                where z.Warning != null
                                select z
                        from f in w
                        where f.Warning != null
                        select new { o, f };

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

            foreach (var i in documents.SequenceDiagrams)
            {
                CheckEntities(i.FileName, i.Entities);
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

            void CheckEntities(string fileName, List<UMLOrderedEntity> entities)
            {
                foreach (var g in entities)
                {
                    if (g.Warning != null)
                    {
                        newMessages.Add(new DocumentMessage()
                        {
                            FileName = fileName,
                            LineNumber = g.LineNumber,
                            Text = g.Warning,
                            Warning = true
                        });
                    }

                    if (g is UMLSequenceBlockSection s)
                    {
                        CheckEntities(fileName, s.Entities);
                    }
                }
            }
        }
    }
}