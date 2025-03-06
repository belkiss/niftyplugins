// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;

namespace Aurora
{
    public static class AsyncProcess
    {
        private const int DefaultTimeout = 30000; // in ms

        public delegate void OnDone(bool ok, object? arg0);

        public static void Init()
        {
            s_helperThread = new System.Threading.Thread(new ThreadStart(ThreadMain));
            s_helperThread.Start();
        }

        public static void Term()
        {
            s_helperThread?.Abort();
        }

        public static bool Run(string executable, string commandline, string? workingdir, OnDone? callback, object? callbackArg)
        {
            int timeout = 1000;

            if (!RunCommand(executable, commandline, workingdir, timeout))
            {
                Log.Debug("Failed to run immediate (process hung?), trying again on a remote thread: " + commandline);
                return Schedule(executable, commandline, workingdir, callback, callbackArg);
            }
            else
            {
                callback?.Invoke(true, callbackArg);
            }

            return true;
        }

        public static bool Schedule(string executable, string commandline, string? workingdir, OnDone? callback, object? callbackArg)
        {
            return Schedule(executable, commandline, workingdir, callback, callbackArg, DefaultTimeout);
        }

        public static bool Schedule(string executable, string commandline, string? workingdir, OnDone? callback, object? callbackArg, int timeout)
        {
            var cmd = new CommandThread(
                executable,
                commandline,
                workingdir,
                callback,
                callbackArg,
                timeout);

            try
            {
                s_queueLock.WaitOne();
                s_commandQueue.Enqueue(cmd);
            }
            finally
            {
                s_queueLock.ReleaseMutex();
            }

            s_startEvent.Release();
            Log.Info("Scheduled {0} {1}\n", cmd.Executable, cmd.Commandline);
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------
        // BEGIN INTERNALS
        private static readonly Mutex s_queueLock = new Mutex();
        private static readonly Semaphore s_startEvent = new Semaphore(0, 9999);
        private static readonly Queue<CommandThread> s_commandQueue = new Queue<CommandThread>();
        private static System.Threading.Thread? s_helperThread;

        private static void ThreadMain()
        {
            while (true)
            {
                s_startEvent.WaitOne();
                CommandThread? cmd = null;

                try
                {
                    s_queueLock.WaitOne();
                    cmd = s_commandQueue.Dequeue();
                }
                finally
                {
                    s_queueLock.ReleaseMutex();
                }

                if (cmd != null)
                {
                    try
                    {
                        var thread = new System.Threading.Thread(new ThreadStart(cmd.Run));
                        thread.Start();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private sealed class CommandThread
        {
            public string Executable { get; }

            public string Commandline { get; }

            public string? Workingdir { get; }

            public OnDone? Callback { get; }

            public object? CallbackArg { get; }

            public int Timeout { get; } = 10000;

            public CommandThread(string executable, string commandline, string? workingdir, OnDone? callback, object? callbackArg, int timeout)
            {
                Executable = executable;
                Commandline = commandline;
                Workingdir = workingdir;
                Callback = callback;
                CallbackArg = callbackArg;
                Timeout = timeout;
            }

            public void Run()
            {
                bool ok;
                try
                {
                    ok = RunCommand(Executable, Commandline, Workingdir, Timeout);
                }
                catch
                {
                    ok = false;
                    Log.Error("Caught unhandled exception in async process -- suppressing so that we don't bring down Visual Studio");
                }

                Callback?.Invoke(ok, CallbackArg);
            }
        }

        private static bool RunCommand(string executable, string commandline, string? workingdir, int timeout)
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = executable;
                    if (timeout == 0)
                    {
                        // We are not for these processes reading the stdout and thus they could if they wrote more
                        // data on the output line hang.
                        process.StartInfo.RedirectStandardOutput = false;
                        process.StartInfo.RedirectStandardError = false;
                    }
                    else
                    {
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                    }

                    process.StartInfo.CreateNoWindow = true;
                    if (workingdir != null)
                        process.StartInfo.WorkingDirectory = workingdir;
                    process.StartInfo.Arguments = commandline;

                    Log.Debug("executableName : " + executable);
                    Log.Debug("workingDirectory : " + workingdir ?? "unset");
                    Log.Debug("command : " + commandline);

                    if (!process.Start())
                    {
                        Log.Error("{0}: {1} Failed to start. Is Perforce installed and in the path?\n", executable, commandline);
                        return false;
                    }

                    if (timeout == 0)
                    {
                        // Fire and forget task.
                        return true;
                    }

                    bool exited = false;
                    string alloutput = string.Empty;
                    using (Process.Handler stderr = new Process.Handler(), stdout = new Process.Handler())
                    {
                        process.OutputDataReceived += stdout.OnOutput;
                        process.BeginOutputReadLine();

                        process.ErrorDataReceived += stderr.OnOutput;
                        process.BeginErrorReadLine();

                        exited = process.WaitForExit(timeout);

                        stderr.Sentinel.WaitOne();
                        stdout.Sentinel.WaitOne();
                        alloutput = stdout.Buffer + "\n" + stderr.Buffer;
                    }

                    if (!exited)
                    {
                        Log.Info("{0}: {1} timed out ({2} ms)", executable, commandline, timeout);
                        process.Kill();
                        return false;
                    }
                    else
                    {
                        Log.Info(executable + ": " + commandline);
                        Log.Info(alloutput);

                        if (process.ExitCode != 0)
                        {
                            Log.Debug("{0}: {1} exit code {2}", executable, commandline, process.ExitCode);
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                Log.Error("{0}: {1} failed to spawn: {2}", executable, commandline, e.ToString());
                return false;
            }
        }

        // END INTERNALS
        // ---------------------------------------------------------------------------------------------------------------------------------------------
    }
}
