using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
 

namespace PlantUMLEditor.Models.Runners
{
    internal class GitSupport
    {
        public GitSupport()
        {
        }

        internal static string? FindRepoRoot(string anyPath)
        {
            try
            {
                var discovered = Repository.Discover(anyPath);
                if (string.IsNullOrEmpty(discovered)) return null;
                // Discover returns path to .git directory or workdir
                // Normalize to workdir (repo root)
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

        public record GitMetadataInfo(string RepositoryRoot, string CurrentBranch);

        internal GitMetadataInfo? GetCurrentBranch(string folderBase)
        {
            if (string.IsNullOrEmpty(folderBase)) 
                return null;
            var repoRoot = FindRepoRoot(folderBase) ?? folderBase;
            try
            {
                using var repo = new Repository(repoRoot);
                return new( repoRoot, repo.Head.FriendlyName);
            }
            catch
            {
                return null;
            }
        }

        internal bool UndoChanges(string folderBase, string fullPath)
        {
            if (string.IsNullOrEmpty(folderBase) || string.IsNullOrEmpty(fullPath))
            {
                return false;
            }

            try
            {
                var repoRoot = FindRepoRoot(folderBase) ?? folderBase;
                using var repo = new Repository(repoRoot);
                var tip = repo.Head.Tip;
                if (tip == null) return false;

                var rel = System.IO.Path.GetRelativePath(repoRoot, fullPath).Replace("\\", "/");

                // Restore workdir and index to HEAD for this path
                // Use Commit overload with paths
                repo.CheckoutPaths(tip.Sha, new[] { rel }, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });

                // Ensure file is unstaged (clear index changes)
                try { Commands.Unstage(repo, new[] { rel }); } catch { }

                // If file didn't exist in HEAD, delete from workdir
                var entry = tip[rel];
                if (entry == null && System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal Task<(bool, string)> CommitAndSync(string folderBase, string commitMessage)
        {
            if (string.IsNullOrEmpty(folderBase))
            {
                return Task.FromResult<(bool, string)>((false,string.Empty));
            }

            return Task.Run(() =>
            {
                try
                {
                    StringBuilder sb = new();
                    var repoRoot = FindRepoRoot(folderBase) ?? folderBase;
                    using (var repo = new Repository(repoRoot))
                    {
                        // Pull changes from the remote repository
                        var pullOptions = new PullOptions
                        {
                            FetchOptions = new FetchOptions
                            {
                                CredentialsProvider = UseGitCredentials
                            }
                        };
                        var signature = new Signature(AppSettings.Default.GITUser ,AppSettings.Default.GITEmail, DateTimeOffset.Now);
                        var mr =  Commands.Pull(repo, signature, pullOptions);
                        sb.AppendLine("git pull");

                        if (mr.Status == MergeStatus.UpToDate)
                        {
                            sb.AppendLine("Already up to date.");
                       
                        }
                        else
                        {
                            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Merge status: {mr.Status}");
                            return (true, sb.ToString());
                        }
                            // Stage all changes
                            Commands.Stage(repo, "*");
                        sb.AppendLine("git add *");

                        // Check for changes
                        var status = repo.RetrieveStatus();
                        if (!status.IsDirty)
                        {
                            sb.AppendLine("No changes to commit.");
                            return (false, sb.ToString());
                        }

                        // Commit changes
                        var author = new Signature(AppSettings.Default.GITUser, AppSettings.Default.GITEmail, DateTimeOffset.Now);
                        var committer = author;
                        var commit = repo.Commit(commitMessage, author, committer);
                        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"git commit -m \"{commitMessage}\"");

                        // Get the current branch
                        var currentBranch = repo.Head;
                        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Current branch: {currentBranch.FriendlyName}");

                        // Push changes with Git Credential Manager authentication
                        var pushOptions = new PushOptions
                        {
                            CredentialsProvider = UseGitCredentials
                        };
                        repo.Network.Push(currentBranch, pushOptions);
                        sb.AppendLine("git push");

                    }

                    return (false, sb.ToString());
                }
                catch (Exception ex)
                {
                    return (false, ex.ToString());
                }
            });
        }

      

        private Credentials UseGitCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types)
        {
         
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = "credential fill",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                Process process = new Process
                {
                    StartInfo = startInfo
                };

                process.Start();

                // Write query to stdin. 
                // For stdin to work we need to send \n instead of WriteLine
                // We need to send empty line at the end
                var uri = new Uri(url);
                process.StandardInput.NewLine = "\n";
                process.StandardInput.WriteLine($"protocol={uri.Scheme}");
                process.StandardInput.WriteLine($"host={uri.Host}");
                process.StandardInput.WriteLine($"path={uri.AbsolutePath}");
                process.StandardInput.WriteLine();

                // Get user/pass from stdout
                string? username = null;
                string? password = null;
                string? line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    string[] details = line.Split('=');
                    if (details[0] == "username")
                    {
                        username = details[1];
                    }
                    else if (details[0] == "password")
                    {
                        password = details[1];
                    }
                }

                return new UsernamePasswordCredentials()
                {
                    Username = username,
                    Password = password
                };
            
        }

        internal Dictionary<string, GitFileStatus> GetRawStatus(string openedPath)
        {
            var repoRoot = FindRepoRoot(openedPath);
            if(repoRoot == null)
                return new Dictionary<string, GitFileStatus>(StringComparer.OrdinalIgnoreCase);
            using var repo = new Repository(repoRoot);

            var status = repo.RetrieveStatus();
                  var map = new Dictionary<string, GitFileStatus>(StringComparer.OrdinalIgnoreCase);

                foreach (StatusEntry entry in status)
                {
                    // Compute full path relative to repo root
                    var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot, entry.FilePath));
                    // Only include files under the opened path (supports deep subfolder openings)
                    if (!full.StartsWith(openedPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    map[full] = Map(entry.State);
                }

                return map;
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
    }
}
