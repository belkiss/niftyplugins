// Copyright (C) 2006-2017 Jim Tilander, 2017-2025 Lambert Clara. See the COPYING file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace NiftyPerforce.Core
{
    public static class Log
    {
        // Internal enumeration. Only used in handlers to identify the type of message
        public enum Level
        {
            /// <summary>
            /// Debug level messages
            /// </summary>
            Debug,

            /// <summary>
            /// Informational messages
            /// </summary>
            Info,

            /// <summary>
            /// Warning messages
            /// </summary>
            Warn,

            /// <summary>
            /// Error messages
            /// </summary>
            Error,
        }

        // This needs to be implemented by all clients.
        public interface IHandler
        {
            void OnMessage(Level level, string message, string formattedLine);
        }

        // Helper class to keep the indent levels balanced (with the help of the using statement)

        // Log class implement below
        public static string Prefix { get; set; } = string.Empty;

        public static int HandlerCount => s_handlers.Count;

        public static void AddHandler(IHandler? handler)
        {
            if (handler == null)
                return;

            lock (s_handlers)
            {
                s_handlers.Add(handler);
            }
        }

        public static void RemoveHandler(IHandler handler)
        {
            lock (s_handlers)
            {
                s_handlers.Remove(handler);
            }
        }

        public static void ClearHandlers()
        {
            lock (s_handlers)
            {
                s_handlers.Clear();
            }
        }

        public static void Debug(string message, params object[] args)
        {
#if DEBUG
            OnMessage(Level.Debug, message, args);
#endif
        }

        public static void Info(string message, params object[] args)
        {
            OnMessage(Level.Info, message, args);
        }

        public static void Warning(string message, params object[] args)
        {
            OnMessage(Level.Warn, message, args);
        }

        public static void Error(string message, params object[] args)
        {
            OnMessage(Level.Error, message, args);
        }

        private static void OnMessage(Level level, string format, object[] args)
        {
            var chunks = new List<string>(4);
            if (!string.IsNullOrEmpty(Prefix))
                chunks.Add(Prefix);

            string levelName = level.ToString().PadLeft(5, ' ');
            chunks.Add(levelName);
            chunks.Add($"{Environment.CurrentManagedThreadId,4:X}");

            string message = args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, format, args) : format;
            chunks.Add($"{message}\n");

            string formattedLine = string.Join("|", chunks);
            foreach (IHandler handler in s_handlers)
            {
                handler.OnMessage(level, message, formattedLine);
            }
        }

        private static readonly List<IHandler> s_handlers = new List<IHandler>();
    }
}
