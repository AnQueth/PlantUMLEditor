using System.IO;
using System.Windows.Media.Imaging;

namespace PlantUMLEditor.Models
{
    internal class ImageDocumentModel : BaseDocumentModel
    {
        private readonly BitmapImage? _imageSource;

        public ImageDocumentModel(string fileName, string title) : base(fileName, title)
        {
            if (!File.Exists(FileName))
            {
                return;
            }

            MemoryStream ms = new MemoryStream();
            using (var fs = File.OpenRead(FileName))
            {
                fs.CopyTo(ms);
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
