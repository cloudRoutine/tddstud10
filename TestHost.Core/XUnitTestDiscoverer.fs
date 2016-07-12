﻿namespace R4nd0mApps.TddStud10.TestExecution.Adapters

open R4nd0mApps.TddStud10.TestExecution
open R4nd0mApps.TddStud10.Common.Domain
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter

type XUnitTestDiscoverer() = 
    let dc = TestPlatformExtensions.createDiscoveryContext()
    let ml = TestPlatformExtensions.createMessageLogger()
    let ds = TestPlatformExtensions.createDiscoverySink
    let testDiscovered = new Event<_>()
    member public t.TestDiscovered = testDiscovered.Publish
    member public t.DiscoverTests(binDir, FilePath asm) = 
        let td = binDir |> TestPlatformExtensions.loadTestAdapter :?> ITestDiscoverer
        td.DiscoverTests([ asm ], dc, ml, ds testDiscovered.Trigger)
