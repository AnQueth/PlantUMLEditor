using PlantUMLEditor.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PlantUMLEditor.Controls
{
    public class MyRichTextBox : RichTextBox
    {

        public static readonly DependencyProperty BindedDocumentProperty =
       DependencyProperty.Register(
       nameof(BindedDocument), typeof(DocumentModel),
       typeof(MyRichTextBox), new PropertyMetadata(BindedDocumentPropertyChanged)
       );

        private static void BindedDocumentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = d as MyRichTextBox;
            var value = (DocumentModel) e.NewValue;

            if (value != null)
            {
                value.TextWrite = m.SetText;
                value.TextRead = m.ReadText;
                value.InsertText = m.InsertText;
                value.Binded();
            }

            value = (DocumentModel)e.OldValue;
            if (value != null)
            {
                value.TextWrite = null;
                value.TextRead = null;
                value.InsertText = null;
            }

        }

        public DocumentModel BindedDocument
        {
            get
            {
                return (DocumentModel) GetValue(BindedDocumentProperty);
            }
            set
            {
                SetValue(BindedDocumentProperty, value);
         
            }
        }

        public void InsertText(string text)
        {

            CaretPosition.InsertTextInRun(text);
        }

        private void SetText(string text )
        {
          
        

            TextRange tr = new TextRange(Document.ContentStart, Document.ContentEnd);
        
            tr.Text = text;
        }

        private string ReadText()
        {
            TextRange tr = new TextRange(Document.ContentStart, Document.ContentEnd);
            return tr.Text;
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            
            BindedDocument.TextChanged(ReadText());
            base.OnTextChanged(e);
        }


        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
          

            var _currentCursorIndex = CaretPosition;

            var rec = _currentCursorIndex.GetCharacterRect(LogicalDirection.Forward);

            _currentCursorIndex.GetLineStartPosition(-int.MaxValue, out var line);

            string text = _currentCursorIndex.GetTextInRun(LogicalDirection.Backward);

            BindedDocument.AutoComplete(rec, text, line);
            base.OnPreviewKeyDown(e);


        }

       
    }
}
