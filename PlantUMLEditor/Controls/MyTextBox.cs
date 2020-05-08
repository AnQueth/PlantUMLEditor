using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace PlantUMLEditor.Controls
{
    public class MyTextBox : TextBox, INotifyPropertyChanged, ITextEditor
    {
        private static MyTextBox Me;

        private readonly Grid _find;
        private readonly TextBox _findText;
        private readonly TextBox _replaceText;
        private IAutoComplete _autoComplete;
        private ImageSource _lineNumbers;
        private Timer _syntaxDocument = null;
        private Timer _timer = null;
        private FixedDocument styledDocument = new FixedDocument();

        public static readonly DependencyProperty BindedDocumentProperty =
                                                       DependencyProperty.Register(
       nameof(BindedDocument), typeof(DocumentModel),
       typeof(MyTextBox), new PropertyMetadata(BindedDocumentPropertyChanged)
       );

        public MyTextBox()
        {
            //  DefaultStyleKey = typeof(MyTextBox);
            Me = this;

            _find = new Grid();

            _find.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            _find.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            _find.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            _find.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            _find.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            _findText = new TextBox();
            _find.Children.Add(_findText);
            Grid.SetRow(_findText, 0);
            _findText.Width = 200;

            _replaceText = new TextBox();
            _find.Children.Add(_replaceText);
            _replaceText.Width = 200;
            Grid.SetRow(_replaceText, 1);

            Button find = new Button();
            find.Content = "Find";
            _find.Children.Add(find);
            Grid.SetColumn(find, 1);

            Button replace = new Button();
            replace.Content = "Replace";
            _find.Children.Add(replace);
            Grid.SetColumn(replace, 1);
            Grid.SetRow(replace, 1);

            Button close = new Button();
            close.Content = "Close";
            _find.Children.Add(close);
            Grid.SetColumn(close, 1);
            Grid.SetRow(close, 2);

            close.Click += Close_Click;
            find.Click += Find_Click;
            replace.Click += Replace_Click;

            _find.Visibility = Visibility.Collapsed;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DocumentModel BindedDocument
        {
            get
            {
                return (DocumentModel)GetValue(BindedDocumentProperty);
            }
            set
            {
                SetValue(BindedDocumentProperty, value);
            }
        }

        public ImageSource LineNumbers
        {
            get
            {
                return _lineNumbers;
            }
            set
            {
                _lineNumbers = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LineNumbers)));
            }
        }

        public FixedDocument StyledDocument
        {
            get { return styledDocument; }
            set
            {
                styledDocument = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StyledDocument)));
            }
        }

        private static void BindedDocumentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = d as MyTextBox;
            var value = (DocumentModel)e.NewValue;

            if (value != null)
            {
                value.Binded(Me);
            }

            value = (DocumentModel)e.OldValue;
            if (value != null)
            {
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _find.Visibility = Visibility.Collapsed;
        }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            Regex r = new Regex(_findText.Text);
            var m = r.Matches(this.Text);

            Keyboard.Focus(this);

            foreach (Group item in m)
            {
                this.Select(item.Index, item.Length);
            }
        }

        private void ProcessAutoComplete(object state)
        {
            Dispatcher.Invoke(() =>
            {
                BindedDocument.TextChanged(this.Text);

                var rec = GetRectFromCharacterIndex(CaretIndex);

                var line = GetLineIndexFromCharacterIndex(CaretIndex);

                string text = GetLineText(line);

                int c = GetCharacterIndexFromLineIndex(line);
                int where = CaretIndex;
                string word = "";
                int typedLength = 0;
                if (text.Length != 0)
                {
                    Stack<char> chars = new Stack<char>();
                    for (var x = CaretIndex - c - 1; x >= 0; x--)
                    {
                        while (x > text.Length - 1 && x > 0)
                            x--;
                        if (text[x] == ' ')
                            break;
                        chars.Push(text[x]);
                    }

                    where = where - chars.Count;
                    typedLength = chars.Count;

                    while (chars.Count > 0)
                        word += chars.Pop();
                }

                BindedDocument.AutoComplete(new AutoCompleteParameters(rec, text, line, word, where, typedLength));
            });
        }

        private void RenderLineNumbers()
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (this.ActualHeight > 0)
                {
                    int lines = this.GetLastVisibleLineIndex();
                    var p = VisualTreeHelper.GetDpi(this).PixelsPerDip;

                    Typeface tf = new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch);
                    DrawingVisual dv = new DrawingVisual();
                    var context = dv.RenderOpen();
                    Point pt = new Point(0, 0);

                    context.PushTransform(new TranslateTransform(0, -this.VerticalOffset));

                    for (var x = 0; x <= lines; x++)
                    {
                        FormattedText ft = new FormattedText((x + 1).ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, this.FontSize, Brushes.Black,
                          p);
                        context.DrawText(ft, pt);
                        pt.Y += ft.Height;
                    }

                    context.Close();

                    RenderTargetBitmap rtb = new RenderTargetBitmap(25, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(dv);

                    this.LineNumbers = rtb;
                }
            }));
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            Regex r = new Regex(_findText.Text);
            this.Text = r.Replace(this.Text, (s) =>
             {
                 return _replaceText.Text;
             });
        }

        private void ShowFind()
        {
            _find.Visibility = Visibility.Visible;
        }

        private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            this.RenderLineNumbers();
        }

        private void SyntaxDocumentCreator(object state)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                SynatxFlowDocument syntaxFlowDocument = new SynatxFlowDocument();
                syntaxFlowDocument.SetText(this.Text);

                StyledDocument = syntaxFlowDocument.Document;
            }));
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            ((Grid)this.Parent).Children.Add(_find);
            Grid.SetColumn(_find, Grid.GetColumn(this));
            Grid.SetRow(_find, Grid.GetRow(this));
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            BindedDocument.CloseAutoComplete();
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                this.InsertText("    ");
                CaretIndex += 4;
            }
            if (e.KeyboardDevice.IsKeyDown(Key.F) && e.KeyboardDevice.IsKeyDown(Key.LeftCtrl))
            {
                ShowFind();
            }
            if (e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.K) && e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.LeftCtrl))
            {
                var c = ((TextBlock)((FixedPage)StyledDocument.Pages[0].Child).Children[0]);
                this.Text = new TextRange(c.ContentStart, c.ContentEnd).Text;
            }
            if (_autoComplete.IsVisible && (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter) &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                _autoComplete.SendEvent(e);
                e.Handled = true;
                return;
            }

            if (_autoComplete.IsVisible && e.Key == Key.Space)
            {
                this.CaretIndex = this.SelectionStart + this.SelectionLength;
                _autoComplete.CloseAutoComplete();
            }
            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
        {
            if (_autoComplete.IsVisible && (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter) &&
                (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                e.Handled = true;
                return;
            }

            BindedDocument.KeyPressed();
            if (this._timer == null)
            {
                _timer = new Timer(ProcessAutoComplete);
            }

            this._timer.Change(500, Timeout.Infinite);

            //if(this._syntaxDocument == null)
            //{
            //    _syntaxDocument = new Timer(SyntaxDocumentCreator);

            //}
            //this._syntaxDocument.Change(1000, Timeout.Infinite);

            if (e.Key == Key.Enter)
            {
                SyntaxDocumentCreator(null);
                this.RenderLineNumbers();
            }

            base.OnPreviewKeyUp(e);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            //  drawingContext.DrawRectangle(Brushes.Blue, new Pen(Brushes.Purple, 11), new Rect(0, 0, 100, 100));
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
        }

        public static T FindDescendant<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return default(T);
            int numberChildren = VisualTreeHelper.GetChildrenCount(obj);
            if (numberChildren == 0) return default(T);

            for (int i = 0; i < numberChildren; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T)
                {
                    return (T)(object)child;
                }
            }

            for (int i = 0; i < numberChildren; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                var potentialMatch = FindDescendant<T>(child);
                if (potentialMatch != default(T))
                {
                    return potentialMatch;
                }
            }

            return default(T);
        }

        public void GotoLine(int lineNumber)
        {
            if (lineNumber == 0)
                lineNumber = 1;

            Dispatcher.BeginInvoke((Action)(() =>
            {
                CaretIndex = this.GetCharacterIndexFromLineIndex(lineNumber - 1);
                this.ScrollToLine(lineNumber - 1);
                Keyboard.Focus(this);
            }));
        }

        public void InsertText(string text)
        {
            int c = CaretIndex;

            Text = Text.Insert(c, text);

            CaretIndex = c;
        }

        public void InsertTextAt(string text, int index, int typedLength)
        {
            this.SelectionStart = index;

            if (this.Text[index + typedLength] != char.MinValue)
            {
                while (char.IsLetterOrDigit(this.Text[index + typedLength]) && this.Text[index + typedLength] != '\r' && index + typedLength < Text.Length)
                {
                    typedLength++;
                }
            }

            if (typedLength > this.SelectionLength)
                this.SelectionLength = typedLength;

            if (!string.IsNullOrEmpty(this.SelectedText))
                this.SelectedText = "";
            this.SelectionStart = index;

            this.SelectedText = text;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var sv = FindDescendant<ScrollViewer>(this);
            if (sv != null)
            {
                sv.ScrollChanged += Sv_ScrollChanged;
            }
        }

        public void SetAutoComplete(IAutoComplete autoComplete)
        {
            _autoComplete = autoComplete;
        }

        public void TextClear()
        {
            this.Text = "";
        }

        public string TextRead()
        {
            return this.Text;
        }

        public void TextWrite(string text)
        {
            this.Text = text;
        }
    }
}