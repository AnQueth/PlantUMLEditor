using Prism.Commands;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;


namespace PlantUMLEditor.Models
{
    internal class PromptViewModel : BindingBase
    {
        private PromptStorage _promptStorage;



        public PromptViewModel(PromptStorage promptStorage)
        {
            this._promptStorage = promptStorage;




          

            _selectedPrompt = Prompts.FirstOrDefault();

            SaveCommand = new DelegateCommand<Window>(SaveCommandHandler);
            DeleteCommand = new DelegateCommand(DeleteCommandHandler, () => _selectedPrompt is not null);
            AddCommand = new DelegateCommand(AddCommandHandler, () => !string.IsNullOrEmpty(_name));
            CancelCommand = new DelegateCommand<Window>(window => window.Close());
        }


        internal class PromptModel : BindingBase
        {
            private string _name = string.Empty;
            private string _content = string.Empty;

            public string Name
            {
                get => _name;
                set => SetValue(ref _name, value);
            }
            public string Content
            {
                get => _content;
                set => SetValue(ref _content, value);
            }
        }
        private ObservableCollection<PromptModel> _prompts = new ObservableCollection<PromptModel>();
        public ObservableCollection<PromptModel> Prompts
        {
            get
            {
                if (_prompts.Count == 0)
                {


                    _promptStorage.Prompts.Where(z => z.Name != PromptStorage.SystemPromptkey)
                        .OrderBy(t => t.Name)
                        .ToList()
                        .ForEach(t => _prompts.Add(new PromptModel
                        {
                            Name = t.Name,
                            Content = t.Content
                        }));

                }

                return _prompts;
            }

        }

        private PromptModel? _selectedPrompt;


        public PromptModel? SelectedPrompt
        {
            get => _selectedPrompt;
            set
            {
                SetValue(ref _selectedPrompt, value);
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
        public DelegateCommand<Window> SaveCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand<Window> CancelCommand { get; }




        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                SetValue(ref _name, value);
                AddCommand.RaiseCanExecuteChanged();
            }
        }
        private void AddCommandHandler()
        {


            Prompts.Add(new PromptModel
            {
                Name = Name,
                Content = ""
            });
        }

        private void DeleteCommandHandler()
        {
            if (SelectedPrompt != null)
            {
                Prompts.Remove(SelectedPrompt);
            }
        }

        private async void SaveCommandHandler(Window window)
        {
            _promptStorage.SetPrompts(Prompts.Select(t => new PromptItem
            {
                Name = t.Name,
                Content = t.Content
            }));
            await _promptStorage.Save(AppSettings.Default.Prompts);
            window.Close();
        }
    }
}