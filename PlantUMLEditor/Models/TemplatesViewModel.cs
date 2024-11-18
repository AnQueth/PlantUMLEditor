using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace PlantUMLEditor.Models
{
    internal class TemplatesViewModel : BindingBase
    {



        internal class TemplateModel : BindingBase
        {
            private string _name;
            private string _content;

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
        public ObservableCollection<TemplateModel> Templates
        {
            get;
            set;
        } = new();

        private TemplateModel? _selectedTemplate;
        private readonly TemplateStorage _templateStorage;

        public TemplateModel? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                SetValue(ref _selectedTemplate, value);
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
        public DelegateCommand<Window> SaveCommand { get; }
        public DelegateCommand DeleteCommand { get; }
        public DelegateCommand AddCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public TemplatesViewModel(TemplateStorage templateStorage)
        {

            _templateStorage = templateStorage;
      
   
            foreach (var template in templateStorage.Templates)
            {
                Templates.Add(new TemplateModel
                {
                    Name = template.Name,
                    Content = template.Content
                });
            }

            _selectedTemplate = Templates.FirstOrDefault();

            SaveCommand = new DelegateCommand<Window>(SaveCommandHandler);
            DeleteCommand = new DelegateCommand(DeleteCommandHandler, ()=> _selectedTemplate is not null);
            AddCommand = new DelegateCommand(AddCommandHandler, ()=> !string.IsNullOrEmpty(_name));
 
        }


        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                SetValue(ref _name, value);
                AddCommand.RaiseCanExecuteChanged();
            }
        }
        private void AddCommandHandler( )
        {
         

            Templates.Add(new TemplateModel
            {
                Name = Name,
                Content = ""
            });
        }

        private void DeleteCommandHandler()
        {
            Templates.Remove(SelectedTemplate);
        }

        private async void SaveCommandHandler(Window window)
        {
            _templateStorage.SetTemplates(Templates.Select(t => new TemplateItem
            {
                Name = t.Name,
                Content = t.Content
            }));
            await _templateStorage.Save(AppSettings.Default.TemplatePath);
            window.Close();
        }


    }
}
