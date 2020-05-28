using PlantUMLEditor.Models;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

using System.Globalization;
using System.IO;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace PlantUMLEditor.Controls
{
    public class MyTextBox : TextBox, INotifyPropertyChanged, ITextEditor, IAutoComplete
    {
        private readonly List<(int line, int character)> _errors = new List<(int line, int character)>();

        private IAutoComplete _autoComplete;

        private DocumentModel _bindedDocument;
        private (int selectionStart, int match) _braces;
        private ListBox _cb;
        private IAutoCompleteCallback _currentCallback = null;
        private List<(int start, int length)> _found = new List<(int start, int length)>();
        private ImageSource _lineNumbers;

        private FindResult _selectedFindResult = null;
        private Timer _selectionHandler;
        private Timer _timer = null;

        private bool findReplaceVisible;
        private ObservableCollection<FindResult> findResults = new ObservableCollection<FindResult>();
        private string findText;
        private string replaceText;

        public static DependencyProperty PopupControlProperty = DependencyProperty.Register("PopupControl", typeof(Popup), typeof(MyTextBox));

        public MyTextBox()
        {
            DataContextChanged += MyTextBox_DataContextChanged;

            DefaultStyleKey = typeof(MyTextBox);

            this.Foreground = Brushes.Transparent;
            this.Background = Brushes.Transparent;
            FindCommand = new DelegateCommand(FindHandler);
            ReplaceCommand = new DelegateCommand(ReplaceHandler);
            ClearCommand = new DelegateCommand(ClearHandler);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public DelegateCommand ClearCommand { get; }

        public DelegateCommand FindCommand { get; }

        public bool FindReplaceVisible
        {
            get => findReplaceVisible;
            set
            {
                findReplaceVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FindReplaceVisible)));
            }
        }

        public ObservableCollection<FindResult> FindResults { get => findResults; set => findResults = value; }

        public string FindText
        {
            get => findText;

            set
            {
                findText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FindText)));
            }
        }

        public bool IsPopupVisible => PopupControl != null ? PopupControl.IsOpen : false;

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

        public Popup PopupControl
        {
            get { return (Popup)GetValue(PopupControlProperty); }

            set { SetValue(PopupControlProperty, value); }
        }

        public DelegateCommand ReplaceCommand { get; }

        public string ReplaceText
        {
            get => replaceText;

            set
            {
                replaceText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReplaceText)));
            }
        }

        public FindResult SelectedFindResult
        {
            get
            {
                return _selectedFindResult;
            }
            set
            {
                _selectedFindResult = value;
                if (value != null)
                    GotoLine(value.LineNumber);
            }
        }

        private static T FindDescendant<T>(DependencyObject obj) where T : DependencyObject
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

        private void AutoCompleteItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string currentSelected = e.AddedItems[0] as string;

                _currentCallback.Selection(currentSelected);
            }
        }

        private void ClearHandler()
        {
            FindResults.Clear();
            FindText = "";
            ReplaceText = "";
            lock (_found)
                _found.Clear();

            this.InvalidateVisual();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            FindReplaceVisible = false;
            lock (_found)
                _found.Clear();
            this.InvalidateVisual();
        }

        private void FindHandler()
        {
            RunFind(FindText, true);
        }

        private void FindMatchingBackwards(string text, int selectionStart, char inc, char matchChar)
        {
            int ends = 1;
            int match = 0;
            for (var c = selectionStart - 1; ends != 0 && c >= 0; c--)
            {
                if (text[c] == inc)
                {
                    ends++;
                }
                if (text[c] == matchChar)
                {
                    ends--;
                }

                match = c;
            }

            _braces = (selectionStart, match);

            this.InvalidateVisual();
        }

        private void FindMatchingForward(string text, int selectionStart, char inc, char matchChar)
        {
            int ends = 1;
            int match = 0;
            for (var c = selectionStart + 1; ends != 0 && c < text.Length - 1; c++)
            {
                if (text[c] == inc)
                {
                    ends++;
                }
                if (text[c] == matchChar)
                {
                    ends--;
                }

                match = c;
            }

            _braces = (selectionStart, match);

            this.InvalidateVisual();
        }

        private void MyTextBox_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _bindedDocument = e.NewValue as DocumentModel;

            _bindedDocument?.Binded(this);
        }

        private void ProcessAutoComplete(object state)
        {
            int k = (int)(Key)state;

            if (k != 18 && (k < 34 || k > 69))
                return;

            Dispatcher.Invoke(() =>
            {
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
                        if (text[x] == ' ' || text[x] == '<' || text[x] == '>' || text[x] == '(' || text[x] == ')')
                            break;
                        chars.Push(text[x]);
                    }

                    where = where - chars.Count;
                    typedLength = chars.Count;

                    while (chars.Count > 0)
                        word += chars.Pop();
                }

                _bindedDocument.AutoComplete(new AutoCompleteParameters(rec, text, line, word, where, typedLength, (Key)state));
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

        private void ReplaceHandler()
        {
            if (string.IsNullOrEmpty(FindText))
                return;

            Regex r = new Regex(FindText);
            this.Text = r.Replace(this.Text, ReplaceText);
            lock (_found)
                _found.Clear();

            this.InvalidateVisual();
        }

        private void RunFind(string text, bool invalidate)
        {
            lock (_found)
                _found.Clear();
            if (_autoComplete.IsPopupVisible)
                return;

            string t = this.Text;

            try
            {
                FindResults.Clear();

                if (!text.StartsWith("("))
                    text = "(" + text;
                if (!text.EndsWith(")"))
                    text = text + ")";

                Regex r = new Regex(text);
                var m = r.Matches(t);

                foreach (Group item in m)
                {
                    lock (_found)
                        _found.Add((item.Index, item.Length));
                    int l = GetLineIndexFromCharacterIndex(item.Index);

                    string line = GetLineText(l);
                    string reps = "";
                    if (!string.IsNullOrEmpty(ReplaceText))
                    {
                        reps = r.Replace(line, ReplaceText).Trim();
                    }

                    FindResults.Add(new FindResult(item.Index, line.Trim(), l + 1, reps));
                }
            }
            catch
            {
            }
            if (invalidate)
                Dispatcher.Invoke(this.InvalidateVisual);
        }

        private void ShowFind()
        {
            FindText = this.SelectedText;
            FindReplaceVisible = true;
        }

        private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            this.RenderLineNumbers();
            this.InvalidateVisual();
        }

        protected void BracesMatcher(int start, char c)
        {
            if (c == '{')
            {
                FindMatchingForward(this.Text, start, c, '}');
            }
            else if (c == '}')
            {
                FindMatchingBackwards(this.Text, start, c, '{');
            }
            else if (c == '(')
            {
                FindMatchingForward(this.Text, start, c, ')');
            }
            else if (c == ')')
            {
                FindMatchingBackwards(this.Text, start, c, '(');
            }
            else if (c == '<')
            {
                FindMatchingForward(this.Text, start, c, '>');
            }
            else if (c == '>')
            {
                FindMatchingBackwards(this.Text, start, c, '<');
            }
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            this._autoComplete.CloseAutoComplete();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            _autoComplete.CloseAutoComplete();
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            lock (_found)
            { _found.Clear(); }

            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                this.InsertText("    ");
                CaretIndex += 4;
                e.Handled = true;
                return;
            }
            if (e.KeyboardDevice.IsKeyDown(Key.F) && e.KeyboardDevice.IsKeyDown(Key.LeftCtrl))
            {
                ShowFind();
                e.Handled = true;
                return;
            }
            if (e.KeyboardDevice.IsKeyDown(Key.H) && e.KeyboardDevice.IsKeyDown(Key.LeftCtrl))
            {
                FindText = this.SelectedText.Trim();
                ShowFind();
                e.Handled = true;
                return;
            }
            if (e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.K) && e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.LeftCtrl))
            {
                Indenter i = new Indenter();
                this.Text = i.Process(this.TextRead());
                this._autoComplete.CloseAutoComplete();

                e.Handled = true;
                return;
            }
            if (_autoComplete.IsPopupVisible && (e.Key == Key.Down || e.Key == Key.Up)
                  /*  && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))*/)
            {
                _autoComplete.SendEvent(e);
                e.Handled = true;
                return;
            }
            if (_autoComplete.IsPopupVisible && (e.Key == Key.Space || e.Key == Key.Enter))
            {
                this.CaretIndex = this.SelectionStart + this.SelectionLength;
                _autoComplete.CloseAutoComplete();
                if (e.Key == Key.Space)
                {
                    //this.InsertText(" ");

                    //e.Handled = true;
                    //return;
                }
            }
            if (e.Key == Key.Escape)
            {
                _autoComplete.CloseAutoComplete();
                _found.Clear();
            }
            if (e.Key == Key.Enter)
            {
                Indenter i = new Indenter();
                int indent = i.GetIndentLevelForLine(this.Text, this.GetLineIndexFromCharacterIndex(CaretIndex));
                string line = "\r\n" + new string(' ', indent * 4);
                int index = CaretIndex + line.Length;
                this.Text = this.Text.Insert(CaretIndex, line);
                CaretIndex = index;
                e.Handled = true;
            }

            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                this.RenderLineNumbers();
            }

            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
            {
                int l = CaretIndex - 1;
                if (l < 0)
                    l = 0;
                char c = this.Text[l];
                BracesMatcher(l, c);

                if (e.Key == Key.Left || e.Key == Key.Right)
                    this._autoComplete.CloseAutoComplete();
            }
            else if (e.Key != Key.Enter && e.SystemKey == Key.None && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                if (_timer != null)
                    _timer.Dispose();

                _timer = new Timer(ProcessAutoComplete, e.Key, 100, Timeout.Infinite);
            }

            base.OnPreviewKeyUp(e);
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            //lock (_found)
            //    _found.Clear();
            int item = this.GetCharacterIndexFromPoint(e.GetPosition(this), true);
            if (item < this.Text.Length)
            {
                char c = this.Text[item];
                BracesMatcher(item, c);
            }
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            _braces = default;

            if (_autoComplete.IsPopupVisible && (e.Text == "(" || e.Text == ")" || e.Text == "<" || e.Text == ">"))
            {
                this.CaretIndex = this.SelectionStart + this.SelectionLength;
                _autoComplete.CloseAutoComplete();
            }

            if (e.Text == "{")
            {
                Indenter i = new Indenter();
                int indent = i.GetIndentLevelForLine(this.Text, this.GetLineIndexFromCharacterIndex(CaretIndex) - 1);
                string line = " {\r\n" + new string(' ', (indent + 1) * 4) + "\r\n" + new string(' ', (indent) * 4) + "}\r\n";
                int newIndex = CaretIndex + (" {\r\n" + new string(' ', (indent + 1) * 4)).Length;
                this.Text = this.Text.Insert(CaretIndex, line);
                CaretIndex = newIndex;

                e.Handled = true;
            }
            base.OnPreviewTextInput(e);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.PushTransform(new TranslateTransform(-this.HorizontalOffset, -this.VerticalOffset));
            drawingContext.PushClip(new RectangleGeometry(new Rect(this.HorizontalOffset, this.VerticalOffset,
                this.ViewportWidth, this.ViewportHeight)));
            SetText(this.TextRead(), false, drawingContext);
        }

        protected override void OnSelectionChanged(RoutedEventArgs e)
        {
            base.OnSelectionChanged(e);

            if (!string.IsNullOrWhiteSpace(this.SelectedText))
            {
                //if (_selectionHandler != null)
                //{
                //    _selectionHandler.Dispose();
                //}
                //_selectionHandler = new Timer((o) =>
                //{
                //    Dispatcher.BeginInvoke((Action)(() => { RunFind(this.SelectedText, true); }));
                //}, null, 250, Timeout.Infinite);

                this.FindText = this.SelectedText;
            }
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            _bindedDocument.TextChanged(this.Text);
            lock (_found)
                _found.Clear();
            if (!_autoComplete.IsPopupVisible)
            {
                //if (!string.IsNullOrWhiteSpace(this.SelectedText))
                //    this.RunFind(this.SelectedText, false);
                //if (!string.IsNullOrEmpty(FindText))
                //    this.RunFind(FindText, false);
            }
            _braces = default;

            _errors.Clear();

            base.OnTextChanged(e);
            this.InvalidateVisual();
        }

        public void CloseAutoComplete()
        {
            PopupControl.IsOpen = false;
        }

        public void FocusAutoComplete(Rect rec, IAutoCompleteCallback autoCompleteCallback, bool allowTyping)
        {
            _currentCallback = autoCompleteCallback;
            if (PopupControl.Parent == null)
            {
                if (this.Parent is Grid gf)
                {
                    gf.Children.Add(PopupControl);
                }
            }
            var g = PopupControl;

            g.IsOpen = true;
            g.Placement = PlacementMode.RelativePoint;
            g.HorizontalOffset = rec.Left;
            g.VerticalOffset = rec.Bottom;

            g.Visibility = Visibility.Visible;

            _cb = (ListBox)((Grid)g.Child).Children[0];

            _cb.SelectionChanged -= AutoCompleteItemSelected;
            _cb.SelectionChanged += AutoCompleteItemSelected;
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

        public void ReportError(int line, int character)
        {
            if (!_errors.Contains((line, character)))
            {
                _errors.Add((line, character));
                this.InvalidateVisual();
            }
        }

        public void SendEvent(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                this.CloseAutoComplete();

            int index = _cb.SelectedIndex;

            if (e.Key == Key.Up)
            {
                index--;
            }
            else if (e.Key == Key.Down)
            {
                index++;
            }

            if (index < 0)
                index = 0;
            if (index > _cb.Items.Count - 1)
                index = _cb.Items.Count - 1;
            Debug.WriteLine(index);
            _cb.SelectedIndex = index;

            _cb.ScrollIntoView(_cb.SelectedItem);
        }

        public void SetAutoComplete(IAutoComplete autoComplete)
        {
            _autoComplete = autoComplete;
        }

        public void SetText(string text, bool format, DrawingContext col)
        {
            FormattedText formattedText = new FormattedText(
   this.Text,
   CultureInfo.GetCultureInfo("en-us"),
   FlowDirection.LeftToRight,
   new Typeface(this.FontFamily.Source),
   this.FontSize, Brushes.DarkBlue, VisualTreeHelper.GetDpi(this).PixelsPerDip);

            try
            {
                int start = int.MinValue;
                int end = int.MaxValue;

                ColorCoding coding = new ColorCoding();
                coding.FormatText(this.TextRead(), formattedText);
                lock (_found)
                {
                    foreach (var item in _found)
                    {
                        if (item.start >= start && item.start <= end)
                        {
                            var g = formattedText.BuildHighlightGeometry(new Point(4, 0), item.start, item.length);
                            col.DrawGeometry(Brushes.LightBlue, new Pen(Brushes.Black, 1), g);
                        }
                    }
                }
                if (_braces.selectionStart != 0 && _braces.match != 0)
                {
                    var g = formattedText.BuildHighlightGeometry(new Point(4, 0), _braces.selectionStart, 1);
                    col.DrawGeometry(Brushes.DarkRed, null, g);
                    g = formattedText.BuildHighlightGeometry(new Point(4, 0), _braces.match, 1);
                    col.DrawGeometry(Brushes.DarkRed, null, g);
                }

                foreach (var item in _errors)
                {
                    try
                    {
                        var l = GetCharacterIndexFromLineIndex(item.line - 1);
                        var len = GetLineLength(item.line - 1);
                        if (l >= start && l <= end)
                        {
                            TextDecoration td = new TextDecoration(TextDecorationLocation.Underline,
                                new System.Windows.Media.Pen(Brushes.Red, 2), 0, TextDecorationUnit.FontRecommended,
                                 TextDecorationUnit.FontRecommended);
                            TextDecorationCollection textDecorations = new TextDecorationCollection();
                            textDecorations.Add(td);

                            formattedText.SetTextDecorations(textDecorations, l, len);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
            }

            col.DrawText(formattedText, new Point(4, 0));
        }

        public void TextClear()
        {
            this.Text = "";
        }

        public string TextRead()
        {
            return Dispatcher.Invoke<string>(() => { return this.Text; });
        }

        public void TextWrite(string text, bool format)
        {
            this.Text = text;
            FindReplaceVisible = false;

            this._braces = default;
            lock (_found)
                this._found.Clear();

            if (format)
            {
                Indenter indenter = new Indenter();
                this.Text = indenter.Process(text);
            }
            this.InvalidateVisual();
        }
    }
}