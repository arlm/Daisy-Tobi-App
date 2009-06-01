﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BitFactory.Logging;
using Microsoft.Practices.Composite.Logging;
using Tobi.Infrastructure;

namespace Tobi
{
    public class BitFactoryLoggerAdapter : ILoggerFacade
    {
        private CompositeLogger m_Logger;

        public static void DeleteLogFile()
        {
            if (File.Exists(UserInterfaceStrings.LOG_FILE_PATH))
            {
                Console.Write("Deleting log file [" + UserInterfaceStrings.LOG_FILE_PATH + "]...");
                File.Delete(UserInterfaceStrings.LOG_FILE_PATH);
                Console.Write("File deleted [" + UserInterfaceStrings.LOG_FILE_PATH + "].");
            }
        }

        public BitFactoryLoggerAdapter()
        {
            m_Logger = new CompositeLogger();

            Logger consoleLogger = TextWriterLogger.NewConsoleLogger();
            m_Logger.AddLogger("console", consoleLogger);
#if (true || DEBUG)
            consoleLogger.Formatter = new LogEntryFormatterCodeLocation();
#endif

            Logger fileLogger = new FileLogger(UserInterfaceStrings.LOG_FILE_PATH);
            m_Logger.AddLogger("file", fileLogger);
#if (true || DEBUG)
            fileLogger.Formatter = new LogEntryFormatterCodeLocation();
#endif
        }

        #region ILoggerFacade Members

        public void Log(string msg, Category category, Priority priority)
        {
            string message = msg;

#if (true || DEBUG)
            CodeLocation codeLocation = CodeLocation.GetCallerLocation(2);
            if (codeLocation != null)
            {
                message = message.Insert(0, codeLocation.ToString() + " -- ");
            }
#endif
            
            switch (category)
            {
                case Category.Info:
                    {
                        m_Logger.Log(LogSeverity.Info, message);
#if (false && DEBUG)
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("Info ");
                        consoleWritePriority(priority);
                        Console.WriteLine(" [" + message + "]");
#endif
                    }
                    break;
                case Category.Warn:
                    {
                        m_Logger.Log(LogSeverity.Warning, message);
#if (false && DEBUG)
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write("Warn ");
                        consoleWritePriority(priority);
                        Console.WriteLine(" [" + message + "]");
#endif
                    }
                    break;

                case Category.Exception:
                    {
                        m_Logger.Log(LogSeverity.Error, message);
#if (false && DEBUG)
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("Exception ");
                        consoleWritePriority(priority);
                        Console.WriteLine(" [" + message + "]");
#endif
                    }
                    break;

                case Category.Debug:
                    {
                        m_Logger.Log(LogSeverity.Debug, message);
#if (false && DEBUG)
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("Debug ");
                        consoleWritePriority(priority);
                        Console.WriteLine(" [" + message + "]");
#endif
                    }
                    break;
            }
        }

        #endregion

#if (false && DEBUG)

        private void consoleWritePriority(Priority priority)
        {
            switch (priority)
            {
                case Priority.High:
                    {
                        Console.Write("(High)");
                    }
                    break;
                case Priority.Medium:
                    {
                        Console.Write("(Medium)");
                    }
                    break;

                case Priority.Low:
                    {
                        Console.Write("(Low)");
                    }
                    break;

                case Priority.None:
                    {
                        //Console.Write("(None)");
                    }
                    break;
            }
        }
#endif
    }

    public class LogEntryFormatterCodeLocation : LogEntryFormatter
    {
        protected override string AsString(LogEntry aLogEntry)
        {
            String appString = "";
            if (aLogEntry.Application != null)
            {
                appString = "[" + aLogEntry.Application + "] -- ";
            }
            if (aLogEntry.Category != null)
            {
                appString = appString + "{" + aLogEntry.Category + "} --";
            }
            return aLogEntry.Message + " -- " + appString + "<" + aLogEntry.SeverityString + "> -- " + DateString(aLogEntry);
        }
    }

    /// <summary>
    /// Code much inspired (some parts copied) from Daniel Vaughan's Clog logging framework
    /// </summary>
    public class CodeLocation
    {
        /// <summary>
        /// Gets the caller location from the <see cref="StackTrace"/>.
        /// </summary>
        /// <returns>The code location that the call to log originated.</returns>
        public static CodeLocation GetCallerLocation(int methodCallCount)
        {
            StackTrace trace;
            string className;
            string methodName;
            string fileName;
            int lineNumber;
            StackFrame frame = null;

            try
            {
                trace = new StackTrace(methodCallCount, true);
                frame = trace.GetFrame(0);
            }
            catch (MethodAccessException ex)
            {
                Debug.Fail("Unable to get stack trace." + ex);
            }

            if (frame != null)
            {
                className = frame.GetMethod().ReflectedType.FullName;
                methodName = frame.GetMethod().Name;
                fileName = frame.GetFileName();
                lineNumber = frame.GetFileLineNumber();
            }
            else
            {
                className = string.Empty;
                methodName = string.Empty;
                fileName = string.Empty;
                lineNumber = -1;
            }

            return new CodeLocation()
            {
                ClassName = className,
                MethodName = methodName,
                FileName = fileName,
                LineNumber = lineNumber
            };
        }

        /// <summary>
        /// Gets or sets the name of the class.
        /// </summary>
        /// <value>The name of the class.</value>
        public string ClassName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the method.
        /// </summary>
        /// <value>The name of the method.</value>
        public string MethodName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
        /// <value>The name of the file.</value>
        public string FileName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the line number of the location.
        /// </summary>
        /// <value>The line number.</value>
        public int LineNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents 
        /// the current <see cref="CodeLocation"/>.
        /// The format is &lt;FileName&gt;(&lt;LineNumber&gt;):&lt;ClassName&gt;.&lt;MethodName&gt;
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="CodeLocation"/>.
        /// </returns>
        public override string ToString()
        {
            return string.Format("{0}({1}): {2}.{3}",
                FileName, LineNumber, ClassName, MethodName);
        }
    }
}
