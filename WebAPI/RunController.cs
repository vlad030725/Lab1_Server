using Core;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace WebAPI;

[Route("api/[controller]")]
[ApiController]
public class RunController : ControllerBase
{
    [HttpGet("last-png")]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetLastPng()
    {
        string outputDirectory = GetHeatmapsDirectory();
        if (!Directory.Exists(outputDirectory))
        {
            return NotFound(new { error = "Heatmaps directory was not found." });
        }

        string? lastFile = Directory
            .GetFiles(outputDirectory, "heatmap_*.png")
            .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (lastFile is null)
        {
            return NotFound(new { error = "No generated PNG files were found." });
        }

        byte[] pngBytes = System.IO.File.ReadAllBytes(lastFile);
        return File(pngBytes, "image/png", Path.GetFileName(lastFile));
    }

    [HttpPost]
    public ActionResult<RunResponse> Run([FromBody] RunRequest request)
    {
        var solver = new ExplicitHeatSolver();
        SimulationConfig config = BuildSimulationConfig(request);

        try
        {
            SimulationResult result;
            int usedThreads = 1;
            var stopwatch = Stopwatch.StartNew();

            if (request.RunInParallel)
            {
                result = solver.RunParallel(config, request.MaxDegreeOfParallelism, out usedThreads);
            }
            else
            {
                result = solver.RunSequential(config);
            }

            stopwatch.Stop();

            string outputDirectory = GetHeatmapsDirectory();
            Directory.CreateDirectory(outputDirectory);

            string outputPath = Path.Combine(
                outputDirectory,
                $"heatmap_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png");

            PngWriter.SaveHeatmapPng(outputPath, result.FinalField, result.Nx, result.Ny);

            return Ok(new RunResponse
            {
                Nx = result.Nx,
                Ny = result.Ny,
                H = result.H,
                Dt = result.Dt,
                Steps = result.Steps,
                UsedThreads = usedThreads,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                FinalField = result.FinalField
            });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("compare")]
    public ActionResult<CompareRunResponse> Compare([FromBody] RunRequest request)
    {
        var solver = new ExplicitHeatSolver();
        SimulationConfig config = BuildSimulationConfig(request);

        try
        {
            var seqWatch = Stopwatch.StartNew();
            SimulationResult sequential = solver.RunSequential(config);
            seqWatch.Stop();

            var parWatch = Stopwatch.StartNew();
            SimulationResult parallel = solver.RunParallel(config, request.MaxDegreeOfParallelism, out int usedThreads);
            parWatch.Stop();

            string outputDirectory = GetHeatmapsDirectory();
            Directory.CreateDirectory(outputDirectory);

            string outputPath = Path.Combine(
                outputDirectory,
                $"heatmap_compare_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png");

            PngWriter.SaveHeatmapPng(outputPath, parallel.FinalField, parallel.Nx, parallel.Ny);

            double seqMs = seqWatch.Elapsed.TotalMilliseconds;
            double parMs = parWatch.Elapsed.TotalMilliseconds;
            double speedup = parMs > 0 ? seqMs / parMs : 0.0;
            double efficiency = usedThreads > 0 ? speedup / usedThreads : 0.0;

            return Ok(new CompareRunResponse
            {
                Nx = parallel.Nx,
                Ny = parallel.Ny,
                H = parallel.H,
                Dt = parallel.Dt,
                Steps = parallel.Steps,
                UsedThreads = usedThreads,
                SequentialElapsedMilliseconds = seqMs,
                ParallelElapsedMilliseconds = parMs,
                Speedup = speedup,
                Efficiency = efficiency,
                MaxAbsoluteDifference = GetMaxAbsoluteDifference(sequential.FinalField, parallel.FinalField),
                FinalField = parallel.FinalField
            });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static string GetHeatmapsDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "heatmaps");

    private static SimulationConfig BuildSimulationConfig(RunRequest request) => new()
    {
        Width = request.Width,
        Height = request.Height,
        H = request.H,
        Dt = request.Dt,
        TotalTime = request.TotalTime,
        Alpha = request.Alpha,
        InitialTemperature = request.InitialTemperature,
        InitialPointTemperatures = request.InitialPoints
            .Select(point => point.ToCore())
            .ToArray(),
        G1_Left = request.G1_Left.ToBoundarySpec(),
        G2_Right = request.G2_Right.ToBoundarySpec(),
        G3_Bottom = request.G3_Bottom.ToBoundarySpec(),
        G4_Top = request.G4_Top.ToBoundarySpec()
    };

    private static double GetMaxAbsoluteDifference(double[] a, double[] b)
    {
        int n = Math.Min(a.Length, b.Length);
        double max = 0.0;
        for (int i = 0; i < n; i++)
        {
            double diff = Math.Abs(a[i] - b[i]);
            if (diff > max) max = diff;
        }

        return max;
    }
}

public sealed class RunRequest
{
    public double Width { get; init; } = 1.0;
    public double Height { get; init; } = 1.0;
    public double H { get; init; } = 0.01;
    public double Dt { get; init; } = 5e-5;
    public double TotalTime { get; init; } = 0.2;
    public double Alpha { get; init; } = 0.5;
    public double InitialTemperature { get; init; } = 20.0;
    public IReadOnlyList<InitialPointRequest> InitialPoints { get; init; } =
        Array.Empty<InitialPointRequest>();
    public bool RunInParallel { get; init; }
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
    public BoundaryRequest G1_Left { get; init; } = BoundaryRequest.Neumann(0.0);
    public BoundaryRequest G2_Right { get; init; } = BoundaryRequest.Neumann(0.0);
    public BoundaryRequest G3_Bottom { get; init; } = BoundaryRequest.Dirichlet(100.0);
    public BoundaryRequest G4_Top { get; init; } = BoundaryRequest.Dirichlet(20.0);
}

public sealed class BoundaryRequest
{
    public BoundaryKind Kind { get; init; } = BoundaryKind.Neumann;
    public double Value1 { get; init; }
    public double Value2 { get; init; }

    public BoundarySpec ToBoundarySpec() => new(Kind, Value1, Value2);

    public static BoundaryRequest Dirichlet(double temperature) => new()
    {
        Kind = BoundaryKind.Dirichlet,
        Value1 = temperature
    };

    public static BoundaryRequest Neumann(double derivative) => new()
    {
        Kind = BoundaryKind.Neumann,
        Value1 = derivative
    };
}

public sealed class InitialPointRequest
{
    public int I { get; init; }
    public int J { get; init; }
    public double Temperature { get; init; }

    public InitialPointTemperature ToCore() => new(I, J, Temperature);
}

public sealed class RunResponse
{
    public required int Nx { get; init; }
    public required int Ny { get; init; }
    public required double H { get; init; }
    public required double Dt { get; init; }
    public required int Steps { get; init; }
    public required int UsedThreads { get; init; }
    public required double ElapsedMilliseconds { get; init; }
    public required double[] FinalField { get; init; }
}

public sealed class CompareRunResponse
{
    public required int Nx { get; init; }
    public required int Ny { get; init; }
    public required double H { get; init; }
    public required double Dt { get; init; }
    public required int Steps { get; init; }
    public required int UsedThreads { get; init; }
    public required double SequentialElapsedMilliseconds { get; init; }
    public required double ParallelElapsedMilliseconds { get; init; }
    public required double Speedup { get; init; }
    public required double Efficiency { get; init; }
    public required double MaxAbsoluteDifference { get; init; }
    public required double[] FinalField { get; init; }
}
