using Core;
using System.Diagnostics;

namespace Core;

public sealed class ExplicitHeatSolver
{
    //Условие устойчивости
    public static double GetStableDtMax(double alpha, double h)
    {
        if (alpha <= 0) throw new ArgumentOutOfRangeException(nameof(alpha));
        if (h <= 0) throw new ArgumentOutOfRangeException(nameof(h));
        return (h * h) / (4.0 * alpha);
    }

    /// <summary>
    /// Запуск последовательного алгоритма
    /// </summary>
    /// <param name="cfg">Входные данные</param>
    /// <returns></returns>
    public SimulationResult RunSequential(SimulationConfig cfg) =>
        Run(cfg, parallel: false, maxDegree: 1, out _);

    /// <summary>
    /// Запуск параллельного алгоритма
    /// </summary>
    /// <param name="cfg">Входные данные</param>
    /// <param name="maxDegreeOfParallelism">Количество потоков</param>
    /// <param name="usedThreads"></param>
    /// <returns></returns>
    public SimulationResult RunParallel(SimulationConfig cfg, int maxDegreeOfParallelism, out int usedThreads) =>
        Run(cfg, parallel: true, maxDegree: maxDegreeOfParallelism, out usedThreads);

    private SimulationResult Run(SimulationConfig cfg, bool parallel, int maxDegree, out int usedThreads)
    {
        Validate(cfg);

        int nx = (int)Math.Round(cfg.Width / cfg.H) + 1;
        int ny = (int)Math.Round(cfg.Height / cfg.H) + 1;
        ValidateInitialPointTemperatures(cfg, nx, ny);

        int steps = (int)Math.Ceiling(cfg.TotalTime / cfg.Dt);
        double r = cfg.Alpha * cfg.Dt / (cfg.H * cfg.H);

        var cur = new double[nx * ny];
        var next = new double[nx * ny];

        Array.Fill(cur, cfg.InitialTemperature);
        ApplyInitialPointTemperatures(cur, nx, cfg.InitialPointTemperatures);
        ApplyBoundaries(cur, nx, ny, cfg);

        usedThreads = parallel ? Math.Max(1, maxDegree) : 1;

        var po = new ParallelOptions { MaxDegreeOfParallelism = usedThreads };

        for (int step = 1; step <= steps; step++)
        {
            if (parallel)
            {
                Parallel.For(1, ny - 1, po, j =>
                {
                    int row = j * nx;
                    for (int i = 1; i < nx - 1; i++)
                    {
                        int idx = row + i;
                        double t = cur[idx];
                        double lap =
                            cur[idx - 1] + cur[idx + 1] +
                            cur[idx - nx] + cur[idx + nx] -
                            4.0 * t;

                        next[idx] = t + r * lap;
                    }
                });
            }
            else
            {
                for (int j = 1; j < ny - 1; j++)
                {
                    int row = j * nx;
                    for (int i = 1; i < nx - 1; i++)
                    {
                        int idx = row + i;
                        double t = cur[idx];
                        double lap =
                            cur[idx - 1] + cur[idx + 1] +
                            cur[idx - nx] + cur[idx + nx] -
                            4.0 * t;

                        next[idx] = t + r * lap;
                    }
                }
            }

            ApplyBoundaries(next, nx, ny, cfg);

            (cur, next) = (next, cur);

        }

        return new SimulationResult(nx, ny, cfg.H, cfg.Dt, steps, cur);
    }

    /// <summary>
    /// Валидация входных данных
    /// </summary>
    /// <param name="cfg">Входные данные</param>
    private static void Validate(SimulationConfig cfg)
    {
        if (cfg.H <= 0) throw new ArgumentOutOfRangeException(nameof(cfg.H));
        if (cfg.Dt <= 0) throw new ArgumentOutOfRangeException(nameof(cfg.Dt));
        if (cfg.TotalTime <= 0) throw new ArgumentOutOfRangeException(nameof(cfg.TotalTime));
        if (cfg.Alpha <= 0) throw new ArgumentOutOfRangeException(nameof(cfg.Alpha));

        double dtMax = GetStableDtMax(cfg.Alpha, cfg.H);
        if (cfg.Dt > dtMax)
        {
            throw new InvalidOperationException($"Нарушено условие устойчивости: dt={cfg.Dt} > dt_max={dtMax}. ");
        }
    }

