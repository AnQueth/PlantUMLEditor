using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace Parsers;

public interface ITokenVisitor
{
    void Visit(PlantUmlTokenizer.Token token);
}

public class Parser
{
    protected async Task FillPipeAsync(Stream stream, PipeWriter writer)
    {
        const int minBufferSize = 512;
        while (true)
        {
            Memory<byte> memory = writer.GetMemory(minBufferSize);
            int bytesRead = await stream.ReadAsync(memory);
            if (bytesRead == 0) break;

            writer.Advance(bytesRead);
            var result = await writer.FlushAsync();
            if (result.IsCompleted) break;
        }
        await writer.CompleteAsync();
    }

    protected async Task ReadPipeAsync(PipeReader reader, ITokenVisitor visitor)
    {
        while (true)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            PlantUmlTokenizer.Tokenize(buffer, visitor);

            reader.AdvanceTo(buffer.End);
            if (result.IsCompleted) break;
        }
        await reader.CompleteAsync();
    }
}

internal class UMLComponentDiagramTokenVisitor : ITokenVisitor
{
    public UMLModels.UMLComponentDiagram Diagram { get; private set; }
    private UMLModels.UMLPackage? _rootPackage;
    private readonly Stack<UMLModels.UMLPackage> _packageStack = new();
    private bool _expectingTitleText;

    // Entity building state
    private string? _pendingEntityType; // "component", "interface", etc.
    private string? _pendingName;
    private string? _pendingAlias;
    private UMLModels.UMLComponent? _currentComponentForPorts;

    // Relation state
    private string? _lastEntityRef; // last seen identifier or component name/alias
    private string? _relationSource;

    // Note connection state
    private bool _inNoteDeclaration;
    private string? _noteDirection; // left/right/top/bottom
    private bool _expectingOfTarget;
    private UMLModels.UMLNote? _currentNote;
    private bool _inMultilineNote;
    private string? _lastArrowRaw;
    private string? _lastArrowLabel;

