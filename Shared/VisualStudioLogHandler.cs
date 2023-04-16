// Copyright (C) 2006-2017 Jim Tilander, 2017-2023 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Aurora
{
    public class VisualStudioLogHandler : Log.IHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly object _lock = new object();
        private readonly string _name;
        private IVsOutputWindowPane? _pane;

        public VisualStudioLogHandler(string name, IServiceProvider serviceProvider)
        {
            _name = name;
            _serviceProvider = serviceProvider;
        }

        public void OnMessage(Log.Level level, string message, string formattedLine)
        {
            if (string.IsNullOrEmpty(message))
                return;

            ThreadHelper.JoinableTaskFactory.Run(() => LogAsync(formattedLine));
        }

        private async Task LogAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (EnsurePane())
                {
                    _pane!.OutputStringThreadSafe(message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        private bool EnsurePane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_pane == null)
            {
                lock (_lock)
                {
                    if (_pane == null)
                    {
                        var guid = Guid.NewGuid();
                        var output = _serviceProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                        Assumes.Present(output);
                        output!.CreatePane(ref guid, _name, 1, 0);
                        output!.GetPane(ref guid, out _pane);
                    }
                }
            }

            return _pane != null;
        }
    }
}
