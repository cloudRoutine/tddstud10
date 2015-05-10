﻿namespace R4nd0mApps.TddStud10.Engine.Core

open System
open R4nd0mApps.TddStud10.Engine.Diagnostics

type RunStateTracker() = 
    let mutable state = Initial
    let runStateChanged = new Event<RunState>()

    let logAndReturnBack s ev =
        Logger.logInfof "Run Tracker State Machine: Cannot handle event '%A' in state '%A'" ev Initial
        s
            
    let transitionState = 
        function 
        | _, RunStarting -> Initial

        | s, RunError(RunStepFailedException ({name = _; kind = _; status = Failed; addendum = _; runData = _ })) -> s
        | _, RunError(_) -> EngineError
        
        | _, RunStepError(_, Aborted) -> EngineErrorDetected
        | _, RunStepEnded(_, Aborted) -> EngineError

        | Initial, RunStepStarting(Build) -> FirstBuildRunning
        | Initial, ev -> logAndReturnBack Initial ev 

        | EngineErrorDetected as s, ev -> logAndReturnBack s ev 
        
        | EngineError as s, ev -> logAndReturnBack s ev
        
        | FirstBuildRunning, RunStepEnded(Build, Succeeded) -> BuildPassed
        | FirstBuildRunning, RunStepError(Build, Failed) -> BuildFailureDetected
        | FirstBuildRunning as s, ev -> logAndReturnBack FirstBuildRunning ev
        
        | BuildFailureDetected, RunStepEnded(Build, Failed) -> BuildFailed
        | BuildFailureDetected as s, ev -> logAndReturnBack s ev
        
        | BuildFailed as s, ev -> logAndReturnBack s ev
        
        | TestFailureDetected, RunStepEnded(Test, Failed) -> TestFailed
        | TestFailureDetected as s, ev -> logAndReturnBack s ev
        
        | TestFailed as s, ev -> logAndReturnBack s ev
        
        | BuildRunning, RunStepError(Build, Failed) -> BuildFailureDetected
        | BuildRunning, RunStepEnded(Build, Succeeded) -> BuildPassed
        | BuildRunning as s, ev -> logAndReturnBack s ev
        
        | BuildPassed, RunStepStarting(Build) -> BuildRunning
        | BuildPassed, RunStepStarting(Test) -> TestRunning
        | BuildPassed as s, ev -> logAndReturnBack s ev
        
        | TestRunning, RunStepError(Test, Failed) -> TestFailureDetected
        | TestRunning, RunStepEnded(Test, Succeeded) -> TestPassed
        | TestRunning as s, ev -> logAndReturnBack s ev
        
        | TestPassed as s, ev -> logAndReturnBack s ev
    
    let transitionStateAndRaiseEvent ev = 
        state <- transitionState (state, ev)
        Common.safeExec (fun () -> runStateChanged.Trigger(state))
    
    member t.State = state
    member public t.RunStateChanged = runStateChanged.Publish
    member public t.OnRunStarting(ea : RunData) = transitionStateAndRaiseEvent RunStarting
    member public t.OnRunStepStarting(ea : RunStepEventArg) = transitionStateAndRaiseEvent (RunStepStarting ea.kind)
    member public t.OnRunStepError(ea : RunStepEndEventArg) = 
        transitionStateAndRaiseEvent (RunStepError(ea.kind, ea.status))
    member public t.OnRunStepEnd(ea : RunStepEndEventArg) = 
        transitionStateAndRaiseEvent (RunStepEnded(ea.kind, ea.status))
    member public t.OnRunError(ea : Exception) = transitionStateAndRaiseEvent (RunError ea)
    member public t.OnRunEnd(ea : RunData) = ()
