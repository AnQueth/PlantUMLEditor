using System.Windows;

namespace PlantUMLEditor.Models
{
    internal partial class MainModel 
    {
            private bool _templatesEnabled = false;
            private TemplateItem? _selectedTemplate;

            public bool TemplatesEnabled
            {
                get => _templatesEnabled;
                set => SetValue(ref _templatesEnabled, value);
            }

            public TemplateItem? SelectedTemplate
            {
                get => _selectedTemplate;
                set
                {
                    SetValue(ref _selectedTemplate, value);
                    ApplyTemplateCommand.RaiseCanExecuteChanged();
                }
            }

    
        private void ApplyTemplateCommandHandler()
        {
            if (CurrentDocument is TextDocumentModel tdm && SelectedTemplate is not null)
            {
                TemplateProcessorModel tpm = new TemplateProcessorModel(SelectedTemplate);
                TemplateProcessorWindow tpw = new(tpm);
                if (tpw.ShowDialog().GetValueOrDefault())
                {
                    tdm.InsertAtCursor(tpm.ProcessedContent);
                }
            }
        }

        private void EditTemplatesCommandHandler()
        {
            var win = new TemplateEditorWindow();
            TemplatesViewModel vm = new(_templateStorage);
            win.DataContext = vm;
            win.ShowDialog();
        }
    }
}
