using System.Diagnostics;
using System.Text.Json;
using Core;

const InputMode inputMode = InputMode.JsonFile;
const string inputJsonFileName = "response.json";

static double GetArg(string[] args, string name, double def)
{
    int i = Array.IndexOf(args, name);
    if (i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], out var v)) return v;
    return def;
}

static int GetArgInt(string[] args, string name, int def)
{
    int i = Array.IndexOf(args, name);
    if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v)) return v;
    return def;
}

static string GetArgStr(string[] args, string name, string def)
{
    int i = Array.IndexOf(args, name);
    if (i >= 0 && i + 1 < args.Length) return args[i + 1];
    return def;
}

var dt = GetArg(args, "--dt", 5e-5);
var h = GetArg(args, "--h", 0.01);
var total = GetArg(args, "--time", 0.2);
var threads = GetArgInt(args, "--threads", Environment.ProcessorCount);
var outPng = GetArgStr(args, "--png", "final.png");
var mode = GetArgStr(args, "--mode", "both"); // seq|par|both
var tBottom = GetArg(args, "--bottomT", 100.0);
var tTop = GetArg(args, "--topT", 20.0);

SimulationResult? result = inputMode switch
{
    InputMode.Direct => RunSimulation(dt, h, total, threads, mode, tBottom, tTop),
    InputMode.JsonFile => LoadSimulationResultFromJson(inputJsonFileName),
    _ => throw new InvalidOperationException($"Unsupported input mode: {inputMode}")
};

if (result is null) return;

PngWriter.SaveHeatmapPng(outPng, result.FinalField, result.Nx, result.Ny);
Console.WriteLine($"Saved heatmap: {outPng}");

static SimulationResult? RunSimulation(
    double dt,
    double h,
    double total,
    int threads,
    string mode,
    double tBottom,
    double tTop)
{
    // Вариант 5: Г1=II, Г2=II, Г3=I, Г4=I (Г1/Г2 считаем "слева/справа", Г3/Г4 "низ/верх")
    var cfg = new SimulationConfig
    {
        Width = 1.0,
        Height = 1.0,
        H = h,
        Dt = dt,
        TotalTime = total,
        Alpha = 0.5,
        InitialTemperature = 20.0,
        InitialPointTemperatures = new[]
        {
            new InitialPointTemperature(90, 90, 1000.0)
        },
        G1_Left = BoundarySpec.Neumann(0.0),
        G2_Right = BoundarySpec.Neumann(0.0),
        G3_Bottom = BoundarySpec.Dirichlet(tBottom),
        G4_Top = BoundarySpec.Dirichlet(tTop),
    };

    var solver = new ExplicitHeatSolver();

    // Прогрев JIT, чтобы замеры были честнее.
    _ = solver.RunSequential(cfg);

    double tSeq = double.NaN;
    double tPar = double.NaN;
    SimulationResult? last = null;

    if (mode is "seq" or "both")
    {
        var sw = Stopwatch.StartNew();
        last = solver.RunSequential(cfg);
        sw.Stop();
        tSeq = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"SEQ: {tSeq:F2} ms");
    }

    if (mode is "par" or "both")
    {
        var sw = Stopwatch.StartNew();
        int used;
        last = solver.RunParallel(cfg, threads, out used);
        sw.Stop();
        tPar = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine($"PAR (threads={used}): {tPar:F2} ms");

        if (!double.IsNaN(tSeq))
        {
            double s = tSeq / tPar;
            double e = s / used;
            Console.WriteLine($"Speedup S = {s:F3}");
            Console.WriteLine($"Efficiency E = {e:F3}");
        }
    }

    return last;
}

static SimulationResult LoadSimulationResultFromJson(string fileName)
{
    string jsonPath = Path.Combine(AppContext.BaseDirectory, fileName);

    if (!File.Exists(jsonPath))
    {
        throw new FileNotFoundException($"JSON file was not found рядом с exe: {jsonPath}");
    }

    string json = File.ReadAllText(jsonPath);
    var dto = JsonSerializer.Deserialize<RunResponseDto>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (dto is null)
    {
        throw new InvalidOperationException($"Failed to parse JSON file: {jsonPath}");
    }

    if (dto.FinalField is null || dto.FinalField.Length != dto.Nx * dto.Ny)
    {
        throw new InvalidOperationException(
            $"Invalid finalField length in JSON. Expected {dto.Nx * dto.Ny}, got {dto.FinalField?.Length ?? 0}.");
    }

    Console.WriteLine($"Loaded result from JSON: {jsonPath}");
    Console.WriteLine($"Grid: {dto.Nx}x{dto.Ny}, steps={dto.Steps}, threads={dto.UsedThreads}");

    return new SimulationResult(
        dto.Nx,
        dto.Ny,
        dto.H,
        dto.Dt,
        dto.Steps,
        dto.FinalField);
}

enum InputMode
{
    Direct,
    JsonFile
}

sealed class RunResponseDto
{
    public int Nx { get; init; }
    public int Ny { get; init; }
    public double H { get; init; }
    public double Dt { get; init; }
    public int Steps { get; init; }
    public int UsedThreads { get; init; }
    public double[]? FinalField { get; init; }
}
