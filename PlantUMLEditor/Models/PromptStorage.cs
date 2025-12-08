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
    internal class PromptItem
    {
        public required string Name { get; set; }
        public required string Content { get; set; }
    }

    internal class PromptStorage
    {
        public ObservableCollection<PromptItem> Prompts { get; set; } = new();

        public PromptStorage() { }

        public static readonly string SystemPromptkey = "system";

        public static readonly string SystemPrompt = @"You are a plant uml expert to help users create and edit plantuml.
        You have the ability read html content from current documents.
You are outputting text that will be viewed in a markdown viewer, markdig.
Use the available tools. Ensure you verify any edits. Use tools to read and edit the current diagram.
Ensure diagrams adhere to plantuml syntax. 
If you create a new diagram you do not need to repeat the diagram code back to the user.
examples:
* if user asks to show methods on a class or interface, you search the whole folder for the class or interface definition and read the methods.
* if user asks to add a relationship between two classes, you search the whole folder for the class definitions and add the relationship.
* if user asks to create a new component diagram, you create a new plantuml diagram with appropriate syntax and name it with a word with .component.puml
* if user asks to create a new sequence diagram, you create a new plantuml diagram with appropriate syntax and name it with a word with .seq.puml
* if user asks to create a new class diagram, you create a new plantuml diagram with appropriate syntax and name it with a word with .class.puml
* if user asks to create a new diagram, you create a new plantuml diagram with appropriate syntax and name it with a word with .puml
* if current document text is not uml, it is either md or text from an html document.
* when creating a new sequence diagram, not editing an existing one, add this comment after @startuml: ""'@@novalidate"". if this comment appears in a file, leave it!
* for sequence diagrams prefer the format: participant ""name of something"" as alias
* you should request plantuml syntax help after failing to resolve an issue fast by reading from using the html tool https://plantuml.com/
";

        public void SetPrompts(IEnumerable<PromptItem> prompts)
        {
            Prompts.Clear();
            foreach (var prompt in prompts)
            {
                Prompts.Add(prompt);
            }
        }

        public async Task Save(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (var file in System.IO.Directory.GetFiles(path, "*.prompt"))
            {
                System.IO.File.Delete(file);
            }

            foreach (var prompt in Prompts)
            {
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(path, $"{prompt.Name}.prompt"), prompt.Content);
            }

            Prompts.Clear();
            await Load(path);
        }

        public async Task Load(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var loadedPrompts = new List<PromptItem>();

            foreach (var file in System.IO.Directory.GetFiles(path, "*.prompt"))
            {
                var content = await System.IO.File.ReadAllTextAsync(file);
                loadedPrompts.Add(new PromptItem
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(file),
                    Content = content
                });
            }

            // Modify ObservableCollection on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {

                if(!Prompts.Any(z=>z.Name == SystemPromptkey))
                    Prompts.Add(new PromptItem { Name =SystemPromptkey, Content = SystemPrompt });

                foreach (var prompt in loadedPrompts)
                {
                    Prompts.Add(prompt);
                }
            });
        }
    }
}
