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
    public class MyRichTextBox : RichTextBox, INotifyPropertyChanged
    {

        public static readonly DependencyProperty BindedDocumentProperty =
       DependencyProperty.Register(
       nameof(BindedDocument), typeof(DocumentModel),
       typeof(MyRichTextBox), new PropertyMetadata(BindedDocumentPropertyChanged)
       );


        private static void BindedDocumentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = d as MyRichTextBox;
            var value = (DocumentModel)e.NewValue;

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

        public void InsertText(string text)
        {

            CaretPosition.InsertTextInRun(text);
        }

        private void SetText(string text)
        {
            TextRange tr = new TextRange(Document.ContentStart, Document.ContentEnd);
            tr.Text = text;


        }
 
        private FlowDocument styledDocument;

        public event PropertyChangedEventHandler PropertyChanged;

     

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

 

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);


            var _currentCursorIndex = CaretPosition;




            var rec = _currentCursorIndex.GetCharacterRect(LogicalDirection.Forward);

            _currentCursorIndex.GetLineStartPosition(-int.MaxValue, out var line);


           
                 
            string text = _currentCursorIndex.GetTextInRun(LogicalDirection.Backward);

            BindedDocument.AutoComplete(rec, text, -line, "", 0, 0);







        }

    }
}
