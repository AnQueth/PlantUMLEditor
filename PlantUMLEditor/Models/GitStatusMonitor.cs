using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using LibGit2Sharp;

namespace PlantUMLEditor.Models
{
    public class GitStatusMonitor : IDisposable
    {
        private readonly string _openedPath;
        private readonly string _repoRoot;
        private readonly Dispatcher _uiDispatcher;
        private readonly Action<IDictionary<string, GitFileStatus>> _onStatus;
        private Timer? _timer;
        private readonly TimeSpan _interval;

        public GitStatusMonitor(string repoPath, Dispatcher uiDispatcher, Action<IDictionary<string, GitFileStatus>> onStatus, TimeSpan? interval = null)
        {
            _openedPath = System.IO.Path.GetFullPath(repoPath);
            _repoRoot = FindRepoRoot(_openedPath) ?? _openedPath;
            _uiDispatcher = uiDispatcher;
            _onStatus = onStatus;
            _interval = interval ?? TimeSpan.FromSeconds(2);
        }

        public void Start()
        {
            Stop();
            // Create a one-shot timer; Poll will reschedule after completion
            _timer = new Timer(_ => Poll(), null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private void Poll()
        {
            try
            {
                using var repo = new Repository(_repoRoot);
                var status = repo.RetrieveStatus();

                var map = new Dictionary<string, GitFileStatus>(StringComparer.OrdinalIgnoreCase);

                foreach (StatusEntry entry in status)
                {
                    // Compute full path relative to repo root
                    var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(_repoRoot, entry.FilePath));
                    // Only include files under the opened path (supports deep subfolder openings)
                    if (!full.StartsWith(_openedPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    map[full] = Map(entry.State);
                }

                // Files considered unmodified can be omitted; but include explicitly if desired

                _uiDispatcher.InvokeAsync(() => _onStatus(map));
            }
            catch
            {
                // Swallow errors (non-git folder, transient issues)
            }
            finally
            {
                // Reschedule next poll after processing completes (prevents concurrent runs)
                _timer?.Change(_interval, Timeout.InfiniteTimeSpan);
            }
        }

        private static string? FindRepoRoot(string anyPath)
        {
            try
            {
                var discovered = Repository.Discover(anyPath);
                if (string.IsNullOrEmpty(discovered)) return null;
                var gitDir = discovered.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                if (gitDir.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = System.IO.Directory.GetParent(gitDir);
                    return parent?.FullName;
                }
                return gitDir;
            }
            catch
            {
                return null;
            }
        }

        private static GitFileStatus Map(FileStatus s)
        {
            if (s.HasFlag(FileStatus.Conflicted)) return GitFileStatus.Conflict;
            if (s.HasFlag(FileStatus.NewInWorkdir)) return GitFileStatus.Untracked;
            if (s.HasFlag(FileStatus.ModifiedInWorkdir)) return GitFileStatus.Modified;
            if (s.HasFlag(FileStatus.DeletedFromWorkdir)) return GitFileStatus.Deleted;
            if (s.HasFlag(FileStatus.NewInIndex)) return GitFileStatus.Staged;
            if (s.HasFlag(FileStatus.ModifiedInIndex)) return GitFileStatus.Staged;
            if (s.HasFlag(FileStatus.DeletedFromIndex)) return GitFileStatus.Staged;
            if (s.HasFlag(FileStatus.Ignored)) return GitFileStatus.Ignored;
            return GitFileStatus.Unmodified;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
