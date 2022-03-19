using Prism.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlantUMLEditor.Models
{
    public class PreviewDiagramModel : BindingBase, IPreviewModel
    {
        private record QueueItem(string path, bool delete, string name);

        private readonly IIOService _ioService;
        private readonly ConcurrentQueue<QueueItem> _regenRequests
            = new();
        private readonly AutoResetEvent _are = new(false);
        private FixedDocument? _doc;
        private string _messages = string.Empty;
        private bool _running = false;
        private BitmapSource? image;
        private readonly string _jarLocation;
        private string _name;

        public PreviewDiagramModel(IIOService ioService, string jarLocation, string title)
        {
            _jarLocation = jarLocation;
            _name = title;
            _ioService = ioService;
            PrintImageCommand = new DelegateCommand(PrintImageHandler);
            CopyImage = new DelegateCommand(CopyImageHandler);
            SaveImageCommand = new DelegateCommand(SaveImageHandler);
            Width = 1024;
            Height = 1024;
            _running = true;
            Task.Run(Runner);
        }

        public DelegateCommand CopyImage
        {
            get;
        }

        public FixedDocument? Doc
        {
            get => _doc;
            set => SetValue(ref _doc, value);
        }

        public float Height
        {
            get; set;
        }

        public BitmapSource? Image
        {
            get => image;
            set => SetValue(ref image, value);
        }

        public string Messages
        {
            get => _messages;
            set => SetValue(ref _messages, value);
        }

        public DelegateCommand PrintImageCommand
        {
            get;
        }

        public DelegateCommand SaveImageCommand
        {
            get;
        }

        public string Title
        {
            get => _name;
            set => SetValue(ref _name, value);
        }

        public float Width
        {
            get;
            set;
        }

        private void CopyImageHandler()
        {
            if (Image != null)
            {
                Clipboard.SetImage(Image.Clone());
            }
        }

        private void PrintImage()
        {
            PrintDialog? pdialog = new PrintDialog();
            if (pdialog.ShowDialog() == true && Image != null)
            {
                int widthSlices = (int)Math.Ceiling(Image.PixelWidth / Width);
                int heightSliced = (int)Math.Ceiling(Image.PixelHeight / Height);

                List<DrawingVisual> images = new();

                SaveFrameworkElement(Image, widthSlices, heightSliced, (int)Width, (int)Height, images);

                FixedDocument fd = new();

                foreach (DrawingVisual? item in images)
                {
                    FixedPage? fp = new FixedPage();

                    RenderTargetBitmap rtb = new(1024, 1024, 96d, 96d, PixelFormats.Default);
                    rtb.Render(item);

                    fp.Children.Add(new System.Windows.Controls.Image() { Source = rtb });

                    PageContent? pc = new PageContent
                    {
                        Child = fp
                    };

                    fd.Pages.Add(pc);
                }
                Doc = fd;
                pdialog.PrintDocument(fd.DocumentPaginator, "");
            }
        }

        private void PrintImageHandler()
        {
            PrintImage();
        }

        private void Runner()
        {
            while (_running)
            {
                _are.WaitOne();

                try
                {
                    while (!_regenRequests.IsEmpty)
                    {
                        Messages = string.Empty;
                        _regenRequests.TryDequeue(out QueueItem? res);
                        if (res is null)
                        {
                            continue;
                        }

                        string? dir = Path.GetDirectoryName(res.path);
                        if (dir == null)
                        {
                            continue;
                        }

                        PlantUMLImageGenerator generator = new PlantUMLImageGenerator(_jarLocation, res.path, dir);

                        PlantUMLImageGenerator.UMLImageCreateRecord? createResult = generator.Create().Result;

                        string errors = createResult.errors;


                        if (!string.IsNullOrEmpty(errors))
                        {
                            Debug.WriteLine(errors);

                            Match? m = Regex.Match(errors, "Error line (\\d+)");
                            if (m.Success)
                            {
                                int d = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                                d++;
                                using StreamReader? g = File.OpenText(res.path);
                                int x = 0;
                                while (x <= d + 1)
                                {
                                    x++;
                                    string? ll = g.ReadLine();
                                    if (ll != null)
                                    {
                                        if (x > d - 3)
                                        {
                                            errors += "\r\n" + ll;
                                        }
                                    }
                                }
                            }
                            Messages = errors;
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                string fn = createResult.fileName;
                                if (!File.Exists(fn))
                                {
                                    string? dir = Path.GetDirectoryName(res.path);
                                    if (dir == null)
                                    {
                                        return;
                                    }

                                    string fn2 = Path.Combine(dir, Path.GetFileNameWithoutExtension(res.name) + ".png");

                                    if (File.Exists(fn2))
                                    {
                                        fn = fn2;
                                    }
                                }
                                Image = new BitmapImage(new Uri(fn));

                                if (res.delete && File.Exists(res.path))
                                {
                                    File.Delete(res.path);
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Messages = ex.ToString();
                }
            }
        }

        private static void SaveFrameworkElement(BitmapSource bitmapImage, int widthSteps, int heightSteps, int width, int height, List<DrawingVisual> images)
        {
            for (int startX = 0; startX < widthSteps; startX++)
            {
                for (int startY = 0; startY < heightSteps; startY++)
                {
                    SaveImage(bitmapImage, startX * width, startY * height, images);
                }
            }
        }

        private void SaveImageHandler()
        {
            string? fileName = _ioService.GetSaveFile("Png files | *.png", ".png");

            if (fileName == null || Image == null)
            {
                return;
            }

            PngBitmapEncoder encoder = new();

            encoder.Frames.Add(BitmapFrame.Create((BitmapImage?)Image));

            using FileStream? filestream = new FileStream(fileName, FileMode.Create);
            encoder.Save(filestream);
        }

        public void Stop()
        {
            _running = false;
            _are.Set();
        }

        public static void SaveImage(BitmapSource sourceImage,
                                      int startX,
                              int startY,

                              List<DrawingVisual> images)
        {
            TransformGroup transformGroup = new();
            TranslateTransform translateTransform = new()
            {
                X = -startX,
                Y = -startY
            };
            transformGroup.Children.Add(translateTransform);

            DrawingVisual vis = new();
            DrawingContext cont = vis.RenderOpen();
            cont.PushTransform(transformGroup);
            cont.DrawImage(sourceImage, new Rect(new System.Windows.Size(sourceImage.PixelWidth, sourceImage.PixelHeight)));
            cont.Close();

            images.Add(vis);
        }

        public void Show(string path, string name, bool delete)
        {
            if (!File.Exists(_jarLocation))
            {
                MessageBox.Show("plant uml is missing");
            }
            _regenRequests.Clear();
            _regenRequests.Enqueue(new QueueItem(path, delete, name));

            _are.Set();
        }
    }
}