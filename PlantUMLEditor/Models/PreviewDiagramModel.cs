using Newtonsoft.Json.Linq;
using Prism.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlantUMLEditor.Models
{
    public class PreviewDiagramModel : BindingBase
    {
        private BitmapSource image;
        private string title;
        private bool _running = false;

        public string Title
        {
            get { return title; }
            set { SetValue(ref title, value); }
        }

        public BitmapSource Image
        {
            get { return image; }
            set { SetValue(ref image, value); }
        }

        public DelegateCommand PrintImageCommand { get; }

        public PreviewDiagramModel()
        {
            PrintImageCommand = new DelegateCommand(PrintImageHandler);
            Width = 1024;
            Height = 1024;
            _running = true;
            Task.Run(Runner);

        }

        private void PrintImageHandler()
        {
            PrintImage();
        }

        private void SaveFrameworkElement(BitmapSource bitmapImage, int widthSteps, int heightSteps, int width, int height, List<DrawingVisual> images)
        {

            for (int startX = 0; startX < widthSteps; startX++)
            {
                for (int startY = 0; startY < heightSteps; startY++)
                {
                    SaveImage(bitmapImage, startX * width, startY * height, width, height, images);
                }
            }
        }


        public void SaveImage(BitmapSource sourceImage,
                              int startX,
                              int startY,
                              int width,
                              int height,
                              List<DrawingVisual> images)
        {
            TransformGroup transformGroup = new TransformGroup();
            TranslateTransform translateTransform = new TranslateTransform();
            translateTransform.X = -startX;
            translateTransform.Y = -startY;
            transformGroup.Children.Add(translateTransform);

            DrawingVisual vis = new DrawingVisual();
            DrawingContext cont = vis.RenderOpen();
            cont.PushTransform(transformGroup);
            cont.DrawImage(sourceImage, new Rect(new System.Windows.Size(sourceImage.PixelWidth, sourceImage.PixelHeight)));
            cont.Close();




            images.Add(vis);


        }

        internal void Stop()
        {
            _running = false;
            _are.Set();
        }

        public float Width
        {
            get;
            set;
        }

        public float Height
        {
            get; set;
        }

        private void PrintImage()
        {
            var pdialog = new PrintDialog();
            if (pdialog.ShowDialog() == true)
            {
                int widthSlices = (int)Math.Ceiling(image.PixelWidth / Width);
                int heightSliced = (int)Math.Ceiling(image.PixelHeight / Height);

                List<DrawingVisual> images = new List<DrawingVisual>();

                SaveFrameworkElement(image, widthSlices, heightSliced, (int)Width, (int)Height, images);


                FixedDocument fd = new FixedDocument();


                foreach (var item in images)
                {


                    var fp = new FixedPage();

                    RenderTargetBitmap rtb = new RenderTargetBitmap(1024, 1024, 96d, 96d, PixelFormats.Default);
                    rtb.Render(item);



                    fp.Children.Add(new System.Windows.Controls.Image() { Source = rtb });

                    var pc = new PageContent
                    {
                        Child = fp
                    };

                    fd.Pages.Add(pc);

                }
                doc = fd;
                pdialog.PrintDocument(fd.DocumentPaginator, "");
            }

        }

        FixedDocument _doc;

        public FixedDocument doc
        {
            get
            {
                return _doc;
            }
            set
            {
                SetValue(ref _doc, value);
            }
        }


        private readonly ConcurrentQueue<(string, string, bool)> _regenRequests = new  ConcurrentQueue<(string, string, bool)>();

        private AutoResetEvent _are = new AutoResetEvent(false);
        private void Runner()
        {

            while (_running)
            {
                _are.WaitOne();

                while (_regenRequests.Count > 0)
                {
                    _regenRequests.TryDequeue(out (string, string, bool) res);



                    Process p = new Process();

                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.FileName = "java.exe";
                    p.StartInfo.Arguments = $"-jar {res.Item1} \"{res.Item2}\"";
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;

                    p.Start();
                    p.WaitForExit();

                    string l = p.StandardOutput.ReadToEnd();
                    string e = p.StandardError.ReadToEnd();



                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Image = new BitmapImage(new Uri(Path.Combine(Path.GetDirectoryName(res.Item2), Path.GetFileNameWithoutExtension(res.Item2) + ".png")));

                        if (res.Item3 &&  File.Exists(res.Item2))
                            File.Delete(res.Item2);
                    });

                }
            }
        }

        public async Task ShowImage(string jar, string path, bool delete)
        {
            _regenRequests.Clear();
            _regenRequests.Enqueue((jar, path, delete));

            _are.Set();



        }
    }
}