    public void Visit(PlantUmlTokenizer.Token token)
    {
        switch (token.Type)
        {
            case PlantUmlTokenizer.TokenType.StartUml:
                // Initialize a diagram with an empty title and a root package
                _rootPackage = new UMLModels.UMLPackage("root");
                Diagram = new UMLModels.UMLComponentDiagram(string.Empty, string.Empty, _rootPackage);
                _packageStack.Clear();
                _packageStack.Push(_rootPackage);
                break;

            case PlantUmlTokenizer.TokenType.Title:
                _expectingTitleText = true;
                break;

            // Title text handled via HandleTextValue when _expectingTitleText is true

            case PlantUmlTokenizer.TokenType.Component:
                if (_rootPackage != null)
                {
                    var name = token.Value?.Trim() ?? string.Empty;
                    if (string.Equals(name, "component", StringComparison.OrdinalIgnoreCase))
                    {
                        // keyword form: expect Identifier and optional 'as' alias
                        _pendingEntityType = "component";
                        _pendingName = null;
                        _pendingAlias = null;
                    }
                    else
                    {
                        // bracketed form [Component Name]
                        var comp = new UMLModels.UMLComponent(string.Empty, name, name)
                        {
                            LineNumber = token.Line
                        };
                        CurrentPackage().Children.Add(comp);
                        _currentComponentForPorts = comp;
                        _lastEntityRef = comp.Alias ?? comp.Name;
                    }
                }
                break;

            case PlantUmlTokenizer.TokenType.Interface:
                // Interface lollipop declaration: next text is its name/alias
                _pendingEntityType = "interface";
                _pendingName = null;
                _pendingAlias = null;
                break;

            case PlantUmlTokenizer.TokenType.Note:
                // Standalone note (we do not yet parse connections here)
                if (_rootPackage != null)
                {
                    var note = new UMLModels.UMLNote(string.Empty, null)
                    {
                        LineNumber = token.Line
                    };
                    CurrentPackage().Children.Add(note);
                    _lastEntityRef = note.Alias ?? note.Text;
                    _inNoteDeclaration = true; // may be followed by direction and 'of'
                    _currentNote = note;
                    _inMultilineNote = true;
                }
                break;

            // 'As' handled in HandleIdentifier to avoid duplicate switch labels

            case PlantUmlTokenizer.TokenType.Left:
            case PlantUmlTokenizer.TokenType.Right:
            case PlantUmlTokenizer.TokenType.Top:
            case PlantUmlTokenizer.TokenType.Bottom:
                if (_inNoteDeclaration)
                {
                    _noteDirection = token.Type.ToString().ToLowerInvariant();
                }
                break;

            case PlantUmlTokenizer.TokenType.Of:
                if (_inNoteDeclaration)
                {
                    _expectingOfTarget = true;
                }
                break;

            case PlantUmlTokenizer.TokenType.Package:
                // Begin a package declaration; next Identifier/QuotedString is its name
                _pendingEntityType = "package";
                _pendingName = null;
                _pendingAlias = null;
                break;

            case PlantUmlTokenizer.TokenType.Together:
                // Group block similar to a package named 'together'
                _pendingEntityType = "together";
                _pendingName = "together";
                _pendingAlias = null;
                break;

            // Treat other element keywords similarly to component (keyword form)
            case PlantUmlTokenizer.TokenType.Database:
            case PlantUmlTokenizer.TokenType.Queue:
            case PlantUmlTokenizer.TokenType.Actor:
            case PlantUmlTokenizer.TokenType.Node:
            case PlantUmlTokenizer.TokenType.Cloud:
            case PlantUmlTokenizer.TokenType.Folder:
            case PlantUmlTokenizer.TokenType.Rectangle:
            case PlantUmlTokenizer.TokenType.Frame:
                _pendingEntityType = token.Type.ToString().ToLowerInvariant();
                _pendingName = null;
                _pendingAlias = null;
                break;

            case PlantUmlTokenizer.TokenType.Identifier:
                HandleIdentifier(token);
                break;

            case PlantUmlTokenizer.TokenType.As:
                // Next identifier becomes alias for the pending entity
                _pendingAlias = string.Empty; // mark that alias expected
                break;

            case PlantUmlTokenizer.TokenType.QuotedString:
            case PlantUmlTokenizer.TokenType.Label:
                HandleTextValue(token.Value, token.Line);
                if (_relationSource != null)
                {
                    _lastArrowLabel = token.Value;
                }
                break;

            case PlantUmlTokenizer.TokenType.End:
                // Could terminate multiline note ("end note") or grouping
                if (_inMultilineNote)
                {
                    _inMultilineNote = false;
                    _inNoteDeclaration = false;
                    _expectingOfTarget = false;
                    _noteDirection = null;
                }
                break;

            case PlantUmlTokenizer.TokenType.OpenBrace:
                // Enter last declared package block
                if ((_pendingEntityType == "package" || _pendingEntityType == "together") && !string.IsNullOrEmpty(_pendingName))
                {
                    var pkg = new UMLModels.UMLPackage(_pendingName!, alias: _pendingAlias);
                    CurrentPackage().Children.Add(pkg);
                    _packageStack.Push(pkg);
                    Diagram?.ContainedPackages.Add(pkg);
                    _pendingEntityType = null;
                    _pendingName = null;
                    _pendingAlias = null;
                }
                break;

            case PlantUmlTokenizer.TokenType.CloseBrace:
                // Exit current package block
                if (_packageStack.Count > 1)
                {
                    _packageStack.Pop();
                }
                break;

            case PlantUmlTokenizer.TokenType.Port:
            case PlantUmlTokenizer.TokenType.PortIn:
            case PlantUmlTokenizer.TokenType.PortOut:
                // Next identifier/label adds a port to the last component
                // Mark type via pending entity type state
                _pendingEntityType = token.Type.ToString().ToLowerInvariant();
                break;

            case PlantUmlTokenizer.TokenType.Arrow:
                // Prepare relation: next entity becomes the target
                _relationSource = _lastEntityRef;
                _lastArrowRaw = token.Value;
                break;

            case PlantUmlTokenizer.TokenType.Stereotype:
                // Attach stereotype as auxiliary info under the current component
                if (_currentComponentForPorts != null)
                {
                    _currentComponentForPorts.Children.Add(new UMLModels.UMLOther($"<<{token.Value}>>")
                    {
                        LineNumber = token.Line
                    });
                }
                break;

            case PlantUmlTokenizer.TokenType.Color:
                // Attach color tag as auxiliary info under the current component
                if (_currentComponentForPorts != null)
                {
                    _currentComponentForPorts.Children.Add(new UMLModels.UMLOther($"#{token.Value}")
                    {
                        LineNumber = token.Line
                    });
                }
                break;

            case PlantUmlTokenizer.TokenType.EndUml:
                // No specific action for now; diagram already built incrementally
                break;

            default:
                // Ignore other tokens in this minimal implementation
                break;
        }
    }

