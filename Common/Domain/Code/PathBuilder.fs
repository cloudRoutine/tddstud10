namespace R4nd0mApps.TddStud10.Common

open System
open System.IO
open R4nd0mApps.TddStud10
open R4nd0mApps.TddStud10.Common.Domain

module PathBuilder = 
    let snapShotRoot = Constants.SnapshotRoot
    
    let private makeSlnParentDirName slnPath = 
        match Path.GetFileName(Path.GetDirectoryName(slnPath)) with
        | "" -> Path.GetFileNameWithoutExtension(slnPath)
        | dn -> dn
    
    let makeSlnSnapshotPath (FilePath slnPath) = 
        let slnFileName = Path.GetFileName(slnPath)
        let slnParentDirName = makeSlnParentDirName slnPath
        FilePath(Path.Combine(snapShotRoot, slnParentDirName, slnFileName))
    
    let makeSlnBuildRoot (FilePath slnPath) = 
        let slnParentDirName = makeSlnParentDirName slnPath
        FilePath(Path.Combine(snapShotRoot, slnParentDirName + ".out"))
    
    let rebaseCodeFilePath rsp (FilePath p) = 
        let (FilePath slnPath) = rsp.solutionPath
        let (FilePath slnSnapPath) = rsp.solutionSnapshotPath
        p.ToUpperInvariant()
         .Replace(Path.GetDirectoryName(slnSnapPath).ToUpperInvariant(), 
                  Path.GetDirectoryName(slnPath).ToUpperInvariant()) |> FilePath
