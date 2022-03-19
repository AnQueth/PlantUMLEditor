using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PlantUMLEditor.Models
{
    internal class TemporarySave
    {
        private readonly string _fileName;
        private volatile bool _running;
        private readonly Task _processor;
        private string? _text;
        private readonly ManualResetEventSlim _mre = new ManualResetEventSlim(false);

        public TemporarySave(string fileName)
        {
            _fileName = fileName + ".tmp";
            _running = true;
            _processor = SaveQueue();


        }

        private Task SaveQueue()
        {
            return Task.Factory.StartNew(async () =>
            {


                while (_running)
                {
                    if (_mre.Wait(500) && _text != null)
                    {
                        _mre.Reset();
                        await File.WriteAllTextAsync(_fileName, _text);
                        await Task.Delay(1000);
                    }
                }
            }, TaskCreationOptions.LongRunning).Unwrap();
        }

        public string? ReadIfExists()
        {
            if (File.Exists(_fileName))
            {
                return File.ReadAllText(_fileName);
            }

            return null;
        }

        public void Stop()
        {
            _running = false;
        }



        public void Save(string text)
        {
            _text = text;
            _mre.Set();
        }

        public void Clean()
        {
            File.Delete(_fileName);
        }

    }
}