    private UMLModels.UMLPackage CurrentPackage()
    {
        return _packageStack.Count > 0 ? _packageStack.Peek() : (_rootPackage ?? new UMLModels.UMLPackage("root"));
    }

    private void HandleIdentifier(PlantUmlTokenizer.Token token)
    {
        var id = token.Value;

        // Interpret keyword 'as' from identifier stream for aliasing
        if (string.Equals(id, "as", StringComparison.OrdinalIgnoreCase))
        {
            if (_inNoteDeclaration && _currentNote != null)
            {
                _pendingAlias = string.Empty; // next identifier becomes note alias
                return;
            }
            _pendingAlias = string.Empty; // next identifier becomes alias for pending entity
            return;
        }

        // Alias assignment for current note
        if (_inNoteDeclaration && _pendingAlias == string.Empty && _currentNote != null)
        {
            _currentNote.Alias = id;
            _pendingAlias = null;
            return;
        }

        // Alias assignment for last created component (bracketed form with 'as')
        if (_pendingAlias == string.Empty && _currentComponentForPorts != null && _pendingEntityType == null)
        {
            _currentComponentForPorts.Alias = id;
            _pendingAlias = null;
            _lastEntityRef = _currentComponentForPorts.Alias ?? _currentComponentForPorts.Name;
            return;
        }

        // Note connection target after 'of'
        if (_inNoteDeclaration && _expectingOfTarget && _currentNote != null)
        {
            var connector = _noteDirection != null ? $"note {_noteDirection} of" : "note of";
            // Use note alias or text as identifier for second; if neither, generate
            var noteId = _currentNote.Alias ?? (!string.IsNullOrEmpty(_currentNote.Text) ? _currentNote.Text : $"note_{_currentNote.LineNumber}");
            Diagram?.AddNoteConnection(new UMLModels.UMLNoteConnection(id, connector, noteId));
            _inNoteDeclaration = false;
            _expectingOfTarget = false;
            _noteDirection = null;
            _lastEntityRef = id;
            return;
        }

        if (_pendingEntityType == "component")
        {
            if (string.IsNullOrEmpty(_pendingName))
            {
                _pendingName = id;
                return;
            }
            if (_pendingAlias == string.Empty)
            {
                _pendingAlias = id;
            }

            // finalize component
            var alias = _pendingAlias ?? _pendingName;
            var comp = new UMLModels.UMLComponent(string.Empty, _pendingName!, alias!)
            {
                LineNumber = token.Line
            };
            CurrentPackage().Children.Add(comp);
            _currentComponentForPorts = comp;
            _lastEntityRef = comp.Alias ?? comp.Name;
            _pendingEntityType = null;
            _pendingName = null;
            _pendingAlias = null;
            return;
        }

        if (_pendingEntityType == "interface")
        {
            // Create UMLInterface entity
            var name = id;
            var alias = _pendingAlias ?? name;
            var iface = new UMLModels.UMLInterface(string.Empty, name, alias, Array.Empty<UMLModels.UMLDataType>())
            {
                LineNumber = token.Line
            };
            CurrentPackage().Children.Add(iface);
            _lastEntityRef = iface.Alias ?? iface.Name;
            _pendingEntityType = null;
            _pendingName = null;
            _pendingAlias = null;
            return;
        }

        if (_pendingEntityType == "package")
        {
            _pendingName = id;
            return;
        }

        // Treat common element keywords as components as they appear as identifiers after keywords
        if (string.Equals(_pendingEntityType, "database", StringComparison.Ordinal) ||
            string.Equals(_pendingEntityType, "queue", StringComparison.Ordinal) ||
            string.Equals(_pendingEntityType, "actor", StringComparison.Ordinal) ||
            string.Equals(_pendingEntityType, "node", StringComparison.Ordinal) ||
            string.Equals(_pendingEntityType, "cloud", StringComparison.Ordinal) ||
            string.Equals(_pendingEntityType, "folder", StringComparison.Ordinal) ||
            string.Equals(_pendingEntityType, "rectangle", StringComparison.Ordinal) ||
            string.Equals(_pendingEntityType, "frame", StringComparison.Ordinal))
        {
            var alias = _pendingAlias ?? id;
            var comp = new UMLModels.UMLComponent(string.Empty, id, alias)
            {
                LineNumber = token.Line
            };
            CurrentPackage().Children.Add(comp);
            _currentComponentForPorts = comp;
            _lastEntityRef = comp.Alias ?? comp.Name;
            _pendingEntityType = null;
            _pendingName = null;
            _pendingAlias = null;
            return;
        }

        // Ports
        if (_pendingEntityType == "port" && _currentComponentForPorts != null)
        {
            _currentComponentForPorts.Ports.Add(id);
            _pendingEntityType = null;
            return;
        }
        if (_pendingEntityType == "portin" && _currentComponentForPorts != null)
        {
            _currentComponentForPorts.PortsIn.Add(id);
            _pendingEntityType = null;
            return;
        }
        if (_pendingEntityType == "portout" && _currentComponentForPorts != null)
        {
            _currentComponentForPorts.PortsOut.Add(id);
            _pendingEntityType = null;
            return;
        }

        // Relations: if we just saw an arrow, this identifier is the target
        if (_relationSource != null)
        {
            var target = id;
            ConnectRelation(_relationSource, target);
            _relationSource = null;
            _lastEntityRef = target;
            return;
        }

        // Track last reference for potential relations
        _lastEntityRef = id;
    }

