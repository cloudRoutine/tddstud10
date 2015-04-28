﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using R4nd0mApps.TddStud10.Engine.Diagnostics;
using R4nd0mApps.TddStud10.TestHost;

namespace R4nd0mApps.TddStud10.Engine
{
    public static class EngineLoader
    {
        private static EventHandler runStartingHandler;
        private static EventHandler<string> runStepStartingHandler;
        private static EventHandler runEndedHandler;
        private static EngineFileSystemWatcher efsWatcher;

        public static void Load(DateTime sessionStartTime, string solutionPath, Action runStarting, Action<string> runStepStarting, Action runEnded)
        {
            Logger.I.Log("Loading Engine with solution {0}", solutionPath);

            runStartingHandler = (o, ea) => runStarting();
            runStepStartingHandler = (o, ea) => runStepStarting(ea);
            runEndedHandler = (o, ea) => runEnded();

            Engine.Instance = new Engine(sessionStartTime, solutionPath);
            Engine.Instance.RunStarting += runStartingHandler;
            Engine.Instance.RunStepStarting += runStepStartingHandler;
            Engine.Instance.RunEnded += runEndedHandler;

            efsWatcher = EngineFileSystemWatcher.Create(solutionPath, RunEngine);
        }

        public static void EnableEngine()
        {
            if (efsWatcher == null)
            {
                Logger.I.Log("Engine not loaded. Nothing to enable.");
                return;
            }

            efsWatcher.Enable();
        }

        public static void DisableEngine()
        {
            if (efsWatcher == null)
            {
                Logger.I.Log("Engine not loaded. Nothing to disable.");
                return;
            }

            efsWatcher.Disable();
        }

        public static void UpdateCoverageResults(SequencePointSession seqPtSession, CoverageSession coverageSession, TestDetails testDetails)
        {
            CoverageData.Instance.UpdateCoverageResults(seqPtSession, coverageSession, testDetails);
        }

        public static void Unload()
        {
            Logger.I.Log("Unloading Engine...");

            efsWatcher.Disable();
            efsWatcher.Dispose();

            Engine.Instance.RunStarting -= runStartingHandler;
            Engine.Instance.RunStepStarting -= runStepStartingHandler;
            Engine.Instance.RunEnded -= runEndedHandler;

            runStartingHandler = null;
            runStepStartingHandler = null;
            runEndedHandler = null;
            Engine.Instance = null;
        }

        private static void RunEngine()
        {
            if (Engine.Instance != null)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    if (!Engine.Instance.Start())
                    {
                        return;
                    }

                    var serializer = new XmlSerializer(typeof(SequencePointSession));
                    var res = File.ReadAllText(Engine.Instance.SequencePointStore);
                    SequencePointSession seqPtSession = null;
                    StringReader reader = new StringReader(res);
                    XmlTextReader xmlReader = new XmlTextReader(reader);
                    try
                    {
                        seqPtSession = serializer.Deserialize(xmlReader) as SequencePointSession;
                    }
                    finally
                    {
                        xmlReader.Close();
                        reader.Close();
                    }

                    serializer = new XmlSerializer(typeof(CoverageSession));
                    res = File.ReadAllText(Engine.Instance.CoverageResults);
                    CoverageSession coverageSession = null;
                    reader = new StringReader(res);
                    xmlReader = new XmlTextReader(reader);
                    try
                    {
                        coverageSession = serializer.Deserialize(xmlReader) as CoverageSession;
                    }
                    finally
                    {
                        xmlReader.Close();
                        reader.Close();
                    }

                    TestDetails testDetails = null;
                    res = File.ReadAllText(Engine.Instance.TestResults);
                    reader = new StringReader(res);
                    xmlReader = new XmlTextReader(reader);
                    try
                    {
                        testDetails = TestDetails.Serializer.Deserialize(xmlReader) as TestDetails;
                    }
                    finally
                    {
                        xmlReader.Close();
                        reader.Close();
                    }

                    UpdateCoverageResults(seqPtSession, coverageSession, testDetails);
                }, null);
            }
            else
            {
                Logger.I.Log("Engine is not loaded. Ignoring command.");
            }
        }
    }
}