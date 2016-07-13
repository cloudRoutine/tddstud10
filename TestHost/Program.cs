﻿using Microsoft.FSharp.Control;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using R4nd0mApps.TddStud10.Common.Domain;
using R4nd0mApps.TddStud10.TestExecution.Adapters;
using R4nd0mApps.TddStud10.TestHost.Diagnostics;
using R4nd0mApps.TddStud10.TestRuntime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;

namespace R4nd0mApps.TddStud10.TestHost
{
    public static class Program
    {
        private static bool _debuggerAttached = Debugger.IsAttached;
        private static void LogInfo(string format, params object[] args)
        {
            Logger.I.LogInfo(format, args);
        }

        private static void LogError(string format, params object[] args)
        {
            Logger.I.LogError(format, args);
        }

        [LoaderOptimization(LoaderOptimization.MultiDomain)]
        public static int Main(string[] args)
        {
            LogInfo("TestHost: Entering Main.");
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomainUnhandledException);
            var buildRoot = args[1];
            var codeCoverageStore = args[2];
            var testResultsStore = args[3];
            var discoveredUnitTestsStore = args[4];
            var testFailureInfoStore = args[5];
            var timeFilter = args[6];

            if (args[1] == "discover")
            {
                DiscoverUnitTests(buildRoot, new DateTime(long.Parse(timeFilter)));
                LogInfo("TestHost: Exiting Main.");
                return 0;
            }
            else
            {
                var allTestsPassed = _debuggerAttached
                    ? RunTests(buildRoot, testResultsStore, discoveredUnitTestsStore, testFailureInfoStore)
                    : ExecuteTestWithCoverageDataCollection(() => RunTests(buildRoot, testResultsStore, discoveredUnitTestsStore, testFailureInfoStore), codeCoverageStore);

                LogInfo("TestHost: Exiting Main.");
                return allTestsPassed ? 0 : 1;
            }
        }

