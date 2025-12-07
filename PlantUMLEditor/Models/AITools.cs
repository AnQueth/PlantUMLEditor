using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlantUML;
using PlantUMLEditor.Models.Runners;
using PlantUMLEditorAI;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;

using System.Xml.Linq;
using UMLModels;
using Xceed.Wpf.AvalonDock.Converters;

namespace PlantUMLEditor.Models
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class AIToolModifyAttribute() : Attribute
    {

    }

    internal class AIToolsBasic(Func<string, Task> _openDocument, string _folderBase)
    {
        protected string FolderBase => _folderBase;
        [AIToolModify]
        [Description("returns the contents of a url as text.")]
        public async Task<string> FetchUrlContent([Description("the url to fetch")] string url)
        {
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                try
                {
                    return await httpClient.GetStringAsync(url);
                }
                catch (Exception ex)
                {
                    return $"Error fetching URL content: {ex.Message}";
                }
            }
        }


        [Description("search for a term in all documents")]
        public async Task<List<GlobalFindResult>> SearchInAllDocuments([Description("the text to search for. it can be a word or regex.")] string text)
        {

            string WILDCARD = "*";
            List<GlobalFindResult>? findresults = await GlobalSearch.Find(text, new string[]
            {WILDCARD + FileExtension.PUML.Extension, WILDCARD + FileExtension.MD.Extension, WILDCARD + FileExtension.YML.Extension
            });




            return findresults;

        }

        [AIToolModify]
        [Description(@"creates a new document in the current workspace.
             if creating a class diagram use extension .class.puml. 
            if making a component diagram use .component.puml.
            if making a sequence diagram use .seq.puml.")]
        public async Task<string> CreateNewDocument(
            [Description("the relative directory path to the current file to store the file in")] string path,
            [Description("the name of the new document")] string name,
            [Description("the file extension of the new document, .md, .puml, .yml, .seq.puml, .component.puml, .class.puml")] string extension,
            [Description("the initial content of the document")] string content)
        {
            string fullPath = CheckPathAccess(path);


            string pathToFile = System.IO.Path.Combine(fullPath, name + extension);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            if (File.Exists(pathToFile))
                return @"file already exists {pathToFile}";

            await File.WriteAllTextAsync(pathToFile, content);




            await _openDocument(pathToFile);

            return "Created, make sure to verify if its plant uml";

        }

        [Description("read a file by a path")]
        public async Task<string> ReadFileByPath([Description("the full path to the file")] string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is null or empty", nameof(path));

            string fullPath = CheckPathAccess(path);
            if (!File.Exists(fullPath))
                return $"File not found: {fullPath}";
            return await File.ReadAllTextAsync(fullPath);
        }

        protected string CheckPathAccess(string path)
        {
            if (string.IsNullOrEmpty(FolderBase) || !Directory.Exists(FolderBase))
                throw new InvalidOperationException("Root directory is not set or does not exist.");

            string root = System.IO.Path.GetFullPath(FolderBase);


            string fullPath;
            if (System.IO.Path.IsPathRooted(path))
            {
                fullPath = System.IO.Path.GetFullPath(path);
            }
            else
            {
                // resolve relative path against the root directory
                fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path));
            }

            // Normalize for comparison
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Access to files outside the workspace root is not allowed.");
            }

            return fullPath;
        }


    }


    internal class AIToolsReadable(ITextGetter textGetter, Func<string, Task> openDocument, string folderBase)
            : AIToolsBasic(openDocument, folderBase)
    {


        [Description("reads the current text in the document.")]
        public async Task<string> ReadDocumentText()
        {
            if (textGetter != null)
            {
                return await textGetter.ReadContent();
            }

            return string.Empty;
        }

    }




    internal class AIToolsScriptable(IScriptable _currentTdm,
        Func<string, Task> _openDocument, string _folderBase) : AIToolsReadable(_currentTdm,
         _openDocument, _folderBase)
    {
        [AIToolModify]
        [Description(@"executes javascript in the context of the document and returns the result as string")]
        public async Task<string> ExecuteScript([Description("the javascript to execute")] string script)
        {
            if (_currentTdm != null)
            {
                return await _currentTdm.ExecuteScript(script);
            }
            return string.Empty;
        }



    }
    internal class AIToolsEditable(TextDocumentModel _currentTdm, ChatMessage _currentMessage,
     Func<string, Task> _openDocument, string _folderBase) :
    AIToolsReadable(_currentTdm, _openDocument, _folderBase)
    {

        [Description("verifies a puml file is valid and can be rendered. returns error in the document.")]
        public async Task<string > VerifyUMLFile([Description("path to the file to verify or for the current document, current_document")] string filePath)
        {

            this.CheckPathAccess(filePath);


            string? dir = Path.GetDirectoryName(Path.GetTempPath());
            if(dir is null)
            {
                return "Could not get temp directory";
            }
            if (filePath == "current_document" && _currentTdm != null)
            {
                string tempFile = Path.Combine(dir, Guid.NewGuid().ToString() + ".svg");
                await File.WriteAllTextAsync(tempFile, _currentTdm.Content);
                filePath = tempFile;
            }
            if (!File.Exists(filePath))
            {
                var found = Directory.GetFiles(FolderBase, Path.GetFileName(filePath),
                    SearchOption.AllDirectories).FirstOrDefault();
                if (found is null)
                    return $"File not found: {filePath}";

                filePath = found;

            }

            string tmpfn = Path.GetTempFileName();
            File.Copy(filePath, tmpfn, true);

            PlantUMLImageGenerator plantUMLImageGenerator = new(AppSettings.Default.JARLocation,
                tmpfn, dir, true);
            var result = await plantUMLImageGenerator.Create();
   
            if (File.Exists(tmpfn))
                File.Delete(tmpfn);

            var svgContent = result.errors == string.Empty && File.Exists(result.fileName) ?
                await File.ReadAllTextAsync(result.fileName) : null;

            if (File.Exists(result.fileName))
                File.Delete(result.fileName);
            return result.errors;
        }

        [AIToolModify]
        [Description("Replaces all occurrences of 'text' with 'newText' in the document.")]
        public string ReplaceText(
            [Description("the file path to replace text in, leave empty for current document")] string filePath,
            [Description("the text to find")] string text,
            [Description("the new text")] string newText)
        {
            if (_currentTdm != null && string.IsNullOrEmpty(filePath))
            {
                var original = _currentTdm.Content;
                var found = original.Contains(text);
                if(!found)
                {
                    return "Text to replace not found in document.";
                }

                var t = original.Replace(text, newText);

                found = t.Contains(newText);
                if(!found)
                    {
                    return "Replacement failed.";
                }

                _currentTdm.Content = t;

                _currentMessage.Undos.Add(new UndoOperation(UndoTypes.Replace, _currentTdm.FileName, original, t));
            }
            else
            {
                filePath = GetFullPath(filePath);

                CheckPathAccess(filePath);

                if (File.Exists(filePath))
                {
                    var original = File.ReadAllText(filePath);
                    var found = original.Contains(text);
                    if (!found)
                    {
                        return "Text to replace not found in document.";
                    }

                    var t = original.Replace(text, newText);

                    found = t.Contains(newText);
                    if (!found)
                    {
                        return "Replacement failed.";
                    }
                   
                    File.WriteAllText(filePath, t);


                }
                else
                {
                    return $"File not found: {filePath}";
                }

            }

            return "Replacement successful.";

        }

        private string GetFullPath(string filePath)
        {
             if(File.Exists(filePath))
                return filePath;
            var found = Directory.GetFiles(FolderBase, Path.GetFileName(filePath), SearchOption.AllDirectories).FirstOrDefault();
            if (found is null)
                throw new FileNotFoundException("File not found", filePath);

            return found;
        }

        [AIToolModify]
        [Description("Inserts the specified text at the given position of the current document only")]
        public void InsertTextAtPosition([Description("position in the original text to insert at")] int position, [Description("the text to insert")] string text)
        {
            if (_currentTdm != null)
            {
                var original = _currentTdm.Content;
                _currentTdm.InsertTextAt(text, position, text.Length);
                var t = _currentTdm.Content;
                _currentMessage.Undos.Add(new UndoOperation(UndoTypes.Positional, _currentTdm.FileName, original, t));

            }
        }
        [AIToolModify]
        [Description("rewrite the complete document")]
        public string RewriteDocument(
            [Description("the file path to rewrite if not the current document")] string filePath,
            [Description("the new text for the document")] string text)
        {
            if (_currentTdm != null && string.IsNullOrEmpty(filePath))
            {
                var original = _currentTdm.Content;
                _currentMessage.Undos.Add(new UndoOperation(UndoTypes.ReplaceAll, _currentTdm.FileName, original, text));
                _currentTdm.Content = text;

                return "Rewrote";
            }
            filePath = GetFullPath(filePath);
            CheckPathAccess(filePath);

            if (File.Exists(filePath))
            {
                File.WriteAllText(filePath, text);

                return "Rewrote";
            }

            return "File Not Found";

        }

    }
}