    private void HandleTextValue(string text, int line)
    {
        if (_expectingTitleText && Diagram != null)
        {
            Diagram.Title = text;
            _expectingTitleText = false;
            return;
        }

        if (_pendingEntityType == "package" && string.IsNullOrEmpty(_pendingName))
        {
            _pendingName = text;
            return;
        }

        if (_pendingEntityType == "component" && string.IsNullOrEmpty(_pendingName))
        {
            _pendingName = text;
            return;
        }

        // For notes: use text as note content (supports multiline)
        if (_inMultilineNote && _currentNote != null)
        {
            if (string.IsNullOrEmpty(_currentNote.Text))
            {
                _currentNote.Text = text;
            }
            else
            {
                _currentNote.Text += "\n" + text;
            }
            _currentNote.LineNumber = line;
            _lastEntityRef = _currentNote.Alias ?? _currentNote.Text;
            return;
        }
    }

    private void ConnectRelation(string fromRef, string toRef)
    {
        // Find components by alias or name in current package hierarchy and connect consumes/exposes
        var allEntities = Diagram?.Entities ?? new List<UMLModels.UMLDataType>();
        UMLModels.UMLComponent? from = null;
        UMLModels.UMLComponent? to = null;
        foreach (var e in allEntities)
        {
            if (e is UMLModels.UMLComponent c)
            {
                if (string.Equals(c.Alias, fromRef, StringComparison.OrdinalIgnoreCase) || string.Equals(c.Name, fromRef, StringComparison.OrdinalIgnoreCase))
                    from = c;
                if (string.Equals(c.Alias, toRef, StringComparison.OrdinalIgnoreCase) || string.Equals(c.Name, toRef, StringComparison.OrdinalIgnoreCase))
                    to = c;
            }
        }

        if (from != null && to != null)
        {
            from.Consumes.Add(to);
            to.Exposes.Add(from);

            // Attach simple relation metadata
            var meta = $"arrow={_lastArrowRaw}; label={_lastArrowLabel}";
            from.Children.Add(new UMLModels.UMLOther(meta));
            to.Children.Add(new UMLModels.UMLOther(meta));
            _lastArrowRaw = null;
            _lastArrowLabel = null;
        }
    }
}

