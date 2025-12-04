using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prism.Commands;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel
    {
        private GlobalFindResult? _selectedFindResult;

        public GlobalFindResult? SelectedGlobalFindResult
        {
            get => _selectedFindResult;
            set
            {
                _selectedFindResult = value;
                if (value != null)
                {
                    AttemptOpeningFile(value.FileName,
                      value.LineNumber, value.SearchText).ConfigureAwait(false);
                }
            }
        }

        private void FindAllReferencesInvoked(string text)
        {
            SelectedToolTab = 2;
            FindReferenceResults.Clear();

            foreach (var item in DataTypeServices.FindAllReferences(Documents, text))
            {
                FindReferenceResults.Add(item);
            }
        }

        private async void GlobalSearchHandler(string obj)
        {
            if (string.IsNullOrEmpty(FolderBase))
            {
                return;
            }

            List<GlobalFindResult>? findresults = await GlobalSearch.Find(obj, new string[]
            {WILDCARD + FileExtension.PUML.Extension, WILDCARD + FileExtension.MD.Extension, WILDCARD + FileExtension.YML.Extension
            });
            GlobalFindResults.Clear();
            foreach (GlobalFindResult? f in findresults)
            {
                GlobalFindResults.Add(f);
            }
        }

        private async void GotoDefinitionInvoked(string text)
        {
            foreach (var item in DataTypeServices.GotoDefinition(Documents, text))
            {
                await AttemptOpeningFile(item.FileName, item.DataType.LineNumber, null);
            }
        }
    }
}
