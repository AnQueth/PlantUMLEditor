using System;
using System.Windows;

namespace PlantUMLEditor.Models
{
    internal abstract class BaseDocumentModel : BindingBase
    {
        private bool _isDirty;
        private Visibility _visible;

        public Visibility Visible
        {
            get => _visible; set => SetValue(ref _visible, value);
        }
        protected BaseDocumentModel(string fileName, string title)
        {
            this.FileName = fileName;
            this.Title = title;
        }

        public string Title
        {
            get; init;
        }
        public string FileName
        {
            get; init;
        }

        public virtual void Close()
        {

        }

    

        protected bool DocGeneratorDirty
        {
            get; set;
        }

        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                SetValue(ref _isDirty, value);
                DocGeneratorDirty = true;
            }

        }
    }
}