    private static void ValidateInitialPointTemperatures(SimulationConfig cfg, int nx, int ny)
    {
        foreach (InitialPointTemperature point in cfg.InitialPointTemperatures)
        {
            if (point.I < 0 || point.I >= nx)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cfg.InitialPointTemperatures),
                    $"Point I index is out of range: I={point.I}, valid=[0..{nx - 1}].");
            }

            if (point.J < 0 || point.J >= ny)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cfg.InitialPointTemperatures),
                    $"Point J index is out of range: J={point.J}, valid=[0..{ny - 1}].");
            }
        }
    }

    private static void ApplyInitialPointTemperatures(
        double[] field,
        int nx,
        IReadOnlyList<InitialPointTemperature> points)
    {
        foreach (InitialPointTemperature point in points)
        {
            int idx = point.J * nx + point.I;
            field[idx] = point.Temperature;
        }
    }

    // Граничные условия
    private static void ApplyBoundaries(double[] a, int nx, int ny, SimulationConfig cfg)
    {
        ApplyLeft(a, nx, ny, cfg.G1_Left, cfg.H);
        ApplyRight(a, nx, ny, cfg.G2_Right, cfg.H);
        ApplyBottom(a, nx, ny, cfg.G3_Bottom, cfg.H);
        ApplyTop(a, nx, ny, cfg.G4_Top, cfg.H);
    }

    private static void ApplyLeft(double[] a, int nx, int ny, BoundarySpec b, double h)
    {
        int i0 = 0;
        int i1 = 1;

        for (int j = 0; j < ny; j++)
        {
            int idx0 = j * nx + i0;
            int idx1 = j * nx + i1;

            a[idx0] = b.Kind switch
            {
                BoundaryKind.Dirichlet => b.Value1,
                BoundaryKind.Neumann => a[idx1] + b.Value1 * h,
                BoundaryKind.Robin => RobinLeftOrBottom(a[idx1], b.Value1, b.Value2, h),
                _ => a[idx0]
            };
        }
    }

    private static void ApplyRight(double[] a, int nx, int ny, BoundarySpec b, double h)
    {
        int i0 = nx - 1;
        int i1 = nx - 2;

        for (int j = 0; j < ny; j++)
        {
            int idx0 = j * nx + i0;
            int idx1 = j * nx + i1;

            a[idx0] = b.Kind switch
            {
                BoundaryKind.Dirichlet => b.Value1,
                BoundaryKind.Neumann => a[idx1] + b.Value1 * h, // n=+x
                BoundaryKind.Robin => RobinRightOrTop(a[idx1], b.Value1, b.Value2, h),
                _ => a[idx0]
            };
        }
    }

    private static void ApplyBottom(double[] a, int nx, int ny, BoundarySpec b, double h)
    {
        int j0 = 0;
        int j1 = 1;

        for (int i = 0; i < nx; i++)
        {
            int idx0 = j0 * nx + i;
            int idx1 = j1 * nx + i;

            a[idx0] = b.Kind switch
            {
                BoundaryKind.Dirichlet => b.Value1,
                BoundaryKind.Neumann => a[idx1] + b.Value1 * h, // n=-y
                BoundaryKind.Robin => RobinLeftOrBottom(a[idx1], b.Value1, b.Value2, h),
                _ => a[idx0]
            };
        }
    }

    private static void ApplyTop(double[] a, int nx, int ny, BoundarySpec b, double h)
    {
        int j0 = ny - 1;
        int j1 = ny - 2;

        for (int i = 0; i < nx; i++)
        {
            int idx0 = j0 * nx + i;
            int idx1 = j1 * nx + i;

            a[idx0] = b.Kind switch
            {
                BoundaryKind.Dirichlet => b.Value1,
                BoundaryKind.Neumann => a[idx1] + b.Value1 * h,
                BoundaryKind.Robin => RobinRightOrTop(a[idx1], b.Value1, b.Value2, h),
                _ => a[idx0]
            };
        }
    }

    private static double RobinLeftOrBottom(double inner, double beta, double tEnv, double h)
    {
        double denom = (1.0 / h) + beta;
        return ((inner / h) + beta * tEnv) / denom;
    }

    private static double RobinRightOrTop(double inner, double beta, double tEnv, double h)
    {
        double denom = (1.0 / h) + beta;
        return ((inner / h) + beta * tEnv) / denom;
    }
}
