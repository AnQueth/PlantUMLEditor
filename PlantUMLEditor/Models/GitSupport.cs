using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
 

namespace PlantUMLEditor.Models
{
    internal class GitSupport
    {
        public GitSupport()
        {
        }

        internal Task<(bool, string)> CommitAndSync(string folderBase)
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

                    using (var repo = new Repository(folderBase))
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
                            sb.AppendLine($"Merge status: {mr.Status}");
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
                        var commit = repo.Commit($"Commit at {DateTimeOffset.Now}", author, committer);
                        sb.AppendLine($"git commit -m \"Commit at {DateTimeOffset.Now}\"");

                        // Get the current branch
                        var currentBranch = repo.Head;
                        sb.AppendLine($"Current branch: {currentBranch.FriendlyName}");

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
                string username = null;
                string password = null;
                string line;
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
    }
}
