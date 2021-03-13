// Copyright (C) 2006-2010 Jim Tilander. See COPYING for and README for more details.
using EnvDTE;

namespace Aurora
{
    public class VisualStudioLogHandler : Log.IHandler
	{
        private readonly Plugin mPlugin;

		public VisualStudioLogHandler(Plugin plugin)
		{
            mPlugin = plugin;
		}

		public void OnMessage(Log.Level level, string message, string formattedLine)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            OutputWindowPane pane = mPlugin.OutputPane;
			if (null == pane)
				return;

			pane.OutputString(formattedLine);
		}
	}
}
