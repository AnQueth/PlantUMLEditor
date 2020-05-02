using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

using System.Windows.Input;

namespace PlantUMLEditor.Controls
{
    public class MyTextBox : TextBox, INotifyPropertyChanged, ITextEditor
    {
        private static MyTextBox Me;

        private Timer _timer = null;

        private FixedDocument styledDocument = new FixedDocument();

        public static readonly DependencyProperty BindedDocumentProperty =
                               DependencyProperty.Register(
       nameof(BindedDocument), typeof(DocumentModel),
       typeof(MyTextBox), new PropertyMetadata(BindedDocumentPropertyChanged)
       );

        public MyTextBox()
        {
            Me = this;
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

        private void ProcessAutoComplete(object state)
        {
            Dispatcher.Invoke(() =>
            {
                BindedDocument.TextChanged(this.Text);

                SynatxFlowDocument syntaxFlowDocument = new SynatxFlowDocument();
                syntaxFlowDocument.SetText(this.Text);

                StyledDocument = syntaxFlowDocument.Document;

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
        }

        protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
        {
            BindedDocument.KeyPressed();
            if (this._timer == null)
            {
                _timer = new Timer(ProcessAutoComplete);
            }

            this._timer.Change(500, Timeout.Infinite);

            base.OnPreviewKeyUp(e);
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
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

            if (this.Text[typedLength] != char.MinValue)
            {
                while (char.IsLetterOrDigit(this.Text[typedLength]) && this.Text[typedLength] != '\r')
                {
                    typedLength++;
                }
            }

            if (this.Text[typedLength] == '\r')
                typedLength--;
            if (typedLength > this.SelectionLength)
                this.SelectionLength = typedLength;

            if (!string.IsNullOrEmpty(this.SelectedText))
                this.SelectedText = "";
            this.SelectionStart = index;

            this.SelectedText = text;
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