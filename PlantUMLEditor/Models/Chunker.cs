using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    /// <summary>
    /// Represents a contiguous portion of a source file that was placed into a chunk.
    /// </summary>
    public sealed record ChunkSegment(string FilePath, int StartInFile, int LengthInFile);

    /// <summary>
    /// A chunk produced for vector indexing. Text is what should be embedded.
    /// Segments describe where the text came from so matches can be traced back to files.
    /// </summary>
    public sealed class Chunk
    {
        public string Id { get; }
        public string Text { get; }
        public IReadOnlyList<ChunkSegment> Segments { get; }

        public Chunk(string id, string text, IReadOnlyList<ChunkSegment> segments)
        {
            Id = id;
            Text = text;
            Segments = segments;
        }
    }

    public sealed class ChunkOptions
    {
        public int MaxChars { get; init; } = 2000;
        public int OverlapChars { get; init; } = 200;
        /// <summary>
        /// If true, chunks are created from the concatenation of all files and chunks may span file boundaries.
        /// If false, each file is chunked independently.
        /// </summary>
        public bool CrossFile { get; init; } = false;
        /// <summary>
        /// Optional preprocessing of raw file text (e.g. remove binary data, normalize whitespace, strip markdown fenced blocks, etc.).
        /// </summary>
        public Func<string, string>? Preprocess { get; init; }
    }

    public static class Chunker
    {
        /// <summary>
        /// Create chunks for the provided files. Chunks contain an overlap so context is preserved across chunk boundaries.
        /// If options.CrossFile is true, files are concatenated with a small separator so chunks may cross file boundaries.
        /// </summary>
        public static async IAsyncEnumerable<Chunk> CreateChunksAsync(IEnumerable<string> files, ChunkOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            options ??= new ChunkOptions();
            var fileList = files.ToList();
            if (!fileList.Any())
                yield break;

            if (!options.CrossFile)
            {
                // Chunk each file independently
                foreach (var file in fileList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string raw;
                    try
                    {
                        raw = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        continue; // skip unreadable files
                    }

                    if (options.Preprocess is not null)
                        raw = options.Preprocess(raw) ?? string.Empty;

                    raw = NormalizeWhiteSpace(raw);

                    int pos = 0;
                    int max = Math.Max(1, options.MaxChars);
                    int overlap = Math.Clamp(options.OverlapChars, 0, max - 1);
                    int chunkIndex = 0;

                    while (pos < raw.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int take = Math.Min(max, raw.Length - pos);
                        string text = raw.Substring(pos, take);

                        var seg = new ChunkSegment(file, pos, take);
                        var id = BuildId(file, chunkIndex);
                        yield return new Chunk(id, text, new[] { seg });

                        chunkIndex++;
                        if (pos + take >= raw.Length)
                            break;

                        // advance by take - overlap so chunks overlap
                        pos += Math.Max(1, take - overlap);
                    }
                }

                yield break;
            }

            // Cross-file: concatenate all files with a separator and produce sliding-window chunks
            const string separator = "\n\n"; // small separator that will appear in chunk text
            var sb = new StringBuilder();
            var boundaries = new List<(string FilePath, int Start, int Length)>();

            foreach (var file in fileList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string raw;
                try
                {
                    raw = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                if (options.Preprocess is not null)
                    raw = options.Preprocess(raw) ?? string.Empty;

                raw = NormalizeWhiteSpace(raw);

                int start = sb.Length;
                sb.Append(raw);
                int length = raw.Length;
                sb.Append(separator);
                length += separator.Length;

                boundaries.Add((file, start, length));
            }

            string all = sb.ToString();
            int total = all.Length;
            int maxChars = Math.Max(1, options.MaxChars);
            int overlapChars = Math.Clamp(options.OverlapChars, 0, maxChars - 1);

            int step = Math.Max(1, maxChars - overlapChars);
            int index = 0;
            for (int pos = 0; pos < total; pos += step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int take = Math.Min(maxChars, total - pos);
                string text = all.Substring(pos, take);

                // determine segments by intersecting [pos, pos+take) with boundaries
                var segments = new List<ChunkSegment>();
                int chunkStart = pos;
                int chunkEnd = pos + take;

                foreach (var b in boundaries)
                {
                    int segStart = Math.Max(chunkStart, b.Start);
                    int segEnd = Math.Min(chunkEnd, b.Start + b.Length);
                    if (segEnd > segStart)
                    {
                        int startInFile = segStart - b.Start;
                        int lenInFile = segEnd - segStart;
                        // clamp to original file length (exclude separator from reported file-length segments if it maps beyond original file)
                        if (startInFile < 0) startInFile = 0;
                        if (lenInFile < 0) lenInFile = 0;
                        segments.Add(new ChunkSegment(b.FilePath, startInFile, lenInFile));
                    }
                }

                string id = BuildId("cross", index);
                yield return new Chunk(id, text, segments);
                index++;

                if (pos + take >= total)
                    break;
            }
        }

        private static string BuildId(string filePath, int index) => $"{Path.GetFileName(filePath)}::{index}";

        private static string NormalizeWhiteSpace(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            var sb = new StringBuilder(s.Length);
            bool lastWasSpace = false;
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }
                }
                else
                {
                    sb.Append(ch);
                    lastWasSpace = false;
                }
            }

            return sb.ToString().Trim();
        }
    }
}
