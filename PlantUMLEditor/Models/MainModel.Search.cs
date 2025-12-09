using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prism.Commands;
using System.Linq;

namespace PlantUMLEditor.Models
{
    public record FindReferenceParam(string line, string word);
    internal partial class MainModel
    {
       

        // Search-related command properties
        public DelegateCommand<string> GlobalSearchCommand { get; }
        public DelegateCommand<string> GotoDefinitionCommand { get; init; }
        public DelegateCommand<FindReferenceParam> FindAllReferencesCommand { get; init; }

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

        private void FindAllReferencesInvoked(FindReferenceParam findParams)
        {
            SelectedToolTab = 2;

            HashSet<GlobalFindResult> hs = new();

            FindReferenceResults.Clear();

            foreach (var item in DataTypeServices.FindAllReferences(Documents, findParams))
            {
                hs.Add(item);
               
            }
            foreach (var item in hs.OrderBy(z=>z.FileName).ThenBy(z=>z.LineNumber))
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