public class PlantUmlTokenizer
{
    public enum TokenType
    {
        StartUml,
        EndUml,
        Title,
        Component,
        Database,
        Queue,
        Actor,
        Interface,
        Package,
        Frame,
        Node,
        Cloud,
        Folder,
        Together,
        Rectangle,
        Port,
        PortIn,
        PortOut,
        Arrow,
        Note,
        Left,
        Right,
        Top,
        Bottom,
        Up,
        Down,
        Direction,
        To,
        Of,
        End,
        Footer,
        Skinparam,
        Sprite,
        OpenBrace,
        CloseBrace,
        OpenBracket,
        CloseBracket,
        OpenParen,
        CloseParen,
        Colon,
        As,
        Identifier,
        QuotedString,
        Label,
        Color,
        Stereotype,
        Newline,
        Whitespace,
        Comment,
        Unknown,
        EndOfFile
    }

    public struct Token
    {
        public TokenType Type;
        public string Value;
        public int Line;
        public int Column;

        public Token(TokenType type, string value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
    }

    private class TokenizerState
    {
        public int Line;
        public int Column;

        public TokenizerState()
        {
            Line = 1;
            Column = 1;
        }

        public void AdvanceColumn()
        {
            Column++;
        }

        public void AdvanceLine()
        {
            Line++;
            Column = 1;
        }
    }

    public static void Tokenize(ReadOnlySequence<byte> buffer, ITokenVisitor visitor)
    {
        var reader = new SequenceReader<byte>(buffer);
        var state = new TokenizerState();

        while (!reader.End)
        {
            var token = NextToken(ref reader, state);
            if (token.Type != TokenType.Whitespace && token.Type != TokenType.Comment)
            {
                visitor.Visit(token);
            }
        }
    }

    private static Token NextToken(ref SequenceReader<byte> reader, TokenizerState state)
    {
        SkipWhitespace(ref reader, state);

        if (reader.End)
        {
            return new Token(TokenType.EndOfFile, string.Empty, state.Line, state.Column);
        }

        int startLine = state.Line;
        int startColumn = state.Column;
        
        if (!reader.TryPeek(out byte current))
        {
            return new Token(TokenType.EndOfFile, string.Empty, state.Line, state.Column);
        }

        // Comments
        if (current == '\'')
        {
            return ReadComment(ref reader, state, startLine, startColumn);
        }

        // Quoted strings
        if (current == '"')
        {
            return ReadQuotedString(ref reader, state, startLine, startColumn);
        }

        // Special characters
        switch (current)
        {
            case (byte)'{':
                reader.Advance(1);
                state.AdvanceColumn();
                return new Token(TokenType.OpenBrace, "{", startLine, startColumn);
            case (byte)'}':
                reader.Advance(1);
                state.AdvanceColumn();
                return new Token(TokenType.CloseBrace, "}", startLine, startColumn);
            case (byte)'[':
                // Check if this is a bracketed component [Component Name]
                if (IsBracketedComponent(ref reader))
                {
                    return ReadBracketedComponent(ref reader, state, startLine, startColumn);
                }
                reader.Advance(1);
                state.AdvanceColumn();
                return new Token(TokenType.OpenBracket, "[", startLine, startColumn);
            case (byte)']':
                reader.Advance(1);
                state.AdvanceColumn();
                return new Token(TokenType.CloseBracket, "]", startLine, startColumn);
            case (byte)'(':
                // Check if this is an interface () or () "Interface Name"
                if (reader.TryPeek(1, out byte nextParen) && nextParen == ')')
                {
                    reader.Advance(2);
                    state.AdvanceColumn();
                    state.AdvanceColumn();
                    return new Token(TokenType.Interface, "()", startLine, startColumn);
                }
                reader.Advance(1);
                state.AdvanceColumn();
                return new Token(TokenType.OpenParen, "(", startLine, startColumn);
            case (byte)')':
                reader.Advance(1);
                state.AdvanceColumn();
                return new Token(TokenType.CloseParen, ")", startLine, startColumn);
            case (byte)':':
                reader.Advance(1);
                state.AdvanceColumn();
                // Check if there's label text after the colon
                SkipWhitespace(ref reader, state);
                if (!reader.End && reader.TryPeek(out byte labelStart) && 
                    labelStart != '\n' && labelStart != '\r')
                {
                    // Read the label text after the colon
                    return ReadLabel(ref reader, state, startLine, startColumn);
                }
                return new Token(TokenType.Colon, ":", startLine, startColumn);
            case (byte)'#':
                return ReadColor(ref reader, state, startLine, startColumn);
            case (byte)'<':
                if (reader.TryPeek(1, out byte next) && next == '<')
                {
                    return ReadStereotype(ref reader, state, startLine, startColumn);
                }
                break;
        }

        // Arrows and relationships
        if (IsArrowStart(ref reader))
        {
            return ReadArrow(ref reader, state, startLine, startColumn);
        }

        // Keywords and identifiers
        return ReadKeywordOrIdentifier(ref reader, state, startLine, startColumn);
    }

