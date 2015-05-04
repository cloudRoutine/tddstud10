﻿module R4nd0mApps.TddStud10.Engine.TestDoubles

open R4nd0mApps.TddStud10.Engine.Core
open R4nd0mApps.TddStud10.TestHost

type public TestHost(cancelStep : int) = 
    let mutable callCount = 0
    interface IRunExecutorHost with
        member this.CanContinue() = 
            callCount <- callCount + 1
            callCount <= cancelStep

type StepFunc(throwException) = 
    new() = StepFunc(false)
    member val Called = false with get, set
    member val CalledWith = None with get, set
    member val ReturningWith = None with get, set
    member public t.Func (h : IRunExecutorHost) name events (rd : RunData) : RunData = 
        t.Called <- true
        t.CalledWith <- Some(rd.GetHashCode())
        if throwException then failwith "Step threw some exception"
        let retRd = { rd with sequencePoints = Some(new SequencePoints()) }
        t.ReturningWith <- Some(retRd.GetHashCode())
        retRd

let inline RS(sf : StepFunc) = 
    { name = RunStepName(sf.GetHashCode().ToString())
      func = sf.Func }
