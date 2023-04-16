// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Threading;

namespace Aurora
{
    public static class Process
    {
        // Helper class to capture output correctly and send an event once we've reached the end of the file.
        public class Handler : IDisposable
        {
            public string Buffer { get; private set; }

            public ManualResetEvent Sentinel { get; set; }

            public Handler()
            {
                Buffer = string.Empty;
                Sentinel = new ManualResetEvent(false);
            }

            public void Dispose()
            {
                Sentinel.Close();
                GC.SuppressFinalize(this);
            }

            public void OnOutput(object sender, System.Diagnostics.DataReceivedEventArgs e)
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

        public static string Execute(string executable, string? workingdir, string arguments, bool throwIfNonZeroExitCode = true)
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
                throw new ProcessException("{0}: Failed to start {1}.", executable, process.StartInfo.Arguments);
            }

            using Handler stderr = new Handler(), stdout = new Handler();

            process.OutputDataReceived += stdout.OnOutput;
            process.BeginOutputReadLine();

            process.ErrorDataReceived += stderr.OnOutput;
            process.BeginErrorReadLine();

            process.WaitForExit();

            if (throwIfNonZeroExitCode && process.ExitCode != 0)
            {
                throw new ProcessException("Failed to execute {0} {1}, exit code was {2}", executable, process.StartInfo.Arguments, process.ExitCode);
            }

            stderr.Sentinel.WaitOne();
            stdout.Sentinel.WaitOne();

            return stdout.Buffer + "\n" + stderr.Buffer;
        }
    }

    public class ProcessException : Exception
    {
        public ProcessException()
        {
        }

        public ProcessException(string message)
            : base(message)
        {
        }

        public ProcessException(string info, params object[] vaargs)
            : this(string.Format(CultureInfo.InvariantCulture, info, vaargs))
        {
        }

        public ProcessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ProcessException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
