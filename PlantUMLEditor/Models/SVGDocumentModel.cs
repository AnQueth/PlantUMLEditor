using System.IO;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    internal class SVGDocumentModel : BaseDocumentModel
    {
        private string _svg;

        public string SVG 
        {
            get => _svg;
              set => SetValue(ref _svg, value);
        }

        public SVGDocumentModel(string fileName, string title) : base(fileName, title)
        {
            _svg = fileName;
        
           



        }

        public async Task Init()
        {

           
        }




    }
}
