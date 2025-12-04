using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Prism.Commands;
using PlantUML;
using UMLModels;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SelectedMessage = null;
        }

        private async Task AddDefaultDataType(TextDocumentModel[] textDocumentModels, MissingDataTypeMessage missingDataTypeMessage)
        {
            UMLClassDiagram? f = Documents.ClassDocuments.FirstOrDefault(p => string.CompareOrdinal(p.Title, DEFAULTSCLASS) == 0);
            if (f != null)
            {
                f.Package.Children.Add(new UMLClass("", "default", null, false, missingDataTypeMessage.MissingDataTypeName, new List<UMLDataType>()));

                ClassDiagramDocumentModel? od = textDocumentModels.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => p.FileName == f.FileName);
                if (od != null)
                {
                    CurrentDocument = od;
                    od.UpdateDiagram(f);
                }
                else
                {
                    od = await OpenDocumenntManager.OpenClassDiagram(f.FileName, f, 0, null) as ClassDiagramDocumentModel;

                    if (od != null)
                    {
                        CurrentDocument = od;
                        od.UpdateDiagram(f);
                    }
                }
            }
            else
            {
                MessageBox.Show("Create a defaults.class document in the root of the work folder first.");
            }
        }

        private async Task AddMissingAttributeToClass(TextDocumentModel[] textDocumentModels, MissingMethodDocumentMessage missingMethodMessage)
        {
            foreach (UMLClassDiagram? doc in Documents.ClassDocuments)
            {
                UMLDataType? d = doc.DataTypes.FirstOrDefault(p => p.Id == missingMethodMessage.MissingMethodDataTypeId);
                if (d == null)
                {
                    continue;
                }

                if (missingMethodMessage.MissingMethodText == null)
                {
                    continue;
                }

                UMLClassDiagramParser.TryParseLineForDataType(missingMethodMessage.MissingMethodText.Trim(),
                    new Dictionary<string, UMLDataType>(), d);

                ClassDiagramDocumentModel? od = textDocumentModels.OfType<ClassDiagramDocumentModel>().FirstOrDefault(p => string.CompareOrdinal(p.FileName, doc.FileName) == 0);
                if (od != null)
                {
                    CurrentDocument = od;
                    od.UpdateDiagram(doc);
                }
                else
                {
                    od = await OpenDocumenntManager.OpenClassDiagram(doc.FileName, doc, 0, null) as ClassDiagramDocumentModel;
                    if (od is not null)
                    {
                        CurrentDocument = od;
                        od.UpdateDiagram(doc);
                    }
                }
            }
        }

        private async void CheckMessages()
        {
            while (true)
            {
                // Wait for file change event (no more polling!)
                await _messageCheckerTrigger.Reader.WaitToReadAsync();

                // Consume all pending signals for debouncing
                while (_messageCheckerTrigger.Reader.TryRead(out _)) { }

                // Small delay to batch rapid file changes
                await Task.Delay(200);

                await _checkMessagesRunning.WaitAsync();

                try
                {
                    if (string.IsNullOrEmpty(_metaDataFile) || string.IsNullOrEmpty(FolderBase))
                    {
                        continue;
                    }

                    if (Application.Current == null)
                    {
                        continue;
                    }

                    List<UMLDiagram> diagrams = new();
                    try
                    {
                        await UpdateDiagramDependencies();
                    }
                    catch
                    {
                    }

                    try
                    {
                        TextDocumentModel[] dm = GetTextDocumentModelReadingArray();

                        foreach (TextDocumentModel? doc in dm)
                        {
                            UMLDiagram? d = await doc.GetEditedDiagram();
                            if (d != null)
                            {
                                d.FileName = doc.FileName;
                                diagrams.Add(d);
                            }
                        }

                        foreach (UMLClassDiagram? doc in Documents.ClassDocuments)
                        {
                            if (!dm.Any(p => p.FileName == doc.FileName))
                            {
                                diagrams.Add(doc);
                            }
                        }
                        foreach (UMLComponentDiagram? doc in Documents.ComponentDiagrams)
                        {
                            if (!dm.Any(p => p.FileName == doc.FileName))
                            {
                                diagrams.Add(doc);
                            }
                        }
                        foreach (UMLSequenceDiagram? doc in Documents.SequenceDiagrams)
                        {
                            if (!dm.Any(p => p.FileName == doc.FileName))
                            {
                                diagrams.Add(doc);
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    DocumentMessageGenerator documentMessageGenerator = new(diagrams);
                    List<DocumentMessage>? newMessages = documentMessageGenerator.Generate(FolderBase);

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        List<DocumentMessage> removals = new();
                        foreach (DocumentMessage? item in Messages)
                        {
                            if (!newMessages.Any(z => string.CompareOrdinal(z.FileName, item.FileName) == 0 &&
                            string.CompareOrdinal(z.Text, item.Text) == 0 && z.LineNumber == item.LineNumber))
                            {
                                removals.Add(item);
                            }
                        }

                        removals.ForEach(p => Messages.Remove(p));

                        foreach (DocumentMessage? item in newMessages)
                        {
                            if (!Messages.Any(z => string.CompareOrdinal(z.FileName, item.FileName) == 0 &&
                           string.CompareOrdinal(z.Text, item.Text) == 0 && z.LineNumber == item.LineNumber))
                            {
                                Messages.Add(item);
                            }
                        }

                        foreach (DocumentMessage? d in Messages)
                        {
                            if ((d is MissingMethodDocumentMessage || d is MissingDataTypeMessage) && d.FixingCommand is null)
                            {
                                d.FixingCommand = new DelegateCommand<DocumentMessage>(FixingCommandHandler);
                            }

                            lock (_docLock)
                            {
                                IEnumerable<BaseDocumentModel>? docs = OpenDocuments.Where(p => string.Equals(p.FileName, d.FileName, StringComparison.Ordinal));
                                foreach (BaseDocumentModel? doc in docs)
                                {
                                    if (CurrentDocument == doc && doc is TextDocumentModel textDoc)
                                    {
                                        textDoc.ReportMessage(d);
                                    }
                                }
                            }
                        }
                    });
                }
                finally
                {
                    _checkMessagesRunning.Release();
                }
            }
        }

        private async void FixingCommandHandler(DocumentMessage sender)
        {
            TextDocumentModel[] textDocumentModels = GetTextDocumentModelReadingArray();

            switch (sender)
            {
                case MissingMethodDocumentMessage missingMethodMessage:
                    await AddMissingAttributeToClass(textDocumentModels, missingMethodMessage);
                    break;

                case MissingDataTypeMessage missingDataTypeMessage:
                    await AddDefaultDataType(textDocumentModels, missingDataTypeMessage);
                    break;
            }
        }
    }
}
