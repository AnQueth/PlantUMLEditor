using Newtonsoft.Json.Serialization;
using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace PlantUMLEditor.Controls
{
    public class MyRichTextBox : RichTextBox, INotifyPropertyChanged, ITextEditor
    {
        private static MyRichTextBox Me;

        private FlowDocument styledDocument;

        public static readonly DependencyProperty BindedDocumentProperty =
                       DependencyProperty.Register(
       nameof(BindedDocument), typeof(DocumentModel),
       typeof(MyRichTextBox), new PropertyMetadata(BindedDocumentPropertyChanged)
       );

        public MyRichTextBox()
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

        public FlowDocument StyledDocument
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
            var m = d as MyRichTextBox;
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

        private string ReadText()
        {
            TextRange tr = new TextRange(Document.ContentStart, Document.ContentEnd);
            return tr.Text;
        }

        private void SetText(string text)
        {
            TextRange tr = new TextRange(Document.ContentStart, Document.ContentEnd);
            tr.Text = text;
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);

            var _currentCursorIndex = CaretPosition;

            var rec = _currentCursorIndex.GetCharacterRect(LogicalDirection.Forward);

            _currentCursorIndex.GetLineStartPosition(-int.MaxValue, out var line);

            string text = _currentCursorIndex.GetTextInRun(LogicalDirection.Backward);

            BindedDocument.AutoComplete(new AutoCompleteParameters(rec, text, -line, "", 0, 0));
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            BindedDocument.TextChanged(ReadText());
            base.OnTextChanged(e);
        }

        public void GotoLine(int lineNumber)
        {
        }

        public void InsertText(string text)
        {
            CaretPosition.InsertTextInRun(text);
        }

        public void InsertTextAt(string text, int where, int originalLength)
        {
            throw new NotImplementedException();
        }

        public void TextClear()
        {
            throw new NotImplementedException();
        }

        public string TextRead()
        {
            throw new NotImplementedException();
        }

        public void TextWrite(string text)
        {
            throw new NotImplementedException();
        }
    }
}