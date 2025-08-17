// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace NiftyPerforce.Core
{
    public static class AsyncProcess
    {
        private const int DefaultTimeout = 30000; // in ms

        public delegate void OnDone(bool ok, object? arg0);

        public static void Init()
        {
            s_helperThread = new Thread(ThreadMain);
            s_loop = true;
            s_helperThread.Start();
        }

        public static void Term()
        {
            if (!s_loop)
                return;

            s_loop = false;
            s_startEvent.Release();
            if (!s_helperThread?.Join(1000) ?? false)
            {
                s_helperThread?.Abort();
            }
        }

        public static bool Run(string executable, string commandline, string? workingdir, OnDone? callback, object? callbackArg)
        {
            const int Timeout = 1000;

            bool ok;
            try
            {
                ok = RunCommand(executable, commandline, workingdir, Timeout);
            }
            catch
            {
                ok = false;
                Log.Error("Caught unhandled exception when running process -- suppressing so that we don't bring down Visual Studio");
            }

            callback?.Invoke(ok, callbackArg);
            return ok;
        }

        public static bool Schedule(string executable, string commandline, string? workingdir, OnDone? callback, object? callbackArg)
        {
            return Schedule(executable, commandline, workingdir, callback, callbackArg, DefaultTimeout);
        }

        public static bool Schedule(string executable, string commandline, string? workingdir, OnDone? callback, object? callbackArg, int timeout, Dictionary<string, string>? environmentVariables = null)
        {
            var cmd = new CommandThread(
                executable,
                commandline,
                workingdir,
                callback,
                callbackArg,
                timeout,
                environmentVariables);

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
            Log.Info("Scheduled {0} {1}", cmd.Executable, cmd.Commandline);
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------------------------------------------
        // BEGIN INTERNALS
        private static readonly Mutex s_queueLock = new Mutex();
        private static readonly Semaphore s_startEvent = new Semaphore(0, 9999);
        private static readonly Queue<CommandThread> s_commandQueue = new Queue<CommandThread>();
        private static Thread? s_helperThread;
        private static bool s_loop;

        private static void ThreadMain()
        {
            while (true)
            {
                s_startEvent.WaitOne();
                if (!s_loop)
                    return;

                CommandThread? cmd;

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
                        var thread = new Thread(cmd.Run);
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

            public Dictionary<string, string>? EnvironmentVariables { get; }

            public CommandThread(string executable, string commandline, string? workingdir, OnDone? callback, object? callbackArg, int timeout, Dictionary<string, string>? environmentVariables)
            {
                Executable = executable;
                Commandline = commandline;
                Workingdir = workingdir;
                Callback = callback;
                CallbackArg = callbackArg;
                Timeout = timeout;
                EnvironmentVariables = environmentVariables;
            }

            public void Run()
            {
                bool ok;
                try
                {
                    ok = RunCommand(Executable, Commandline, Workingdir, Timeout, EnvironmentVariables);
                }
                catch
                {
                    ok = false;
                    Log.Error("Caught unhandled exception in async process -- suppressing so that we don't bring down Visual Studio");
                }

                Callback?.Invoke(ok, CallbackArg);
            }
        }

        private static bool RunCommand(string executable, string commandline, string? workingdir, int timeout, Dictionary<string, string>? environmentVariables = null)
        {
            try
            {
                using var process = new System.Diagnostics.Process();

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

                Log.Debug("cmd: '{0} {1}'", executable, commandline);
                Log.Debug("dir: '{0}'", workingdir ?? "unset");

                if ((environmentVariables?.Count ?? 0) > 0)
                {
                    Log.Debug("extra environment variables: {0}", string.Join(",", environmentVariables.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                    foreach (KeyValuePair<string, string> kvp in environmentVariables!)
                        process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }

                process.StartInfo.CreateNoWindow = true;
                if (workingdir != null)
                    process.StartInfo.WorkingDirectory = workingdir;
                process.StartInfo.Arguments = commandline;

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

                bool exited;
                string alloutput;
                using (Process.Handler stderr = new Process.Handler(), stdout = new Process.Handler())
                {
                    process.OutputDataReceived += stdout.OnOutput;
                    process.BeginOutputReadLine();

                    process.ErrorDataReceived += stderr.OnOutput;
                    process.BeginErrorReadLine();

                    exited = process.WaitForExit(timeout);

                    stderr.Sentinel.WaitOne();
                    stdout.Sentinel.WaitOne();
                    alloutput = stdout.Buffer.Trim();
                    if (alloutput.Length > 0 && stderr.Buffer.Length > 0)
                        alloutput += "\n";
                    alloutput += stderr.Buffer.Trim();
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
