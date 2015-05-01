﻿namespace R4nd0mApps.TddStud10.TestHost.Diagnostics

open System
open Microsoft.Diagnostics.Tracing

[<Sealed>]
[<EventSource(Name = "R4nd0mApps-TddStud10-TestHost")>]
type Logger() = 
    inherit EventSource()
    static let i = new Logger()
    static member I = i
    
    [<Event(1, Level = EventLevel.Informational)>]
    member public this.LogInfo(message : string) = base.WriteEvent(1, message)
    
    [<Event(2, Level = EventLevel.Error)>]
    member public this.LogError(message : string) = base.WriteEvent(2, message)
    
    [<NonEvent>]
    member public this.LogInfo(format : string, [<ParamArray>] args : Object array) = 
        if (base.IsEnabled(EventLevel.Informational, EventKeywords.All)) then this.LogInfo(String.Format(format, args))
    
    [<NonEvent>]
    member public this.LogError(format : string, [<ParamArray>] args : Object array) = 
        if (base.IsEnabled(EventLevel.Error, EventKeywords.All)) then this.LogError(String.Format(format, args))
    
    [<NonEvent>]
    static member private invokeLogf level logFn = 
        let l = 
            if Logger.I.IsEnabled(level, EventKeywords.All) then logFn
            else ignore
        Printf.ksprintf l
    
    [<NonEvent>]
    static member public logInfof format = Logger.invokeLogf EventLevel.Informational Logger.I.LogInfo format
    
    [<NonEvent>]
    static member public logErrorf format = Logger.invokeLogf EventLevel.Error Logger.I.LogError format
