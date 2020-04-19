using Prism.Commands;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlantUMLEditor.Models
{
    public class DocumentModel : BindingBase
    {
        private string name;
        private string content;

        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                SetValue(ref name, value);
            }
        }

        public DocumentModel()
        {
             
        }


        private string _lastTypedWork;

        public virtual void AutoComplete(object sender, System.Windows.Input.KeyEventArgs e)
        {
   
            
        }

        protected virtual void ContentChanged(ref string text)
        {

        }

        public DocumentTypes DocumentType
        {
            get; set;
        }

        public string Content
        {
            get { return content; }
            set { SetValue(ref content, value);

                ContentChanged(ref content);

             

            }
        }

        public string FileName
        {
            get; set;
        }
    }

    public enum DocumentTypes
    {
        Class,
        Sequence
    }
}