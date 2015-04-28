﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace R4nd0mApps.TddStud10.Hosts.Console.Diagnostics
{
    [EventSource(Name = "R4nd0mApps-TddStud10-Hosts-Console")]
    internal sealed class Logger : EventSource
    {
        public static Logger I = new Logger();

        [Event(1, Level = EventLevel.Informational)]
        public void Log(string message)
        {
            WriteEvent(1, message);
        }

        [Event(2, Level = EventLevel.Error)]
        internal void LogError(string message)
        {
            base.WriteEvent(2, message);
        }

        [NonEvent]
        public void Log(string format, params object[] args)
        {
            if (IsEnabled())
            {
                Log(string.Format(format, args));
            }
        }
    }
}