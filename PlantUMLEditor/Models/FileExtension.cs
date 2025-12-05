using System;

namespace PlantUMLEditor.Models
{
    internal class FileExtension
    {
        public static readonly FileExtension PNG = new FileExtension(".png");
        public static readonly FileExtension JPG = new FileExtension(".jpg");
        public static readonly FileExtension PUML = new FileExtension(".puml");
        public static readonly FileExtension MD = new FileExtension(".md");
        public static readonly FileExtension YML = new FileExtension(".yml");
        public static readonly FileExtension URLLINK = new FileExtension(".url");
        public static readonly FileExtension SVG = new FileExtension(".svg");


        public FileExtension(string extension)
        {
            Extension = extension;
        }

        public string Extension
        {
            get;
        }

        public static implicit operator string(FileExtension extension)
        {
            return extension.Extension;
        }
        public static implicit operator FileExtension(string extension)
        {
            return new FileExtension(extension);
        }

        public bool Compare(FileExtension extension)
        {
            return Extension.Equals(extension.Extension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
