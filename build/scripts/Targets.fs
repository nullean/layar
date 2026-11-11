module Targets

open Argu
open System
open System.IO
open Bullseye
open CommandLine
open Fake.Tools.Git
open ProcNet


let exec binary args =
    Proc.Exec (binary, args |> List.toArray)

let private restoreTools = lazy(exec "dotnet" ["tool"; "restore"])
let private currentVersion =
    lazy(
        restoreTools.Value |> ignore
        // the same settings as Directory.Build.props, so validation expects the version the packages carry
        let r = Proc.Start("dotnet", "minver", "-p", "canary.0", "-m", "0.1", "-t", "v")
        let o = r.ConsoleOut |> Seq.find (fun l -> not(l.Line.StartsWith("MinVer:")))
        o.Line
    )
let private currentVersionInformational =
    lazy(
        match Paths.IncludeGitHashInInformational with
        | false -> currentVersion.Value
        | true -> sprintf "%s+%s" currentVersion.Value (Information.getCurrentSHA1( "."))
    )

let private clean (arguments:ParseResults<Arguments>) =
    if (Paths.Output.Exists) then Paths.Output.Delete (true)
    // clean every project but this build itself: `dotnet clean` on the solution would delete the assemblies the
    // running build still loads lazily (Fake, Proc) and fail the next target
    ["src"; "tests"; "examples"]
    |> Seq.collect (fun d -> Directory.EnumerateFiles(d, "*.?sproj", SearchOption.AllDirectories))
    |> Seq.iter (fun p -> exec "dotnet" ["clean"; p; "-c"; "Release"; "-v"; "q"] |> ignore)

let private build (arguments:ParseResults<Arguments>) = exec "dotnet" ["build"; "-c"; "Release"] |> ignore

let private test (arguments:ParseResults<Arguments>) =
    exec "dotnet" ["run"; "--project"; "tests/Laya.Tests/Laya.Tests.csproj"; "-c"; "Release"] |> ignore

let private generatePackages (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative Paths.Output.FullName
    exec "dotnet" ["pack"; "-c"; "Release"; "-o"; output] |> ignore

let private validatePackages (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative <| Paths.Output.FullName
    let currentVersion = currentVersion.Value
    let nugetPackages =
        Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(fun f -> f.CreationTimeUtc)

    let args = ["-v"; currentVersionInformational.Value; "-t"; output]
    nugetPackages
    |> Seq.iter (fun p ->
        let packageId = Path.GetFileNameWithoutExtension(p.Name).Replace("." + currentVersion, "")
        // only the package's own assembly: Layar.Cli bundles its dependencies (Google.Protobuf, ...) as a tool
        let _, assembly = Paths.packageInfo packageId
        exec "dotnet" (["nupkg-validator"; Paths.RootRelative p.FullName; "-a"; assembly] @ args) |> ignore
    )


let private generateApiChanges (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative <| Paths.Output.FullName
    let currentVersion = currentVersion.Value
    let nugetPackages =
        Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(_.CreationTimeUtc)
        |> Seq.map (fun p -> Path.GetFileNameWithoutExtension(Paths.RootRelative p.FullName).Replace("." + currentVersion, ""))
    nugetPackages
    |> Seq.iter(fun p ->
        printfn "(%s) -> (%s)" p currentVersion
        let outputFile =
            let f = sprintf "breaking-changes-%s.md" p
            Path.Combine(output, f)
        let args =
            let projectDirectory, assembly = Paths.packageInfo p
            [
                "assembly-differ"
                (sprintf "previous-nuget|%s|%s|%s" p currentVersion Paths.MainTFM);
                (sprintf "directory|.artifacts/bin/%s/release" projectDirectory);
                "-a"; "true"; "--target"; assembly; "-f"; "github-comment"; "--output"; outputFile
            ]

        exec "dotnet" args |> ignore
    )

let Setup (parsed:ParseResults<Arguments>) (subCommand:Arguments) =
    let step (name:string) action = Targets.Target(name, new Action(fun _ -> action(parsed)))

    let cmd (name:string) commandsBefore steps action =
        let singleTarget = (parsed.TryGetResult SingleTarget |> Option.defaultValue false)
        let deps =
            match (singleTarget, commandsBefore) with
            | (true, _) -> []
            | (_, Some d) -> d
            | _ -> []
        let steps = steps |> Option.defaultValue []
        Targets.Target(name, deps @ steps, Action(action))

    step Clean.Name clean
    cmd Build.Name None (Some [Clean.Name]) <| fun _ -> build parsed

    cmd Test.Name (Some [Build.Name;]) None <| fun _ -> test parsed

    cmd Pack.Name
        (Some [Build.Name;])
        (Some [GeneratePackages.Name; ValidatePackages.Name; GenerateApiChanges.Name])
        ignore
    cmd Ci.Name
        (Some [Test.Name;])
        (Some [GeneratePackages.Name; ValidatePackages.Name; GenerateApiChanges.Name])
        ignore

    step GeneratePackages.Name generatePackages
    step ValidatePackages.Name validatePackages
    step GenerateApiChanges.Name generateApiChanges
    cmd Docs.Name None None
        <| fun _ ->
            Documentation.build ()
            let port = parsed.TryGetResult Port |> Option.defaultValue 8000
            Documentation.serve port