    private static void SkipWhitespace(ref SequenceReader<byte> reader, TokenizerState state)
    {
        while (reader.TryPeek(out byte c))
        {
            if (c == ' ' || c == '\t' || c == '\r')
            {
                reader.Advance(1);
                state.AdvanceColumn();
            }
            else if (c == '\n')
            {
                reader.Advance(1);
                state.AdvanceLine();
            }
            else
            {
                break;
            }
        }
    }

    private static Token ReadComment(ref SequenceReader<byte> reader, TokenizerState state, int startLine, int startColumn)
    {
        var startPosition = reader.Position;
        
        while (reader.TryPeek(out byte c) && c != '\n')
        {
            reader.Advance(1);
            state.AdvanceColumn();
        }
        
        var commentSequence = reader.Sequence.Slice(startPosition, reader.Position);
        string value = Encoding.UTF8.GetString(commentSequence);
        
        return new Token(TokenType.Comment, value, startLine, startColumn);
    }

    private static Token ReadQuotedString(ref SequenceReader<byte> reader, TokenizerState state, int startLine, int startColumn)
    {
        reader.Advance(1); // Skip opening quote
        state.AdvanceColumn();
        
        var startPosition = reader.Position;
        bool escaped = false;
        
        while (reader.TryPeek(out byte c))
        {
            if (escaped)
            {
                escaped = false;
                reader.Advance(1);
                state.AdvanceColumn();
                continue;
            }
            
            if (c == '\\')
            {
                escaped = true;
                reader.Advance(1);
                state.AdvanceColumn();
            }
            else if (c == '"')
            {
                break;
            }
            else if (c == '\n')
            {
                reader.Advance(1);
                state.AdvanceLine();
            }
            else
            {
                reader.Advance(1);
                state.AdvanceColumn();
            }
        }
        
        var stringSequence = reader.Sequence.Slice(startPosition, reader.Position);
        string value = Encoding.UTF8.GetString(stringSequence);
        
        // Skip closing quote if present
        if (reader.TryPeek(out byte closingQuote) && closingQuote == '"')
        {
            reader.Advance(1);
            state.AdvanceColumn();
        }
        
        return new Token(TokenType.QuotedString, value, startLine, startColumn);
    }

