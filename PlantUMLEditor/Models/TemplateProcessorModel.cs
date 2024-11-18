using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PlantUMLEditor.Models
{
    public class TemplateProcessorModel
    {
        public class Field : BindingBase
        {
            private string _value;

            public string Name
            {
                get; init;
            }

            public string Value
            {
                get => _value;
                set => SetValue(ref _value, value);
            }

        }

        public List<Field> Fields { get; init; } = new();
        public DelegateCommand<Window> ProcessCommand { get; }
        public string ProcessedContent { get; private set; }

        internal TemplateProcessorModel(TemplateItem template)
        {
            _template = template;
             var fields = ReadFields(template.Content);
            Fields.AddRange(fields);


            ProcessCommand = new DelegateCommand<Window>(ProcessCommandHandler);
             
        }

        private void ProcessCommandHandler(Window window)
        {
           ProcessedContent =  Regex.Replace(_template.Content, @"\{\{(?<name>[^\}]+)\}\}", 
                m => Fields.First(f => f.Name == m.Groups["name"].Value).Value);
            
           window.DialogResult = true;
            window.Close();
        }

        private readonly Regex _fieldsRegEx = new Regex(@"\{\{(?<name>[^\}]+)\}\}", RegexOptions.Compiled);
        private readonly TemplateItem _template;

        private List<Field> ReadFields(string content)
        {
           return _fieldsRegEx.Matches(content).Select(m => m.Groups["name"].Value).Distinct()
                .Select(name => new Field { Name = name }).ToList();
        }
    }
}
