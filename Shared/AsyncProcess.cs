// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using System.Collections.Generic;
using System.Threading;

namespace Aurora
{
    public static class AsyncProcess
    {
        private const int s_defaultTimeout = 30000; // in ms

        public delegate void OnDone(bool ok, object? arg0);

        public static void Init()
        {
            m_helperThread = new System.Threading.Thread(new ThreadStart(ThreadMain));
            m_helperThread.Start();
        }

        public static void Term()
        {
            m_helperThread?.Abort();
        }

        public static bool Run(string executable, string commandline, string workingdir, OnDone? callback, object? callbackArg)
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

        public static bool Schedule(string executable, string commandline, string workingdir, OnDone? callback, object? callbackArg)
        {
            return Schedule(executable, commandline, workingdir, callback, callbackArg, s_defaultTimeout);
        }

        public static bool Schedule(string executable, string commandline, string workingdir, OnDone? callback, object? callbackArg, int timeout)
        {
            var cmd = new CommandThread (
                executable,
                commandline,
                workingdir,
                callback,
                callbackArg,
                timeout
            );

            try
            {
                m_queueLock.WaitOne();
                m_commandQueue.Enqueue(cmd);
            }
            finally
            {
                m_queueLock.ReleaseMutex();
            }

            m_startEvent.Release();
            Log.Info("Scheduled {0} {1}\n", cmd.executable, cmd.commandline);
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------
        //
        // BEGIN INTERNALS
        //
        private static readonly Mutex m_queueLock = new Mutex();
        private static readonly Semaphore m_startEvent = new Semaphore(0, 9999);
        private static readonly Queue<CommandThread> m_commandQueue = new Queue<CommandThread>();
        private static System.Threading.Thread? m_helperThread;

        private static void ThreadMain()
        {
            while (true)
            {
                m_startEvent.WaitOne();
                CommandThread? cmd = null;

                try
                {
                    m_queueLock.WaitOne();
                    cmd = m_commandQueue.Dequeue();
                }
                finally
                {
                    m_queueLock.ReleaseMutex();
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

        private class CommandThread
        {
            public readonly string executable;
            public readonly string commandline;
            public readonly string workingdir;
            public readonly OnDone? callback;
            public readonly object? callbackArg;
            public readonly int timeout = 10000;

            public CommandThread(string executable, string commandline, string workingdir, OnDone? callback, object? callbackArg, int timeout)
            {
                this.executable = executable;
                this.commandline = commandline;
                this.workingdir = workingdir;
                this.callback = callback;
                this.callbackArg = callbackArg;
                this.timeout = timeout;
            }

            public void Run()
            {
                bool ok;
                try
                {
                    ok = RunCommand(executable, commandline, workingdir, timeout);
                }
                catch
                {
                    ok = false;
                    Log.Error("Caught unhandled exception in async process -- supressing so that we don't bring down Visual Studio");
                }

                callback?.Invoke(ok, callbackArg);
            }
        }

        private static bool RunCommand(string executable, string commandline, string workingdir, int timeout)
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = executable;
                    if (0 == timeout)
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
                    process.StartInfo.WorkingDirectory = workingdir;
                    process.StartInfo.Arguments = commandline;

                    Log.Debug("executableName : " + executable);
                    Log.Debug("workingDirectory : " + workingdir);
                    Log.Debug("command : " + commandline);

                    if (!process.Start())
                    {
                        Log.Error("{0}: {1} Failed to start. Is Perforce installed and in the path?\n", executable, commandline);
                        return false;
                    }

                    if (0 == timeout)
                    {
                        // Fire and forget task.
                        return true;
                    }

                    bool exited = false;
                    string alloutput = "";
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

                        if (0 != process.ExitCode)
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

        //
        // END INTERNALS
        //
        // ---------------------------------------------------------------------------------------------------------------------------------------------
    }
}