    private static Token ReadColor(ref SequenceReader<byte> reader, TokenizerState state, int startLine, int startColumn)
    {
        var startPosition = reader.Position;
        reader.Advance(1); // Skip #
        state.AdvanceColumn();
        
        while (reader.TryPeek(out byte c))
        {
            if ((c >= '0' && c <= '9') || 
                (c >= 'a' && c <= 'f') || 
                (c >= 'A' && c <= 'F') ||
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z'))
            {
                reader.Advance(1);
                state.AdvanceColumn();
            }
            else
            {
                break;
            }
        }
        
        var colorSequence = reader.Sequence.Slice(startPosition, reader.Position);
        string value = Encoding.UTF8.GetString(colorSequence);
        
        return new Token(TokenType.Color, value, startLine, startColumn);
    }

    private static Token ReadStereotype(ref SequenceReader<byte> reader, TokenizerState state, int startLine, int startColumn)
    {
        reader.Advance(2); // Skip <<
        state.AdvanceColumn();
        state.AdvanceColumn();
        
        var startPosition = reader.Position;
        
        while (reader.TryPeek(out byte c))
        {
            if (c == '>' && reader.TryPeek(1, out byte next) && next == '>')
            {
                break;
            }
            
            if (c == '\n')
            {
                reader.Advance(1);
                state.AdvanceLine();
            }
            else
            {
                reader.Advance(1);
                state.AdvanceColumn();
            }
        }
        
        var stereotypeSequence = reader.Sequence.Slice(startPosition, reader.Position);
        string value = Encoding.UTF8.GetString(stereotypeSequence);
        
        // Skip closing >>
        if (reader.TryPeek(out byte c1) && c1 == '>')
        {
            reader.Advance(1);
            state.AdvanceColumn();
            if (reader.TryPeek(out byte c2) && c2 == '>')
            {
                reader.Advance(1);
                state.AdvanceColumn();
            }
        }
        
        return new Token(TokenType.Stereotype, value, startLine, startColumn);
    }

    private static bool IsBracketedComponent(ref SequenceReader<byte> reader)
    {
        // Check if [ is followed by text and eventually a ]
        // Can span multiple lines for multi-line descriptions
        var tempReader = reader;
        tempReader.Advance(1); // Skip [
        
        int depth = 0;
        int newlineCount = 0;
        while (tempReader.TryPeek(out byte c))
        {
            if (c == '\n')
            {
                newlineCount++;
                // Allow multi-line component descriptions
                // But if we see too many newlines without finding ], it's probably not a component
                if (newlineCount > 20) // Reasonable limit for multi-line descriptions
                    return false;
            }
            if (c == ']')
                return true;
            if (depth > 500) // Prevent infinite loop
                return false;
            tempReader.Advance(1);
            depth++;
        }
        return false;
    }

    private static Token ReadBracketedComponent(ref SequenceReader<byte> reader, TokenizerState state, int startLine, int startColumn)
    {
        reader.Advance(1); // Skip [
        state.AdvanceColumn();
        
        var startPosition = reader.Position;
        
        while (reader.TryPeek(out byte c) && c != ']')
        {
            if (c == '\n')
            {
                reader.Advance(1);
                state.AdvanceLine();
            }
            else if (c == '\\' && reader.TryPeek(1, out byte next) && next == 'n')
            {
                // Handle \n escape sequence
                reader.Advance(2);
                state.AdvanceColumn();
                state.AdvanceColumn();
            }
            else
            {
                reader.Advance(1);
                state.AdvanceColumn();
            }
        }
        
        var componentSequence = reader.Sequence.Slice(startPosition, reader.Position);
        string value = Encoding.UTF8.GetString(componentSequence);
        
        // Skip closing ]
        if (reader.TryPeek(out byte closingBracket) && closingBracket == ']')
        {
            reader.Advance(1);
            state.AdvanceColumn();
        }
        
        return new Token(TokenType.Component, value, startLine, startColumn);
    }

    private static Token ReadLabel(ref SequenceReader<byte> reader, TokenizerState state, int startLine, int startColumn)
    {
        var startPosition = reader.Position;
        
        // Read until end of line or end of buffer
        while (reader.TryPeek(out byte c) && c != '\n' && c != '\r')
        {
            reader.Advance(1);
            state.AdvanceColumn();
        }
        
        var labelSequence = reader.Sequence.Slice(startPosition, reader.Position);
        string value = Encoding.UTF8.GetString(labelSequence).Trim();
        
        return new Token(TokenType.Label, value, startLine, startColumn);
    }

    private static bool IsArrowStart(ref SequenceReader<byte> reader)
    {
        if (!reader.TryPeek(out byte current))
            return false;
            
        if (current == '<' || current == '-' || current == '.' || current == '>')
            return true;
            
        if (current == '=' && reader.TryPeek(1, out byte next) && next == '=')
            return true;
            
        return false;
    }

    private static Token ReadArrow(ref SequenceReader<byte> reader, TokenizerState state, int startLine, int startColumn)
    {
        var startPosition = reader.Position;
        bool inBracket = false;
        
        while (reader.TryPeek(out byte c))
        {
            if (c == '[')
            {
                inBracket = true;
                reader.Advance(1);
                state.AdvanceColumn();
            }
            else if (c == ']')
            {
                inBracket = false;
                reader.Advance(1);
                state.AdvanceColumn();
            }
            else if (inBracket)
            {
                // Inside brackets, allow alphanumeric, #, and special chars for directions and colors
                // Examples: [down], [#red], [#00FF00], [bold,#blue]
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || 
                    (c >= '0' && c <= '9') || c == '_' || c == '#' || c == ',')
                {
                    reader.Advance(1);
                    state.AdvanceColumn();
                }
                else
                {
                    break;
                }
            }
            else if (c == '<' || c == '>' || c == '-' || c == '.' || c == '=' || 
                     c == '(' || c == ')' || c == 'o' || 
                     c == '#' || c == '|' || c == ',')
            {
                reader.Advance(1);
                state.AdvanceColumn();
            }
            else
            {
                break;
            }
        }
        
        var arrowSequence = reader.Sequence.Slice(startPosition, reader.Position);
        string value = Encoding.UTF8.GetString(arrowSequence);
        
        return new Token(TokenType.Arrow, value, startLine, startColumn);
    }

