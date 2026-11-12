module Program

open System.IO
open Nullean.Make.Fs
open Proc.Fs

/// The build's targets; `dotnet run --project build/scripts -- --help` lists them with these descriptions
type Target =
    /// clean known output locations
    | Clean
    /// Run build
    | Build
    /// Runs build then tests
    | Test
    /// Runs build, then creates and validates the packages and reports public API changes
    | Pack
    /// Runs tests, then creates and validates the packages: exactly what CI runs
    | Ci
    /// Pack the NuGet packages into build/output
    | Generatepackages
    /// Validate each package's own assembly
    | Validatepackages
    /// Report public API changes against the previous release
    | Generateapichanges
    /// Build documentation and serve locally for preview
    | Docs

/// build/output, relative to the repository root
let private output = Paths.RootRelative Paths.Output.FullName

/// The packed .nupkg files with their package ids, newest first
let private packages version =
    Paths.Output.GetFiles("*.nupkg")
    |> Seq.sortByDescending (fun f -> f.CreationTimeUtc)
    |> Seq.map (fun f -> f, Path.GetFileNameWithoutExtension(f.Name).Replace("." + version, ""))
    |> Seq.toList

/// Every project but this build itself: `dotnet clean` on the solution would delete the assemblies the running
/// build still loads lazily and fail the next target
let private projectsToClean () =
    ["src"; "tests"; "examples"]
    |> List.collect (fun d -> Directory.EnumerateFiles(d, "*.?sproj", SearchOption.AllDirectories) |> Seq.toList)

[<EntryPoint>]
let main argv =
    let app = MakeApp<Target>("dotnet run --project build/scripts --", None)
    let port = app.Option<int>("--port", desc = "Port to use when serving documentation", defaultValue = 8000)

    // `-s` skips what a target `requires`, never what it `composes`: build always cleans first
    app.Bind <| function
        | Clean -> target [] {
              if Paths.Output.Exists then Paths.Output.Delete true
              for project in projectsToClean () do
                  sh $"dotnet clean {project} -c Release -v q" }
        | Build -> command {
              composes [Clean]
              executes (shell { sh $"dotnet build -c Release" }) }
        | Test -> target [Build] { sh $"dotnet run --project tests/Laya.Tests/Laya.Tests.csproj -c Release" }
        | Pack -> command { requires [Build]; composes [Generatepackages; Validatepackages; Generateapichanges] }
        | Ci -> command { requires [Test]; composes [Generatepackages; Validatepackages; Generateapichanges] }
        | Generatepackages -> target [] { sh $"dotnet pack -c Release -o {output}" }
        | Validatepackages -> target [] {
              let informational = Versions.informational.Value
              for package, id in packages Versions.current.Value do
                  // only the package's own assembly: Layar.Cli bundles its dependencies (Google.Protobuf, ...) as a tool
                  let _, assembly = Paths.packageInfo id
                  let package = Paths.RootRelative package.FullName
                  sh $"dotnet nupkg-validator validate {package} -a {assembly} -v {informational} -t {output}" }
        | Generateapichanges -> target [] {
              let version = Versions.current.Value
              for _, id in packages version do
                  printfn "(%s) -> (%s)" id version
                  let report = Path.Combine(output, $"breaking-changes-{id}.md")
                  let projectDirectory, assembly = Paths.packageInfo id
                  // quoted: an unquoted | would pipe
                  sh $"dotnet assembly-differ diff 'previous-nuget|{id}|{version}|{Paths.MainTFM}' 'directory|.artifacts/bin/{projectDirectory}/release' -a --target {assembly} -f github-comment --output {report}" }
        | Docs -> target [] {
              Documentation.build ()
              Documentation.serve port.Value }

    app.RunAsync(argv).GetAwaiter().GetResult()
