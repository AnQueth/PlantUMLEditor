using Prism.Commands;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace PlantUMLEditor.Models
{
    internal class CommitMessageViewModel : BindingBase
    {
        private string _commitMessage = string.Empty;

        public string CommitMessage
        {
            get => _commitMessage;
            set
            {
                SetValue(ref _commitMessage, value);
                CommitCommand.RaiseCanExecuteChanged();
            }
        }

        public ObservableCollection<string> ChangedFiles { get; } = new();

        public DelegateCommand<Window> CommitCommand { get; }

        public bool DialogResult { get; private set; }

        public CommitMessageViewModel(IEnumerable<string> changedFiles)
        {
            foreach (var file in changedFiles)
            {
                ChangedFiles.Add(file);
            }

            CommitCommand = new DelegateCommand<Window>(
                CommitHandler,
                _ => !string.IsNullOrWhiteSpace(CommitMessage));
        }

        private void CommitHandler(Window window)
        {
            DialogResult = true;
            window.DialogResult = true;
            window.Close();
        }
    }
}
