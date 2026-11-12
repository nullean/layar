module Paths

open System
open System.IO

let ToolName = "Layar"
let Repository = "nullean/layar"
/// The framework the public API diff reads from the previous release and from this build: the oldest the libraries
/// target, which every release ships
let MainTFM = "net10.0"
let SignKey = ""

let ValidateAssemblyName = true
let IncludeGitHashInInformational = true
let GenerateApiChanges = true

/// Package id -> (project directory under src/, which is also its folder under .artifacts/bin; assembly name).
/// The three differ for most packages here (src/Laya.Onnx packs Layar.Onnx.dll as Layar.Onnx), and the package
/// targets need the directory to diff and the assembly name to validate only the package's own dll.
let Packages =
    dict [
        "Layar", ("Layar.Core", "Layar.Core")
        "Layar.Tokenization", ("Laya.Tokenization", "Layar.Tokenization")
        "Layar.Onnx", ("Laya.Onnx", "Layar.Onnx")
        "Layar.TorchSharp", ("Laya.TorchSharp", "Layar.TorchSharp")
        "Layar.Onnx.Cpu", ("Layar.Onnx.Cpu", "Layar.Onnx.Cpu")
        "Layar.TorchSharp.Cpu", ("Layar.TorchSharp.Cpu", "Layar.TorchSharp.Cpu")
        "Layar.Cli", ("Laya.Cli", "Laya.Cli")
    ]

let packageInfo (packageId: string) =
    match Packages.TryGetValue packageId with
    | true, info -> info
    | _ -> failwithf "%s is not listed in Paths.Packages; add its project directory and assembly name" packageId

let Root =
    let mutable dir = DirectoryInfo(".")
    while dir.GetFiles("*.slnx").Length = 0 do dir <- dir.Parent
    Environment.CurrentDirectory <- dir.FullName
    dir

let RootRelative path = Path.GetRelativePath(Root.FullName, path)

/// The build output the API diff reads: a library's MainTFM folder, or the one folder of a project that targets a single
/// framework (a dotnet tool, net11.0 alone)
let ApiDiffOutput (projectDirectory: string) =
    let library = $".artifacts/bin/{projectDirectory}/release_{MainTFM}"
    if Directory.Exists library then library else $".artifacts/bin/{projectDirectory}/release"

let Output = DirectoryInfo(Path.Combine(Root.FullName, "build", "output"))

let ToolProject = DirectoryInfo(Path.Combine(Root.FullName, "src", ToolName))
