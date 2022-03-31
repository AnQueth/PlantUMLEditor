#define USE_OLD_COLORING

using PlantUMLEditor.Models;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlantUMLEditor.Controls
{
    public class MyTextBox : TextBox, INotifyPropertyChanged, ITextEditor, IAutoComplete
    {
        private record Error(int Line, int Character);

        private record FindItem(int Start, int Length);
        public record FindResult(int Index, string Line, int LineNumber, string ReplacePreview);

        private readonly List<Error> _errors = new();

        private readonly List<FindItem> _found = new();
        private IAutoComplete _autoComplete;

        private AutoCompleteParameters? _autoCompleteParameters;
        private Rect _autoCompleteRect;
        private TextDocumentModel? _bindedDocument;
        private (int selectionStart, int match) _braces;
        private DrawingVisual? _cachedDrawing;
        private ListBox? _cb;
        private List<FormatResult> _colorCodings = new();
        private IAutoCompleteCallback? _currentCallback = null;
        private string _findText = string.Empty;
        private int _lastKnownFirstCharacterIndex = 0;
        private int _lastKnownLastCharacterIndex = 0;
        private double _lineHeight = 0;
        private ImageSource? _lineNumbers;
        private bool _renderText;
        private string _replaceText = string.Empty;
        private double _scrollOffset;
        private FindResult? _selectedFindResult = null;
        private Timer? _timerForAutoComplete = null;
        private Timer? _timerForSelection = null;
        private bool _useRegex;
        private bool findReplaceVisible;


        public static readonly DependencyProperty FindAllReferencesCommandProperty =
DependencyProperty.Register("FindAllReferencesCommand", typeof(DelegateCommand<string>), typeof(MyTextBox));


        public static readonly DependencyProperty GotoDefinitionCommandProperty =
            DependencyProperty.Register("GotoDefinitionCommand", typeof(DelegateCommand<string>), typeof(MyTextBox));

        public static readonly DependencyProperty PopupControlProperty =
                    DependencyProperty.Register("PopupControl", typeof(Popup), typeof(MyTextBox));

        public MyTextBox()
        {
            DataContextChanged += MyTextBox_DataContextChanged;

            DefaultStyleKey = typeof(MyTextBox);

            Foreground = Brushes.Transparent;
            Background = Brushes.Transparent;
            FindCommand = new DelegateCommand(FindHandler);
            ReplaceCommand = new DelegateCommand(ReplaceHandler);
            ClearCommand = new DelegateCommand(ClearHandler);
            _autoComplete = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public DelegateCommand ClearCommand
        {
            get;
        }

        public DelegateCommand FindCommand
        {
            get;
        }

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

        public DelegateCommand<string> FindAllReferencesCommand
        {
            get => (DelegateCommand<string>)GetValue(FindAllReferencesCommandProperty);

            set => SetValue(FindAllReferencesCommandProperty, value);
        }

        public DelegateCommand<string> GotoDefinitionCommand
        {
            get => (DelegateCommand<string>)GetValue(GotoDefinitionCommandProperty);

            set => SetValue(GotoDefinitionCommandProperty, value);
        }

        public bool IsPopupVisible => PopupControl != null && PopupControl.IsOpen;

        public ImageSource? LineNumbers
        {
            get => _lineNumbers;
            set
            {
                _lineNumbers = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LineNumbers)));
            }
        }

        public Popup PopupControl
        {
            get => (Popup)GetValue(PopupControlProperty);

            set => SetValue(PopupControlProperty, value);
        }

        public DelegateCommand ReplaceCommand
        {
            get;
        }

        public string ReplaceText
        {
            get => _replaceText ?? string.Empty;

            set
            {
                _replaceText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReplaceText)));
            }
        }

        public FindResult? SelectedFindResult
        {
            get => _selectedFindResult;
            set
            {
                _selectedFindResult = value;
                if (value != null)
                {
                    GotoLine(value.LineNumber, string.Empty);
                }
            }
        }

        public bool UseRegex
        {
            get => _useRegex;
            set
            {
                _useRegex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseRegex)));
            }
        }

        private static T? FindDescendant<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null)
            {
                return default;
            }

            int numberChildren = VisualTreeHelper.GetChildrenCount(obj);
            if (numberChildren == 0)
            {
                return default;
            }

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
                T? potentialMatch = FindDescendant<T>(child);
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
                string? currentSelected = (string?)e.AddedItems[0];

                if (_autoCompleteParameters != null && _currentCallback != null && !string.IsNullOrEmpty(currentSelected))
                {
                    _currentCallback.Selection(currentSelected, _autoCompleteParameters);
                }
            }
        }

        private void CalculateFirstAndLastCharacters()
        {
            (int cf, int ce) = GetStartAndEndCharacters();

            _lastKnownFirstCharacterIndex = cf;
            _lastKnownLastCharacterIndex = ce;

            // Debug.WriteLine($"{_lastKnownFirstCharacterIndex} {_lastKnownLastCharacterIndex} {Text.Length}");
        }

        private void ClearHandler()
        {
            FindResults.Clear();
            FindText = "";
            ReplaceText = "";
            lock (_found)
            {
                _found.Clear();
            }

            ForceDraw();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            FindReplaceVisible = false;
            lock (_found)
            {
                _found.Clear();
            }
        }

        private int CountLines()
        {
            int lineCount = Text.Count(c => c == '\n');
            return lineCount;
        }

        private void FindHandler()
        {
            RunFind(FindText, true);
        }

        private void FindMatchingBackwards(ReadOnlySpan<char> text, int selectionStart, char inc, char matchChar)
        {
            int ends = 1;
            int match = 0;
            for (int c = selectionStart - 1; ends != 0 && c >= 0; c--)
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

            ForceDraw();
        }

        private void ForceDraw()
        {
            _renderText = true;
            InvalidateVisual();
        }

        private void FindMatchingForward(ReadOnlySpan<char> text, int selectionStart, char inc, char matchChar)
        {
            int ends = 1;
            int match = 0;
            for (int c = selectionStart + 1; ends != 0 && c < text.Length - 1; c++)
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

            ForceDraw();
        }

        private int GetCharaceterFromLine(int line)
        {
            int z = 0;
            int m = 0;
            for (int x = 0; x < Text.Length && m != line; x++)
            {
                if (Text[x] == '\n')
                {
                    m++;
                }
                if (m == line)
                {
                    return z;
                }

                z++;
            }
            return z;
        }

        /// <summary>
        /// stolen from ms source code
        /// </summary>
        /// <returns></returns>
        private double GetLineHeight()
        {
            FontFamily fontFamily = (FontFamily)this.GetValue(FontFamilyProperty);
            double fontSize = (double)this.GetValue(TextElement.FontSizeProperty);

            double lineHeight = 0;

            if (TextOptions.GetTextFormattingMode(this) == TextFormattingMode.Ideal)
            {
                lineHeight = fontFamily.LineSpacing * fontSize;
            }

            return lineHeight;
        }

        private int GetLineNumberDuringRender(int caretIndex)
        {
            ReadOnlySpan<char> text = Text.AsSpan();

            int linestart = 0;

            for (int i = 0; i <= caretIndex && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    linestart++;
                }
            }

            return linestart;
        }



        private (int, int) GetStartAndEndCharacters()
        {
            if (_lineHeight == 0)
            {
                _lineHeight = GetLineHeight();
            }

            GetStartAndEndLines(out int startLine, out int endLine);

            int sc = GetCharaceterFromLine(startLine);
            int ec = GetCharaceterFromLine(endLine);

            return (sc, ec);
        }

        private double _textTransformOffset = 0;
        private IColorCodingProvider? _colorCodingProvider;

        private void GetStartAndEndLines(out int startLine, out int endLine)
        {

            startLine = (int)Math.Floor((VerticalOffset / _lineHeight) + 0.0001);

            _textTransformOffset = _scrollOffset + (startLine == 0 ? 0 : _lineHeight);

            //Debug.WriteLine($"{VerticalOffset} {_lineHeight} {(VerticalOffset / _lineHeight) + 0.0001} {_scrollOffset} {startLine}");

            endLine = (int)Math.Ceiling((VerticalOffset + ActualHeight) / _lineHeight);
            ++endLine;
        }

        private void FindAllReferences()
        {
            string found = GetWordFromCursor();

            FindAllReferencesCommand?.Execute(found.Trim());
        }

        private void GoToDefinition()
        {
            string found = GetWordFromCursor();

            GotoDefinitionCommand?.Execute(found.Trim());
        }

        private string GetWordFromCursor()
        {
            StringBuilder sb = new(20);
            ReadOnlySpan<char> tp = Text.AsSpan();

            for (int c = CaretIndex; c >= 0; c--)
            {
                if (tp[c] is ' ' or '(' or ')' or '{' or '}' or '<' or '>' or '[' or ']')
                {
                    break;
                }

                sb.Insert(0, tp[c]);
            }
            for (int c = CaretIndex + 1; c <= tp.Length; c++)
            {
                if (tp[c] is ' ' or '(' or ')' or '{' or '}' or '<' or '>' or '[' or ']')
                {
                    break;
                }

                sb.Append(tp[c]);
            }

            string found = sb.ToString();
            return found;
        }

        private void MyTextBox_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not TextDocumentModel)
            {
                return;
            }

            CaretIndex = 0;

            _bindedDocument = (TextDocumentModel)e.NewValue;
            _autoComplete = this;
            _bindedDocument?.Binded(this);
        }

        private void ProcessAutoComplete(object? state)
        {
            if (state == null)
            {
                return;
            }

            int k = (int)(Key)state;

            Dispatcher.Invoke(() =>
           {
               Rect rec = GetRectFromCharacterIndex(CaretIndex);

               int line = GetLineIndexFromCharacterIndex(CaretIndex);

               string text = GetLineText(line);

               int c = GetCharacterIndexFromLineIndex(line);

               int where = CaretIndex;
               string word = "";
               int typedLength = 0;
               if (text.Length != 0)
               {
                   if (text.Length > CaretIndex + 1)
                   {
                       if (!char.IsWhiteSpace(text[CaretIndex + 1]))
                       {
                           return;
                       }
                   }

                   Stack<char> chars = new();
                   for (int x = CaretIndex - c - 1; x >= 0; x--)
                   {
                       while (x > text.Length - 1 && x > 0)
                       {
                           x--;
                       }

                       if (text[x] is ' ' or '<' or '>' or '(' or ')')
                       {
                           break;
                       }

                       chars.Push(text[x]);
                   }

                   where -= chars.Count;
                   typedLength = chars.Count;

                   while (chars.Count > 0)
                   {
                       word += chars.Pop();
                   }
               }
               _autoCompleteRect = rec;
               _autoCompleteParameters = new AutoCompleteParameters(text, line, word,
                   where, typedLength, CaretIndex - c);

               _bindedDocument?.AutoComplete(_autoCompleteParameters);
           });
        }

        private void RenderLineNumbers()
        {
            if (ActualHeight > 0)
            {
                int lines = GetLastVisibleLineIndex();
                double p = VisualTreeHelper.GetDpi(this).PixelsPerDip;

                Typeface tf = new(FontFamily, FontStyle, FontWeight, FontStretch);
                DrawingVisual dv = new();
                DrawingContext? context = dv.RenderOpen();
                Point pt = new(0, 0);

                context.PushTransform(new TranslateTransform(0, -VerticalOffset));

                for (int x = 0; x <= lines; x++)
                {
                    FormattedText ft = new((x + 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, tf, FontSize, Brushes.Black, p);
                    context.DrawText(ft, pt);
                    pt.Y += ft.Height;
                }

                context.Close();

                RenderTargetBitmap rtb = new(25, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                LineNumbers = rtb;
            }
        }

        private void ReplaceHandler()
        {
            if (string.IsNullOrEmpty(FindText))
            {
                return;
            }

            if (_useRegex)
            {
                Regex r = new(FindText);
                Text = r.Replace(Text, ReplaceText);
            }
            else
            {
                Text = Text.Replace(FindText, ReplaceText);
            }

            lock (_found)
            {
                _found.Clear();
            }

            ForceDraw();
        }

        private void RunFind(string search, bool invalidate)
        {
            lock (_found)
            {
                _found.Clear();
            }

            if (_autoComplete.IsPopupVisible)
            {
                return;
            }

            if (string.IsNullOrEmpty(search))
            {
                return;
            }

            try
            {
                FindResults.Clear();
                if (_useRegex)
                {
                    if (!search.StartsWith("(", StringComparison.InvariantCulture))
                    {
                        search = "(" + search;
                    }

                    if (!search.EndsWith(")", StringComparison.InvariantCulture))
                    {
                        search += ")";
                    }

                    Regex r = new(search, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                    MatchCollection? m = r.Matches(Text);

                    foreach (Group item in m)
                    {
                        lock (_found)
                        {
                            _found.Add(new FindItem(item.Index, item.Length));
                        }

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


                    int p = Text.IndexOf(search, StringComparison.InvariantCultureIgnoreCase);
                    while (p != -1)
                    {
                        _found.Add(new FindItem(p, search.Length));
                        int l = GetLineIndexFromCharacterIndex(p);

                        string line = GetLineText(l);


                        if (line is not null)
                        {
                            string reps = "";
                            if (!string.IsNullOrEmpty(ReplaceText))
                            {
                                reps = line.Replace(search, ReplaceText, StringComparison.InvariantCultureIgnoreCase).Trim();
                            }


                            FindResults.Add(new FindResult(p, line.Trim(), l + 1, reps));
                        }

                        p = Text.IndexOf(search, p + 1, StringComparison.InvariantCultureIgnoreCase);
                    }
                }
            }
            catch
            {
            }
            if (invalidate)
            {
                ForceDraw();
            }
        }

        private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            //  this._autoComplete.CloseAutoComplete();
            this._renderText = true;
            _scrollOffset = e.VerticalOffset % _lineHeight;
            CalculateFirstAndLastCharacters();
            RenderLineNumbers();
            ForceDraw();
        }

        /// <summary>
        /// moves the autocomplete selected item up or down
        /// </summary>
        /// <param name="e"></param>
        private void TrySelectingAutoCompleteItem(KeyEventArgs e)
        {
            if (_cb == null)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                CloseAutoComplete();
            }

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
            {
                index = 0;
            }

            if (index > _cb.Items.Count - 1)
            {
                index = _cb.Items.Count - 1;
            }

            _cb.SelectedIndex = index;

            _cb.ScrollIntoView(_cb.SelectedItem);
        }

        protected void BracesMatcher(int start, char c, out bool bracesWillTriggerRender)
        {
            if (c == '{')
            {
                FindMatchingForward(Text.AsSpan(), start, c, '}');
                bracesWillTriggerRender = true;
                return;
            }
            else if (c == '}')
            {
                FindMatchingBackwards(Text.AsSpan(), start, c, '{');
                bracesWillTriggerRender = true;
                return;
            }
            else if (c == '(')
            {
                FindMatchingForward(Text.AsSpan(), start, c, ')');
                bracesWillTriggerRender = true;
                return;
            }
            else if (c == ')')
            {
                FindMatchingBackwards(Text.AsSpan(), start, c, '(');
                bracesWillTriggerRender = true;
                return;
            }
            else if (c == '<')
            {
                FindMatchingForward(Text.AsSpan(), start, c, '>');
                bracesWillTriggerRender = true;
                return;
            }
            else if (c == '>')
            {
                FindMatchingBackwards(Text.AsSpan(), start, c, '<');
                bracesWillTriggerRender = true;
                return;
            }

            bracesWillTriggerRender = false;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            _autoComplete.CloseAutoComplete();
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);

            _autoComplete.CloseAutoComplete();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            _autoComplete.CloseAutoComplete();
        }

        protected override async void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (!_autoComplete.IsPopupVisible && e.Key is Key.Tab)
            {
                e.Handled = true;
                InsertText("    ");
                CaretIndex += 4;
                e.Handled = true;
                return;
            }
            else if (e.KeyboardDevice.IsKeyDown(Key.S) && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                if (_bindedDocument != null)
                {
                    await _bindedDocument.Save();
                }

                e.Handled = true;
                return;
            }
            else if (e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.K) && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                int pos = CaretIndex;

                Text = Indenter.Process(TextRead(), false);
                CaretIndex = pos;
                _autoComplete.CloseAutoComplete();

                e.Handled = true;
                return;
            }
            else if (e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.L) && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                int pos = CaretIndex;

                Text = Indenter.Process(TextRead(), true);
                CaretIndex = pos;
                _autoComplete.CloseAutoComplete();

                e.Handled = true;
                return;
            }
            else if (_autoComplete.IsPopupVisible && (e.Key is Key.Down or Key.Up))
            {
                TrySelectingAutoCompleteItem(e);
                e.Handled = true;
                return;
            }
            else if (_autoComplete.IsPopupVisible && (e.Key is Key.Left or Key.Right))
            {
                _autoComplete.CloseAutoComplete();
                return;
            }
            else if (_autoComplete.IsPopupVisible && (e.Key is Key.Tab or Key.Enter or Key.Space))
            {
                CaretIndex = SelectionStart + SelectionLength;
                _autoComplete.CloseAutoComplete();
            }
            else if (e.Key is Key.Back && _autoComplete.IsPopupVisible)
            {
                _autoComplete.CloseAutoComplete();
            }
            else if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
            {
                CalculateFirstAndLastCharacters();
                ForceDraw();
            }
            else if (e.Key is Key.Escape)
            {
                _autoComplete.CloseAutoComplete();
                lock (_found)
                {
                    _found.Clear();
                }

                ForceDraw();
            }
            else if (e.Key is Key.Enter)
            {
                int indent = Indenter.GetIndentLevelForLine(Text, GetLineIndexFromCharacterIndex(CaretIndex));
                string line = "\r\n" + new string(' ', indent * 4);
                int index = CaretIndex + line.Length;
                Text = Text.Insert(CaretIndex, line);
                CaretIndex = index;
                CloseAutoComplete();
                CalculateFirstAndLastCharacters();
                e.Handled = true;
            }


            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                GoToDefinition();
            }
            else if (e.Key == Key.F11)
            {
                FindAllReferences();
            }
            else if (_autoComplete.IsPopupVisible && (e.Key is Key.Down or Key.Up))
            {
                e.Handled = true;
                return;
            }
            else if (e.Key is Key.Tab)
            {
                e.Handled = true;
                return;
            }
            else if (e.Key is Key.Enter)
            {
                RenderLineNumbers();
            }
            else if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
            {
                int l = CaretIndex - 1;
                if (l < 0)
                {
                    l = 0;
                }

                if (Text.Length == 0)
                {
                    return;
                }

                char c = Text[l];
                BracesMatcher(l, c, out bool bracesWillTriggerRender);

                if (!bracesWillTriggerRender)
                {
                    CalculateFirstAndLastCharacters();
                    ForceDraw();
                }
            }
            else if (e.Key is not Key.Escape and not Key.Enter and not Key.LeftCtrl and not Key.RightCtrl and not Key.Back
                && e.SystemKey == Key.None
                && e.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                if (_timerForAutoComplete != null)
                {
                    _timerForAutoComplete.Dispose();
                }

                _timerForAutoComplete = new Timer(ProcessAutoComplete, e.Key, 100, Timeout.Infinite);
            }

            base.OnPreviewKeyUp(e);
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            bool needsRender = true;
            int item = GetCharacterIndexFromPoint(e.GetPosition(this), true);
            if (item < Text.Length)
            {
                char c = Text[item];
                BracesMatcher(item, c, out bool bracesWillTriggerRender);
                needsRender = !bracesWillTriggerRender;
            }

            if (needsRender)
            {
                ForceDraw();
            }
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            _braces = default;

            if (_autoComplete.IsPopupVisible && (e.Text is "(" or ")" or "<" or ">" or "-" or ":"))
            {
                CaretIndex = SelectionStart + SelectionLength;
                _autoComplete.CloseAutoComplete();
            }

            //if (e.Text is "{")
            //{
            //    bool process = false;
            //    for (var x = CaretIndex; x >= 0; x--)
            //    {
            //        if (Text[x] == '{')
            //        {
            //            break;
            //        }
            //        if (char.IsLetterOrDigit(Text[x]) && x != '{')
            //        {
            //            process = true;
            //            break;
            //        }
            //    }
            //    if (process)
            //    {
            //        int indent = Indenter.GetIndentLevelForLine(Text, GetLineIndexFromCharacterIndex(CaretIndex) - 1);
            //        string line = " {\r\n" + new string(' ', (indent + 1) * 4) + "\r\n" + new string(' ', (indent) * 4) + "}";
            //        int newIndex = CaretIndex + (" {\r\n" + new string(' ', (indent + 1) * 4)).Length;
            //        Text = Text.Insert(CaretIndex, line);
            //        CaretIndex = newIndex;
            //        CloseAutoComplete();
            //        e.Handled = true;
            //    }
            //}

            // CalculateFirstAndLastCharacters();

            base.OnPreviewTextInput(e);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.PushTransform(new TranslateTransform(-HorizontalOffset, -_textTransformOffset));
            drawingContext.PushClip(new RectangleGeometry(new Rect(HorizontalOffset, _textTransformOffset, ViewportWidth, ViewportHeight)));
            //drawingContext.PushTransform(new TranslateTransform(-HorizontalOffset, -VerticalOffset));
            //drawingContext.PushClip(new RectangleGeometry(new Rect(HorizontalOffset, VerticalOffset,
            //    ViewportWidth, ViewportHeight)));

            if (_renderText)
            {
                _cachedDrawing = new DrawingVisual();

                DrawingContext? d = _cachedDrawing.RenderOpen();

                DrawText(d);
                d.Close();

                _renderText = false;
            }

            if (_cachedDrawing != null)
            {
                drawingContext.DrawDrawing(_cachedDrawing.Drawing);
            }
        }

        protected override void OnSelectionChanged(RoutedEventArgs e)
        {
            base.OnSelectionChanged(e);

            //remove spaces from end of selection
            while (SelectedText.EndsWith(" ", StringComparison.InvariantCulture))
            {
                if (SelectionLength == 0)
                {
                    break;
                }

                SelectionLength--;
            }

            if (!string.IsNullOrWhiteSpace(SelectedText) && !SelectedText.Contains("\r\n", StringComparison.InvariantCulture))
            {
                //timer used to select text after text selection has calmed down
                if (_timerForSelection != null)
                {
                    _timerForSelection.Dispose();
                }
                _timerForSelection = new Timer((o) =>
                {
                    Dispatcher.BeginInvoke((Action)(() => { RunFind(SelectedText.Trim(), true); }));
                }, null, 250, Timeout.Infinite);

                FindText = SelectedText;
            }
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            //notify documents that text has changed
            _bindedDocument?.TextChanged(Text);

            lock (_found)
            {
                _found.Clear();
            }

            _braces = default;
            _errors.Clear();

            _colorCodings = _colorCodingProvider?.FormatText(TextRead()) ?? new();

            base.OnTextChanged(e);

            CalculateFirstAndLastCharacters();
            ForceDraw();
        }

        public void CloseAutoComplete()
        {
            PopupControl.IsOpen = false;
        }

        public void DrawText(DrawingContext col)
        {
            //Debug.WriteLine("DrawText");
            Stopwatch? sw = Stopwatch.StartNew();

            int cf = _lastKnownFirstCharacterIndex;
            int cl = _lastKnownLastCharacterIndex;

            while (cl > Text.Length)
            {
                cl--;
            }

            if (Text.Length > 0)
            {
                if (cl == -1)
                {
                    cl = Text.Length;
                }

                if (cf >= Text.Length)
                {
                    cf = 0;
                }

                if (cf + (cl - cf) > Text.Length || (cl - cf) < 0)
                {
                    Debug.WriteLine("TEXT OUT OF RANGE");

                    return;
                }

                string t = Text[cf..cl];
                FormattedText? formattedText = new FormattedText(t
    ,
    CultureInfo.GetCultureInfo("en-us"),
    FlowDirection.LeftToRight,
    new Typeface(FontFamily.Source),
    FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);

                // Debug.WriteLine($"{cf} {cl} {Text.Length} {t.Length}");
                foreach (FormatResult? c in _colorCodings.Where(z => z.Intersects(cf, cl)))
                {
                    try
                    {
                        formattedText.SetForegroundBrush(c.Brush, c.AdjustedStart(cf), c.AdjustedLength(cf, cl));
                        if (c.FontWeight != FontWeights.Normal)
                        {
                            formattedText.SetFontWeight(c.FontWeight, c.AdjustedStart(cf), c.AdjustedLength(cf, cl));
                        }
                        if (c.Italic)
                        {
                            formattedText.SetFontStyle(FontStyles.Italic, c.AdjustedStart(cf), c.AdjustedLength(cf, cl));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("colors");
                        Debug.WriteLine($"{c}");
                        Debug.WriteLine(ex);
                    }
                }

                lock (_found)
                {//highlight found words
                    foreach (FindItem? item in _found)
                    {
                        try
                        {
                            if (FormatResult.Intersects(cf, cl, item.Start, item.Start + item.Length))
                            {
                                int start = FormatResult.AdjustedStart(cf, item.Start);
                                int len = FormatResult.AdjustedLength(cf, cl, item.Start, item.Length, item.Start + item.Length);

                                TextDecoration td = new(TextDecorationLocation.Underline,
                              new System.Windows.Media.Pen(Brushes.DarkBlue, 5), 0, TextDecorationUnit.FontRecommended,
                               TextDecorationUnit.FontRecommended);

                                TextDecorationCollection textDecorations = new()
                                {
                                    td
                                };

                                formattedText.SetTextDecorations(textDecorations, start, len);

                                //var g = formattedText.BuildHighlightGeometry(new Point(4, 0), item.start, item.length);

                                //col.DrawGeometry(Brushes.LightBlue, new Pen(Brushes.Black, 1), g);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Finds");
                            Debug.WriteLine($"{ex}");
                        }
                    }
                }
                if (_braces.selectionStart != 0 && _braces.match != 0)
                {
                    try
                    {
                        if (FormatResult.Intersects(cf, cl, _braces.selectionStart, _braces.selectionStart + 1))
                        {
                            Geometry? g = formattedText.BuildHighlightGeometry(new Point(4, 0), FormatResult.AdjustedStart(cf, _braces.selectionStart), 1);
                            col.DrawGeometry(Brushes.LightBlue, null, g);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("braces Selection");
                        Debug.WriteLine($"{ex}");
                    }
                    try
                    {
                        if (FormatResult.Intersects(cf, cl, _braces.match, _braces.match + 1))
                        {
                            Geometry? g = formattedText.BuildHighlightGeometry(new Point(4, 0), FormatResult.AdjustedStart(cf, _braces.match), 1);
                            col.DrawGeometry(Brushes.LightBlue, null, g);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("braces match");
                        Debug.WriteLine($"{ex}");
                    }
                }

                foreach ((int line, int character) in _errors)
                {
                    try
                    {
                        int l = GetCharacterIndexFromLineIndex(line - 1);
                        int len = GetLineLength(line - 1);
                        if (l >= cf && l <= cl)
                        {
                            TextDecoration td = new(TextDecorationLocation.Underline,
                                new System.Windows.Media.Pen(Brushes.Red, 2), 0, TextDecorationUnit.FontRecommended,
                                 TextDecorationUnit.FontRecommended);

                            TextDecorationCollection textDecorations = new()
                            {
                                td
                            };

                            formattedText.SetTextDecorations(textDecorations, l - cf, len);
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("errors");
                    }
                }

                col.DrawText(formattedText, new Point(4, 0));
            }

            double top = (GetLineNumberDuringRender(CaretIndex) * _lineHeight) - VerticalOffset + _textTransformOffset;

            col.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Silver, 2), new Rect(0, top, Math.Max(ViewportWidth + HorizontalOffset, ActualWidth), _lineHeight));

            sw.Stop();
            Debug.WriteLine($"sw = {sw.ElapsedMilliseconds}");
        }

        public void GotoLine(int lineNumber, string? findText)
        {
            if (lineNumber == 0)
            {
                lineNumber = 1;
            }

            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    lineNumber--;
                    int c = GetCharaceterFromLine(lineNumber);// GetCharacterIndexFromLineIndex(lineNumber - 1);
                    if (c >= 0)
                    {
                        CaretIndex = c;
                    }

                    if (!string.IsNullOrEmpty(findText))
                    {
                        FindText = findText;
                        FindHandler();
                    }

                    GetStartAndEndLines(out int startLine, out int endLine);

                    if (startLine <= lineNumber && endLine >= lineNumber)
                    {
                        CalculateFirstAndLastCharacters();
                        ForceDraw();
                    }
                    else
                    {
                        ScrollToLine(lineNumber < 5 ? 1 : lineNumber - 5);
                    }
                });
            }
            catch { }
            Keyboard.Focus(this);
        }

        public void InsertText(string text)
        {
            int c = CaretIndex;

            Text = Text.Insert(c, text);

            CaretIndex = c;
        }

        public void InsertTextAt(string text, int index, int typedLength)
        {
            SelectionStart = index;

            if (typedLength > SelectionLength)
            {
                SelectionLength = typedLength;
            }

            if (!string.IsNullOrEmpty(SelectedText))
            {
                SelectedText = "";
            }

            SelectionStart = index;

            SelectedText = text;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            ScrollViewer? sv = FindDescendant<ScrollViewer>(this);
            if (sv != null)
            {
                sv.ScrollChanged += Sv_ScrollChanged;
            }
            _lineHeight = GetLineHeight();
        }

        public void ReportError(int line, int character)
        {
            Error? e = new Error(line, character);

            if (!_errors.Contains(e))
            {


                _errors.Add(e);
                ForceDraw();
            }
        }

        public void ShowAutoComplete(IAutoCompleteCallback autoCompleteCallback)
        {
            _currentCallback = autoCompleteCallback;
            if (PopupControl.Parent == null)
            {
                if (Parent is Grid gf)
                {
                    gf.Children.Add(PopupControl);
                }
            }
            Popup? g = PopupControl;

            g.IsOpen = true;
            g.Placement = PlacementMode.RelativePoint;
            g.HorizontalOffset = _autoCompleteRect.Left;
            g.VerticalOffset = _autoCompleteRect.Bottom;

            g.Visibility = Visibility.Visible;

            _cb = (ListBox)((Grid)g.Child).Children[0];

            _cb.SelectionChanged -= AutoCompleteItemSelected;
            _cb.SelectionChanged += AutoCompleteItemSelected;
        }

        public void TextClear()
        {
            Text = "";
        }

        public string TextRead()
        {
            return Dispatcher.Invoke<string>(() => { return Text; });
        }

        public void TextWrite(string text, bool format, IColorCodingProvider? colorCodingProvider)
        {
            _colorCodingProvider = colorCodingProvider;
            Text = text;
            FindReplaceVisible = false;

            _braces = default;
            lock (_found)
            {
                _found.Clear();
            }

            if (format)
            {
                Text = Indenter.Process(text, false);
            }
        }

        public void InsertTextAtCursor(string text)
        {


            Text = Text.Insert(CaretIndex, text);

        }

        public void Destroy()
        {
            this._bindedDocument = null;
            this._cachedDrawing = null;
            this.DataContext = null;

        }
    }
}