    private static Token ReadKeywordOrIdentifier(ref SequenceReader<byte> reader, TokenizerState state, int startLine, int startColumn)
    {
        var startPosition = reader.Position;
        
        while (reader.TryPeek(out byte c))
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || 
                (c >= '0' && c <= '9') || c == '_' || c == '@')
            {
                reader.Advance(1);
                state.AdvanceColumn();
            }
            else
            {
                break;
            }
        }
        
        var identifierSequence = reader.Sequence.Slice(startPosition, reader.Position);
        string value = Encoding.UTF8.GetString(identifierSequence);
        
        TokenType type = value.ToLowerInvariant() switch
        {
            "@startuml" => TokenType.StartUml,
            "@enduml" => TokenType.EndUml,
            "@endum" => TokenType.EndUml,
            "title" => TokenType.Title,
            "component" => TokenType.Component,
            "database" => TokenType.Database,
            "queue" => TokenType.Queue,
            "actor" => TokenType.Actor,
            "interface" => TokenType.Interface,
            "package" => TokenType.Package,
            "frame" => TokenType.Frame,
            "node" => TokenType.Node,
            "cloud" => TokenType.Cloud,
            "folder" => TokenType.Folder,
            "together" => TokenType.Together,
            "rectangle" => TokenType.Rectangle,
            "port" => TokenType.Port,
            "portin" => TokenType.PortIn,
            "portout" => TokenType.PortOut,
            "note" => TokenType.Note,
            "left" => TokenType.Left,
            "right" => TokenType.Right,
            "top" => TokenType.Top,
            "bottom" => TokenType.Bottom,
            "up" => TokenType.Up,
            "down" => TokenType.Down,
            "direction" => TokenType.Direction,
            "to" => TokenType.To,
            "of" => TokenType.Of,
            "end" => TokenType.End,
            "footer" => TokenType.Footer,
            "skinparam" => TokenType.Skinparam,
            "sprite" => TokenType.Sprite,
            "as" => TokenType.As,
            _ => TokenType.Identifier
        };
        
        return new Token(type, value, startLine, startColumn);
    }
}

public class ComponentDiagramParser : Parser
{
    public async Task<UMLModels.UMLComponentDiagram> Parse(StreamReader reader, string file)
    {
        using var stream = File.OpenRead(file);
        var pipe = new Pipe();
        var fillPipeTask = FillPipeAsync(stream, pipe.Writer);
        var visitor = new UMLComponentDiagramTokenVisitor();
        var readPipeTask = ReadPipeAsync(pipe.Reader, visitor);

        await Task.WhenAll(fillPipeTask, readPipeTask);

        return visitor.Diagram;
    }



}
