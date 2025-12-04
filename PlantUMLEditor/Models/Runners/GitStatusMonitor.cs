using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using LibGit2Sharp;

namespace PlantUMLEditor.Models.Runners
{
    public class GitStatusMonitor : IDisposable
    {
        private readonly string _openedPath;
 
        private readonly Dispatcher _uiDispatcher;
        private readonly Action<IDictionary<string, GitFileStatus>> _onStatus;
        private Timer? _timer;
        private readonly TimeSpan _interval;

        public GitStatusMonitor(string repoPath, Dispatcher uiDispatcher,
         Action<IDictionary<string, GitFileStatus>> onStatus, TimeSpan? interval = null)
        {
            _openedPath = System.IO.Path.GetFullPath(repoPath);
          
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
                var git = new GitSupport();
                var status = git.GetRawStatus(_openedPath);
                
       

        

      

                _uiDispatcher.InvokeAsync(() => _onStatus(status));
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

 

        public void Dispose()
        {
            Stop();
        }
    }
}
