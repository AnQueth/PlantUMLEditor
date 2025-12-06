using PlantUMLEditor.Models.Runners;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
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

        // Replaced ConcurrentQueue + AutoResetEvent with Channel<T>
        private readonly Channel<QueueItem> _channel;
        private readonly CancellationTokenSource _cts = new();
        private Task? _consumerTask;

        private FixedDocument? _doc;
        private string _messages = string.Empty;

        private readonly string _jarLocation;
        private string _name;

        private string _svg = string.Empty;
        private QueueItem? _lastProcessed;
        private byte[]? _lastImageData;
        private bool _useSVG = true;

        public bool UseSVG { get => _useSVG; set => SetValue(ref _useSVG, value); }

        private bool _isBusy = true;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetValue(ref _isBusy, value);
        }

        public string SVG
        {
            get => _svg;
            set => SetValue(ref _svg, value);
        }


        public PreviewDiagramModel(IIOService ioService, string title)
        {
            _jarLocation = AppSettings.Default.JARLocation;
            _name = title;
            _ioService = ioService;
            PrintImageCommand = new DelegateCommand(PrintImageHandler);
            CopyImage = new DelegateCommand(CopyImageHandler);
            SaveImageCommand = new DelegateCommand(SaveImageHandler);
            Width = 1024;
            Height = 1024;

            var options = new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropOldest
            };

            _channel = Channel.CreateBounded<QueueItem>(options);

            _consumerTask = Task.Run(() => Runner(_cts.Token));
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

        private async void CopyImageHandler()
        {
            if (UseSVG)
            {
                var data = await File.ReadAllTextAsync(SVG);
                Clipboard.SetText(data);
                return;
            }

            await RunInBusy(async () =>
            {
                var img = await GenerateImage();

                if (img != null)
                {
                    int tries = 0;
                    while (tries < 5)
                    {
                        try
                        {
                            Clipboard.SetImage(img.Clone());
                            return;
                        }
                        catch (Exception)
                        {
                            await Task.Delay(100);
                        }
                        tries++;

                    }
                }

            });


        }

        private async Task PrintImage()
        {
            await RunInBusy(async () =>
            {

                PrintDialog? pdialog = new PrintDialog();

                var img = await GenerateImage();

                if (pdialog.ShowDialog() == true && img != null)
                {
                    int widthSlices = (int)Math.Ceiling(img.PixelWidth / Width);
                    int heightSliced = (int)Math.Ceiling(img.PixelHeight / Height);

                    List<DrawingVisual> images = new();

                    SaveFrameworkElement(img, widthSlices, heightSliced, (int)Width, (int)Height, images);

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
            });
        }

        private async void PrintImageHandler()
        {
            await PrintImage();
        }

        private async Task RunInBusy(Func<Task> func)
        {
            IsBusy = true;
            try
            {
                await func();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<BitmapSource?> GenerateImage()
        {


            var res = _lastProcessed;

            if (res is null || _lastImageData is null)
            {
                return null;
            }

            string? dir = Path.GetDirectoryName(res.path);
            if (dir == null)
            {
                return null;
            }

            var tmp = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tmp, _lastImageData);
            try
            {

                PlantUMLImageGenerator generator = new PlantUMLImageGenerator(_jarLocation, tmp, dir, false);

                PlantUMLImageGenerator.UMLImageCreateRecord? createResult = await generator.Create();

                string errors = createResult.errors;


                if (string.IsNullOrEmpty(errors))
                {

                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        string fn = createResult.fileName;
                        if (!File.Exists(fn))
                        {
                            string? dir = Path.GetDirectoryName(res.path);
                            if (dir == null)
                            {
                                return null;
                            }

                            string fn2 = Path.Combine(dir, Path.GetFileNameWithoutExtension(res.name) + FileExtension.PNG);

                            if (File.Exists(fn2))
                            {
                                fn = fn2;
                            }
                        }


                        if (res.delete && File.Exists(res.path))
                        {
                            File.Delete(res.path);
                        }

                        return new BitmapImage(new Uri(fn));

                    });
                }
                return null;
            }
            finally
            {
                if (File.Exists(tmp))
                {
                    File.Delete(tmp);
                }

            }
        }

        private async Task Runner(CancellationToken token)
        {
            try
            {
                await foreach (var res in _channel.Reader.ReadAllAsync(token))
                {
                    Messages = string.Empty;

                    string? dir = Path.GetDirectoryName(res.path);
                    if (dir == null)
                    {
                        continue;
                    }

                    await RunInBusy(async () =>
                    {

                        _lastProcessed = res;
                        _lastImageData = await File.ReadAllBytesAsync(res.path, token);

                        PlantUMLImageGenerator generator = new PlantUMLImageGenerator(_jarLocation, res.path, dir, true);

                        PlantUMLImageGenerator.UMLImageCreateRecord? createResult = await generator.Create();

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

                                    string fn2 = Path.Combine(dir, Path.GetFileNameWithoutExtension(res.name) + FileExtension.SVG);

                                    if (File.Exists(fn2))
                                    {
                                        fn = fn2;
                                    }
                                }

                                SVG = fn;




                                if (res.delete && File.Exists(res.path))
                                {
                                    File.Delete(res.path);
                                }
                            });
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // graceful cancellation
            }
            catch (Exception ex)
            {
                Messages = ex.ToString();
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

        private async void SaveImageHandler()
        {
            if (UseSVG)
            {
                SaveSVG();
                return;
            }

            await RunInBusy(async () =>
            {
                await SavePNG();


            });


        }

        private async Task SavePNG()
        {
            var img = await GenerateImage();
            string? fileName = _ioService.GetSaveFile("Png files | *.png", FileExtension.PNG);

            if (fileName == null || img == null)
            {
                return;
            }

            PngBitmapEncoder encoder = new();

            encoder.Frames.Add(BitmapFrame.Create((BitmapImage?)img));

            using FileStream? filestream = new FileStream(fileName, FileMode.Create);
            encoder.Save(filestream);

        }

        private void SaveSVG()
        {
            string? fileName = _ioService.GetSaveFile("Svg files | *.svg", FileExtension.SVG);
            if (fileName == null || string.IsNullOrEmpty(SVG))
            {
                return;
            }
            File.Copy(SVG, fileName, true);
            return;
        }

        public void Stop()
        {
            // Signal cancellation and complete the writer so the consumer exits.
            _cts.Cancel();
            _channel.Writer.TryComplete();
        }

        private static void SaveImage(BitmapSource sourceImage,
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

            var item = new QueueItem(path, delete, name);

            // Try synchronous write first; if that fails, write asynchronously.
            if (!_channel.Writer.TryWrite(item))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _channel.Writer.WriteAsync(item, _cts.Token);
                    }
                    catch (OperationCanceledException) { }
                });
            }
        }
    }
}