        public static void FindAndExecuteForEachAssembly(string buildOutputRoot, DateTime timeFilter, Action<string> action, int? maxThreads = null)
        {
            int madDegreeOfParallelism = maxThreads.HasValue ? maxThreads.Value : Environment.ProcessorCount;
            Logger.I.LogInfo("FindAndExecuteForEachAssembly: Running with {0} threads.", madDegreeOfParallelism);
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".dll", ".exe" };
            Parallel.ForEach(
                Directory.EnumerateFiles(buildOutputRoot, "*").Where(s => extensions.Contains(Path.GetExtension(s))),
                new ParallelOptions { MaxDegreeOfParallelism = madDegreeOfParallelism },
                assemblyPath =>
                {
                    if (!File.Exists(Path.ChangeExtension(assemblyPath, ".pdb")))
                    {
                        return;
                    }

                    var lastWriteTime = File.GetLastWriteTimeUtc(assemblyPath);
                    if (lastWriteTime < timeFilter)
                    {
                        return;
                    }

                    Logger.I.LogInfo("FindAndExecuteForEachAssembly: Running for assembly {0}. LastWriteTime: {1}.", assemblyPath, lastWriteTime.ToLocalTime());
                    action(assemblyPath);
                });
        }

        private static void DiscoverUnitTests(string buildOutputRoot, DateTime timeFilter)
        {
            var testsPerAssembly = new PerDocumentLocationTestCases();
            FindAndExecuteForEachAssembly(
                buildOutputRoot,
                timeFilter,
                (string assemblyPath) =>
                {
                    var asmPath = FilePath.NewFilePath(assemblyPath);
                    var disc = new XUnitTestDiscoverer();
                    disc.TestDiscovered.AddHandler(
                        new FSharpHandler<TestCase>(
                            (o, ea) =>
                            {
                                //var cfp = PathBuilder.rebaseCodeFilePath(rsp, FilePath.NewFilePath(ea.CodeFilePath));
                                var cfp = FilePath.NewFilePath(ea.CodeFilePath);
                                ea.CodeFilePath = cfp.Item;
                                var dl = new DocumentLocation { document = cfp, line = DocumentCoordinate.NewDocumentCoordinate(ea.LineNumber) };
                                var tests = testsPerAssembly.GetOrAdd(dl, _ => new ConcurrentBag<TestCase>());
                                tests.Add(ea);
                            }));
                    disc.DiscoverTests(buildOutputRoot, FilePath.NewFilePath(assemblyPath));
                });

            var discoveredUnitTestsStore = Path.Combine(buildOutputRoot, "Z_discoveredUnitTests.xml");
            testsPerAssembly.Serialize(FilePath.NewFilePath(discoveredUnitTestsStore));
        }

        private static bool ExecuteTestWithCoverageDataCollection(Func<bool> runTests, string codeCoverageStore)
        {
            bool allTestsPassed = true;
            var ccServer = new CoverageDataCollector();
            using (ServiceHost serviceHost = new ServiceHost(ccServer))
            {
                LogInfo("TestHost: Created Service Host.");
                string address = Marker.CreateCodeCoverageDataCollectorEndpointAddress();
                NetNamedPipeBinding binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
                serviceHost.AddServiceEndpoint(typeof(ICoverageDataCollector), binding, address);
                serviceHost.Open();
                LogInfo("TestHost: Opened _channel.");

                allTestsPassed = runTests();
                LogInfo("TestHost: Finished running test cases.");
            }
            ccServer.CoverageData.Serialize(FilePath.NewFilePath(codeCoverageStore));
            return allTestsPassed;
        }

        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogError("Exception thrown in InvokeEngine: {0}.", e.ExceptionObject);
        }

        private static bool RunTests(string buildRoot, string testResultsStore, string discoveredUnitTestsStore, string testFailureInfoStore)
        {
            Stopwatch stopWatch = new Stopwatch();

            LogInfo("TestHost executing tests...");
            stopWatch.Start();
            var testResults = new PerTestIdResults();
            var testFailureInfo = new PerDocumentLocationTestFailureInfo();
            var perAssemblyTestIds = PerDocumentLocationTestCases.Deserialize(FilePath.NewFilePath(discoveredUnitTestsStore));
            var tests = from dc in perAssemblyTestIds.Keys
                        from t in perAssemblyTestIds[dc]
                        group t by FilePath.NewFilePath(t.Source);
            Parallel.ForEach(
                tests,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                test =>
                {
                    LogInfo("Executing tests in {0}: Start.", test.Key);
                    var exec = new XUnitTestExecutor();
                    exec.TestExecuted.AddHandler(
                        new FSharpHandler<TestResult>(
                            (o, ea) =>
                            {
                                NoteTestResults(testResults, ea);
                                NoteTestFailureInfo(testFailureInfo, ea);
                            }));
                    exec.ExecuteTests(buildRoot, test);
                    LogInfo("Executing tests in {0}: Done.", test.Key);
                });

            if (!_debuggerAttached)
            {
                testResults.Serialize(FilePath.NewFilePath(testResultsStore));
                testFailureInfo.Serialize(FilePath.NewFilePath(testFailureInfoStore));
            }

            stopWatch.Stop();
            var ts = stopWatch.Elapsed;
            var elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        ts.Hours, ts.Minutes, ts.Seconds,
                        ts.Milliseconds / 10);
            LogInfo("Done TestHost executing tests! [" + elapsedTime + "]");
            LogInfo("");

            var rrs =
                from tr in testResults
                from rr in tr.Value
                where rr.Outcome == TestOutcome.Failed
                select rr;

            return !rrs.Any();
        }

        private static void NoteTestFailureInfo(PerDocumentLocationTestFailureInfo pdtfi, TestResult tr)
        {
            LogInfo("Noting Test Failure Info: {0} - {1}", tr.DisplayName, tr.Outcome);

            TestFailureInfoExtensions.create(tr)
            .Aggregate(
                pdtfi,
                (acc, e) =>
                {
                    acc
                    .GetOrAdd(e.Item1, _ => new ConcurrentBag<TestFailureInfo>())
                    .Add(e.Item2);
                    return acc;
                });
        }

        private static void NoteTestResults(PerTestIdResults testResults, TestResult tr)
        {
            LogInfo("Noting Test Result: {0} - {1}", tr.DisplayName, tr.Outcome);

            var testId = new TestId(
                FilePath.NewFilePath(tr.TestCase.Source),
                new DocumentLocation(
                    FilePath.NewFilePath(tr.TestCase.CodeFilePath),
                    DocumentCoordinate.NewDocumentCoordinate(tr.TestCase.LineNumber)));

            var results = testResults.GetOrAdd(testId, _ => new ConcurrentBag<TestResult>());
            results.Add(tr);
        }
    }
}
