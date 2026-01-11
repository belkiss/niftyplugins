// Copyright (C) 2006-2017 Jim Tilander, 2017-2026 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Threading;

namespace NiftyPerforce.Core
{
    public static class Process
    {
        // Helper class to capture output correctly and send an event once we've reached the end of the file.
        public class Handler : IDisposable
        {
            public string Buffer { get; private set; } = string.Empty;

            public ManualResetEvent Sentinel { get; } = new ManualResetEvent(false);

            public void Dispose()
            {
                Sentinel.Close();
                GC.SuppressFinalize(this);
            }

            public void OnOutput(object sender, System.Diagnostics.DataReceivedEventArgs? e)
            {
                if (e?.Data == null)
                {
                    Sentinel.Set();
                }
                else
                {
                    Buffer = Buffer + e.Data + "\n";
                }
            }
        }

        public static string Execute(string executable, string? workingdir, string arguments)
        {
            using var process = new System.Diagnostics.Process();

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = executable;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            if (workingdir != null)
                process.StartInfo.WorkingDirectory = workingdir;
            process.StartInfo.Arguments = arguments;

            if (!process.Start())
            {
                Log.Error("{0}: Failed to start {1}.", executable, process.StartInfo.Arguments);
                return string.Empty;
            }

            using Handler stderr = new Handler(), stdout = new Handler();

            process.OutputDataReceived += stdout.OnOutput;
            process.BeginOutputReadLine();

            process.ErrorDataReceived += stderr.OnOutput;
            process.BeginErrorReadLine();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Log.Error("Failed to execute {0} {1}, exit code was {2}", executable, process.StartInfo.Arguments, process.ExitCode);
            }

            stderr.Sentinel.WaitOne();
            stdout.Sentinel.WaitOne();

            return stdout.Buffer + "\n" + stderr.Buffer;
        }
    }
}
