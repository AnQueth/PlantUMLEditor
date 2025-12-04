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
    public UMLModels.UMLComponentDiagram Diagram { get; private set; } = null!;
    

    public void Visit(PlantUmlTokenizer.Token token)
    {
       
       Console.WriteLine($"Token: {token.Type}, Value: '{token.Value}' at Line {token.Line}, Column {token.Column}");
        
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
            if (token.Type == TokenType.EndOfFile)
            {
                break;
            }
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
        
        // If we failed to consume any character, advance by one to avoid infinite loops
        if (reader.Sequence.GetPosition(0, startPosition).Equals(reader.Position))
        {
            // Consume a single byte as unknown
            if (reader.TryPeek(out byte unknown))
            {
                reader.Advance(1);
                if (unknown == '\n')
                {
                    state.AdvanceLine();
                }
                else
                {
                    state.AdvanceColumn();
                }
            }
            return new Token(TokenType.Unknown, string.Empty, startLine, startColumn);
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

    
        var diagram = visitor.Diagram;
  

        return diagram;
    }



}
