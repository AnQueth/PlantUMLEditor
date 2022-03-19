using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace PlantUMLEditor.Models
{
    internal class ImageDocumentModel : BaseDocumentModel
    {
        private BitmapImage? _imageSource;

        public ImageDocumentModel(string fileName, string title) : base(fileName, title)
        {
            if (!File.Exists(FileName))
            {
                return;
            }



        }

        public async Task Init()
        {
            MemoryStream ms = new MemoryStream();
            using (FileStream? fs = File.OpenRead(FileName))
            {
                await fs.CopyToAsync(ms);
            }
            _imageSource = new BitmapImage();
            _imageSource.BeginInit();
            _imageSource.StreamSource = ms;
            _imageSource.EndInit();
        }

        public BitmapImage? Image => _imageSource;

        public override void Close()
        {
            if (Image != null)
            {

                Image.StreamSource.Dispose();
                Image.StreamSource = null;
            }
            base.Close();
        }
    }
}
