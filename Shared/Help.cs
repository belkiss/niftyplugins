using System;
using System.Diagnostics;
using System.IO;

namespace Aurora
{
    public static class Help
    {
        public static string FindFileInPath(string filename)
        {
            string pathenv = Environment.GetEnvironmentVariable("PATH");
            string[] items = pathenv.Split(';');

            foreach (string item in items)
            {
                string candidate = Path.Combine(item, filename);
                if (System.IO.File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        public class StopwatchProfiler : IDisposable
        {
            private readonly Stopwatch _stopWatch;
            private readonly Action<long> _disposeAction;

            public StopwatchProfiler(Action<long> disposeAction)
            {
                _disposeAction = disposeAction;
                _stopWatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _disposeAction(_stopWatch.ElapsedMilliseconds);
            }
        }
    }
}
