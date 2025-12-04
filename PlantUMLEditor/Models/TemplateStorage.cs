using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PlantUMLEditor.Models
{
    internal class TemplateItem
    {
        public required string Name { get; set; }
        public required string Content { get; set; }
    }
    internal class TemplateStorage
    {
   
        public ObservableCollection<TemplateItem> Templates { get; set; } = new();

        public TemplateStorage() { }

        public void SetTemplates(IEnumerable<TemplateItem> templates)
        {
            Templates.Clear();
            foreach (var template in templates)
            {
                Templates.Add(template);
            }
         
        }

        public async Task Save(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach(var file in System.IO.Directory.GetFiles(path, "*.template"))
            {
                System.IO.File.Delete(file);
            }

            foreach (var template in Templates)
            {
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(path, $"{template.Name}.template"), template.Content);
            }
        }

        public async Task Load(string path)
        {
            if(!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            var loadedTemplates = new List<TemplateItem>();
            
            foreach (var file in System.IO.Directory.GetFiles(path, "*.template"))
            {
                var content = await System.IO.File.ReadAllTextAsync(file);
                loadedTemplates.Add(new TemplateItem
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(file),
                    Content = content
                });
            }
            
            // Modify ObservableCollection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var template in loadedTemplates)
                {
                    Templates.Add(template);
                }
            });
        }
    }
}
