#define USE_OLD_COLORING

using PlantUMLEditor.Models;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlantUMLEditor.Controls
{
    public class MyTextBox : TextBox, INotifyPropertyChanged, ITextEditor, IAutoComplete
    {
        record Error(int Line, int Character);
        record FindItem(int Start, int Length);
        public record FindResult(int Index, string Line, int LineNumber, string ReplacePreview);

        private readonly List<Error> _errors = new();

        private readonly List<FindItem> _found = new();
        private IAutoComplete _autoComplete;

        private DocumentModel _bindedDocument;
        private (int selectionStart, int match) _braces;
        private DrawingVisual _cachedDrawing;
        private ListBox _cb;
        private IAutoCompleteCallback? _currentCallback = null;
        private string _findText;
        private ImageSource _lineNumbers;

        private bool _renderText;
        private string _replaceText;
        private FindResult? _selectedFindResult = null;
        private Timer? _timerForAutoComplete = null;
        private Timer? _timerForSelection = null;
        private bool _useRegex;
        private bool findReplaceVisible;

        public static readonly DependencyProperty GotoDefinitionCommandProperty =
            DependencyProperty.Register("GotoDefinitionCommand", typeof(DelegateCommand<string>), typeof(MyTextBox));

        public static readonly DependencyProperty PopupControlProperty =
                    DependencyProperty.Register("PopupControl", typeof(Popup), typeof(MyTextBox));

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

        public event PropertyChangedEventHandler? PropertyChanged;

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

        public ObservableCollection<FindResult> FindResults { get; } = new ObservableCollection<FindResult>();

        public string FindText
        {
            get => _findText;

            set
            {
                _findText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FindText)));
            }
        }

        public DelegateCommand<string> GotoDefinitionCommand
        {
            get
            {
                return (DelegateCommand<string>)GetValue(GotoDefinitionCommandProperty);
            }

            set
            {
                SetValue(GotoDefinitionCommandProperty, value);
            }
        }

        public bool IsPopupVisible => PopupControl != null && PopupControl.IsOpen;

        public ImageSource LineNumbers
        {
            get
            {
                return _lineNumbers;
            }
            set
            {
                _lineNumbers = value;
                _lineNumbers.Freeze();
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
            get => _replaceText ?? string.Empty;

            set
            {
                _replaceText = value;
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
                    GotoLine(value.LineNumber, string.Empty);
            }
        }

        public bool UseRegex
        {
            get
            {
                return _useRegex;
            }
            set
            {
                _useRegex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseRegex)));
            }
        }

        private static T? FindDescendant<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) 
                return default;
            int numberChildren = VisualTreeHelper.GetChildrenCount(obj);
            if (numberChildren == 0) return default;

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

            return default;
        }

        private void AutoCompleteItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string currentSelected = (string)e.AddedItems[0] ;

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

            this._renderText = true;
            this.InvalidateVisual();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            FindReplaceVisible = false;
            lock (_found)
                _found.Clear();
            this._renderText = true;
            this.InvalidateVisual();
        }

        private void FindHandler()
        {
            RunFind(FindText, true);
        }

        private void FindMatchingBackwards(ReadOnlySpan<char> text, int selectionStart, char inc, char matchChar)
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

            this._renderText = true;
            this.InvalidateVisual();
        }

        private void FindMatchingForward(ReadOnlySpan<char> text, int selectionStart, char inc, char matchChar)
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

            this._renderText = true;
            this.InvalidateVisual();
        }

        private (int start, int len) GetLineInformation(int ch)
        {
            var text = this.Text.AsSpan();

            var linestart = 0;

            for (var i = ch; i >= 0; i--)
            {
                if (text[i] == '\n')
                {
                    linestart = i + 1;
                    break;
                }
            }
            for (var i = ch; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    return (linestart, i - linestart);
                }
                if (i == text.Length - 1)
                    return (linestart, (i - linestart) + 1);
            }

            throw new ArgumentOutOfRangeException(nameof(ch));
        }

        private void MyTextBox_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not DocumentModel)
                return;

            _bindedDocument = (DocumentModel)e.NewValue;

            _bindedDocument?.Binded(this);
        }

        private void ProcessAutoComplete(object? state)
        {
            int k = (int)(Key)state;

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
                    Stack<char> chars = new();
                    for (var x = CaretIndex - c - 1; x >= 0; x--)
                    {
                        while (x > text.Length - 1 && x > 0)
                            x--;
                        if (text[x] == ' ' || text[x] == '<' || text[x] == '>' || text[x] == '(' || text[x] == ')')
                            break;
                        chars.Push(text[x]);
                    }

                    where -= chars.Count;
                    typedLength = chars.Count;

                    while (chars.Count > 0)
                        word += chars.Pop();
                }

                _bindedDocument.AutoComplete(new AutoCompleteParameters(rec, text, line, word,
                    where, typedLength, (Key)state, CaretIndex - c));
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

                    Typeface tf = new(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch);
                    DrawingVisual dv = new();
                    var context = dv.RenderOpen();
                    Point pt = new(0, 0);

                    context.PushTransform(new TranslateTransform(0, -this.VerticalOffset));

                    for (var x = 0; x <= lines; x++)
                    {
                        FormattedText ft = new((x + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, tf, this.FontSize, Brushes.Black, p);
                        context.DrawText(ft, pt);
                        pt.Y += ft.Height;
                    }

                    context.Close();

                    RenderTargetBitmap rtb = new(25, (int)this.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(dv);

                    this.LineNumbers = rtb;
                }
            }));
        }

        private void ReplaceHandler()
        {
            if (string.IsNullOrEmpty(FindText))
                return;

            if (_useRegex)
            {
                Regex r = new(FindText);
                this.Text = r.Replace(this.Text, ReplaceText);
            }
            else
            {
                this.Text = this.Text.Replace(FindText, ReplaceText);
            }

            lock (_found)
                _found.Clear();
            this._renderText = true;
            this.InvalidateVisual();
        }

        private void RunFind(string text, bool invalidate)
        {
            lock (_found)
                _found.Clear();
            if (_autoComplete.IsPopupVisible)
                return;

            if (string.IsNullOrEmpty(text))
                return;

            if (text.Length < 3)
                return;

            string t = this.Text;

            try
            {
                FindResults.Clear();
                if (_useRegex)
                {
                    if (!text.StartsWith("(", StringComparison.InvariantCulture))
                        text = "(" + text;
                    if (!text.EndsWith(")", StringComparison.InvariantCulture))
                        text += ")";

                    Regex r = new(text, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                    var m = r.Matches(t);

                    foreach (Group item in m)
                    {
                        lock (_found)
                            _found.Add(new FindItem(item.Index, item.Length));
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
                else
                {
                    t = t.ToLowerInvariant();
                    string search = text.ToLowerInvariant();

                    var p = t.IndexOf(search, StringComparison.InvariantCulture);
                    while (p != -1)
                    {
                        _found.Add(new FindItem(p, text.Length));
                        int l = GetLineIndexFromCharacterIndex(p);

                        string line = GetLineText(l);

                        string reps = "";
                        if (!string.IsNullOrEmpty(ReplaceText))
                        {
                            reps = line.Replace(text, ReplaceText, StringComparison.InvariantCultureIgnoreCase);
                        }

                        FindResults.Add(new FindResult(p, line.Trim(), l + 1, reps));

                        p = t.IndexOf(search, p + 1, StringComparison.InvariantCulture);
                    }
                }
            }
            catch
            {
            }
            if (invalidate)
            {
                this._renderText = true;
                Dispatcher.Invoke(this.InvalidateVisual);
            }
        }

     
        private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            //  this._autoComplete.CloseAutoComplete();
            this.RenderLineNumbers();
            this.InvalidateVisual();
        }

        /// <summary>
        /// moves the autocomplete selected item up or down
        /// </summary>
        /// <param name="e"></param>
        private void TrySelectingAutoCompleteItem(KeyEventArgs e)
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

            _cb.SelectedIndex = index;

            _cb.ScrollIntoView(_cb.SelectedItem);
        }

        protected bool BracesMatcher(int start, char c)
        {
            if (c == '{')
            {
                FindMatchingForward(this.Text.AsSpan(), start, c, '}');
                return false;
            }
            else if (c == '}')
            {
                FindMatchingBackwards(this.Text.AsSpan(), start, c, '{');
                return false;
            }
            else if (c == '(')
            {
                FindMatchingForward(this.Text.AsSpan(), start, c, ')');
                return false;
            }
            else if (c == ')')
            {
                FindMatchingBackwards(this.Text.AsSpan(), start, c, '(');
                return false;
            }
            else if (c == '<')
            {
                FindMatchingForward(this.Text.AsSpan(), start, c, '>');
                return false;
            }
            else if (c == '>')
            {
                FindMatchingBackwards(this.Text.AsSpan(), start, c, '<');
                return false;
            }

            return true;
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            this._autoComplete.CloseAutoComplete();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);

            this._autoComplete.CloseAutoComplete();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            _autoComplete.CloseAutoComplete();
        }

        protected override async void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            lock (_found)
                _found.Clear();

            if (!this._autoComplete.IsPopupVisible && e.Key == Key.Tab)
            {
                e.Handled = true;
                this.InsertText("    ");
                CaretIndex += 4;
                e.Handled = true;
                return;
            }
            else if (e.KeyboardDevice.IsKeyDown(Key.S) && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                await _bindedDocument.Save();
                e.Handled = true;
                return;
            }
            else if (e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.K) && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                int pos = this.CaretIndex;

                this.Text = Indenter.Process(this.TextRead(), false);
                this.CaretIndex = pos;
                this._autoComplete.CloseAutoComplete();

                e.Handled = true;
                return;
            }
            else if (e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.L) && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                int pos = this.CaretIndex;

                this.Text = Indenter.Process(this.TextRead(), true);
                this.CaretIndex = pos;
                this._autoComplete.CloseAutoComplete();

                e.Handled = true;
                return;
            }
            else if (_autoComplete.IsPopupVisible && (e.Key == Key.Down || e.Key == Key.Up))
            {
                TrySelectingAutoCompleteItem(e);
                e.Handled = true;
                return;
            }
            else if (_autoComplete.IsPopupVisible && (e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Space))
            {
                this.CaretIndex = this.SelectionStart + this.SelectionLength;
                _autoComplete.CloseAutoComplete();
            }
            else if (e.Key == Key.Escape)
            {
                _autoComplete.CloseAutoComplete();
                _found.Clear();
            }
            else if (e.Key == Key.Enter)
            {
                int indent = Indenter.GetIndentLevelForLine(this.Text, this.GetLineIndexFromCharacterIndex(CaretIndex));
                string line = "\r\n" + new string(' ', indent * 4);
                int index = CaretIndex + line.Length;
                this.Text = this.Text.Insert(CaretIndex, line);
                CaretIndex = index;
                this.CloseAutoComplete();
                e.Handled = true;
            }

            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F12 && !string.IsNullOrEmpty(this.SelectedText))
            {
                GotoDefinitionCommand?.Execute(this.SelectedText.Trim());
            }
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
            else if ((e.Key != Key.Enter && e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl)
                && e.SystemKey == Key.None
                && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                if (_timerForAutoComplete != null)
                    _timerForAutoComplete.Dispose();

                _timerForAutoComplete = new Timer(ProcessAutoComplete, e.Key, 100, Timeout.Infinite);
            }

            base.OnPreviewKeyUp(e);
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            lock (_found)
                _found.Clear();

            bool needsRender = true;
            int item = this.GetCharacterIndexFromPoint(e.GetPosition(this), true);
            if (item < this.Text.Length)
            {
                char c = this.Text[item];
                needsRender = BracesMatcher(item, c);
            }

            if (needsRender)
            {
                this._renderText = true;
                this.InvalidateVisual();
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
                bool process = false;
                for (var x = CaretIndex; x >= 0; x--)
                {
                    if (this.Text[x] == '{')
                    {
                        break;
                    }
                    if (char.IsLetterOrDigit(this.Text[x]) && x != '{')
                    {
                        process = true;
                        break;
                    }
                }
                if (process)
                {
                    int indent = Indenter.GetIndentLevelForLine(this.Text, this.GetLineIndexFromCharacterIndex(CaretIndex) - 1);
                    string line = " {\r\n" + new string(' ', (indent + 1) * 4) + "\r\n" + new string(' ', (indent) * 4) + "}";
                    int newIndex = CaretIndex + (" {\r\n" + new string(' ', (indent + 1) * 4)).Length;
                    this.Text = this.Text.Insert(CaretIndex, line);
                    CaretIndex = newIndex;
                    this.CloseAutoComplete();
                    e.Handled = true;
                }
            }
            base.OnPreviewTextInput(e);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.PushTransform(new TranslateTransform(-this.HorizontalOffset, -this.VerticalOffset));
            drawingContext.PushClip(new RectangleGeometry(new Rect(this.HorizontalOffset, this.VerticalOffset,
                this.ViewportWidth, this.ViewportHeight)));

            if (this._renderText)
            {
                _cachedDrawing = new DrawingVisual();

                var d = _cachedDrawing.RenderOpen();

                DrawText(d);
                d.Close();

                this._renderText = false;
            }
            drawingContext.DrawDrawing(_cachedDrawing.Drawing);
        }

        protected override void OnSelectionChanged(RoutedEventArgs e)
        {
            base.OnSelectionChanged(e);

            //remove spaces from end of selection
            while (this.SelectedText.EndsWith(" ", StringComparison.InvariantCulture))
            {
                if (this.SelectionLength == 0)
                    break;

                this.SelectionLength--;
            }

            if (!string.IsNullOrWhiteSpace(this.SelectedText) && !this.SelectedText.Contains("\r\n", StringComparison.InvariantCulture))
            {
                //timer used to select text after text selection has calmed down
                if (_timerForSelection != null)
                {
                    _timerForSelection.Dispose();
                }
                _timerForSelection = new Timer((o) =>
                {
                    Dispatcher.BeginInvoke((Action)(() => { RunFind(this.SelectedText.Trim(), true); }));
                }, null, 250, Timeout.Infinite);

                this.FindText = this.SelectedText;
            }
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            //notify documents that text has changed
            _bindedDocument.TextChanged(this.Text);

            lock (_found)
                _found.Clear();

            _braces = default;
            _errors.Clear();

            base.OnTextChanged(e);
            this._renderText = true;
            this.InvalidateVisual();
        }

        public void CloseAutoComplete()
        {
            PopupControl.IsOpen = false;
        }

        public void DrawText(DrawingContext col)
        {
            var formattedText = new FormattedText(
 this.Text,
 CultureInfo.GetCultureInfo("en-us"),
 FlowDirection.LeftToRight,
 new Typeface(this.FontFamily.Source),
 this.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);

            try
            {
                var (start, len) = this.GetLineInformation(CaretIndex);

                var geo = formattedText.BuildHighlightGeometry(new Point(4, 0),
                    start, len);
                col.DrawGeometry(Brushes.WhiteSmoke, new Pen(Brushes.Silver, 2), geo);
            }
            catch
            {
            }

            try
            {
                int end = int.MaxValue;
                int start = 0;

                ColorCoding coding = new();
                ColorCoding.FormatText(this.TextRead(), formattedText);

                lock (_found)
                {//highlight found words
                    foreach (var item in _found)
                    {
                        if (item.Start >= start && item.Start <= end)
                        {
                            TextDecoration td = new(TextDecorationLocation.Underline,
                          new System.Windows.Media.Pen(Brushes.DarkBlue, 5), 0, TextDecorationUnit.FontRecommended,
                           TextDecorationUnit.FontRecommended);

                            TextDecorationCollection textDecorations = new()
                            {
                                td
                            };

                            formattedText.SetTextDecorations(textDecorations, item.Start, item.Length);

                            //var g = formattedText.BuildHighlightGeometry(new Point(4, 0), item.start, item.length);

                            //col.DrawGeometry(Brushes.LightBlue, new Pen(Brushes.Black, 1), g);
                        }
                    }
                }
                if (_braces.selectionStart != 0 && _braces.match != 0)
                {
                    var g = formattedText.BuildHighlightGeometry(new Point(4, 0), _braces.selectionStart, 1);
                    col.DrawGeometry(Brushes.LightBlue, null, g);
                    g = formattedText.BuildHighlightGeometry(new Point(4, 0), _braces.match, 1);
                    col.DrawGeometry(Brushes.LightBlue, null, g);
                }

                foreach (var (line, character) in _errors)
                {
                    try
                    {
                        var l = GetCharacterIndexFromLineIndex(line - 1);
                        var len = GetLineLength(line - 1);
                        if (l >= start && l <= end)
                        {
                            TextDecoration td = new(TextDecorationLocation.Underline,
                                new System.Windows.Media.Pen(Brushes.Red, 2), 0, TextDecorationUnit.FontRecommended,
                                 TextDecorationUnit.FontRecommended);

                            TextDecorationCollection textDecorations = new()
                            {
                                td
                            };

                            formattedText.SetTextDecorations(textDecorations, l, len);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception)
            {
            }

            col.DrawText(formattedText, new Point(4, 0));
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

        public void GotoLine(int lineNumber, string? findText)
        {
            if (lineNumber == 0)
                lineNumber = 1;

            Dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    CaretIndex = this.GetCharacterIndexFromLineIndex(lineNumber - 1);

                    if (!string.IsNullOrEmpty(findText))
                    {
                        this.FindText = findText;
                        this.FindHandler();
                    }

                    this.ScrollToLine(lineNumber - 1);

                    this._renderText = true;
                    this.InvalidateVisual();
                }
                catch { }
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
            var e = new Error(line, character);

            if (!_errors.Contains(e))
            {
                this._renderText = true;
                _errors.Add(e);
                this.InvalidateVisual();
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
                this.Text = Indenter.Process(text, false);
            }
            this._renderText = true;
            this.InvalidateVisual();
        }
    }
}