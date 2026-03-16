using Core;

namespace Tests;

public class ExplicitHeatSolverTests
{
    /// <summary>
    /// Тест проверяет условие сходимости
    /// </summary>
    [Fact]
    public void GetStableDtMax_ReturnsExpectedValue()
    {
        double dtMax = ExplicitHeatSolver.GetStableDtMax(alpha: 0.5, h: 0.01);

        Assert.Equal(0.00005, dtMax, precision: 10);
    }

    /// <summary>
    /// Тест проверяет, что рассчёт не запускается при нарушении условия устойчивости.
    /// </summary>
    [Fact]
    public void RunSequential_Throws_WhenStabilityConditionIsViolated()
    {
        var solver = new ExplicitHeatSolver();
        var config = new SimulationConfig
        {
            H = 0.01,
            Dt = 0.000051,
            TotalTime = 0.01,
            Alpha = 0.5
        };

        Assert.Throws<InvalidOperationException>(() => solver.RunSequential(config));
    }

    /// <summary>
    /// Тест проверяет, что параллельный и последовательный расчёты дают одинаковый результат
    /// </summary>
    [Fact]
    public void RunParallel_MatchesSequentialResult()
    {
        var solver = new ExplicitHeatSolver();
        var config = new SimulationConfig
        {
            Width = 0.4,
            Height = 0.4,
            H = 0.1,
            Dt = 0.001,
            TotalTime = 0.02,
            Alpha = 0.5,
            InitialTemperature = 20.0,
            G1_Left = BoundarySpec.Neumann(0.0),
            G2_Right = BoundarySpec.Neumann(0.0),
            G3_Bottom = BoundarySpec.Dirichlet(100.0),
            G4_Top = BoundarySpec.Dirichlet(0.0)
        };

        SimulationResult sequential = solver.RunSequential(config);
        SimulationResult parallel = solver.RunParallel(config, maxDegreeOfParallelism: 4, out int usedThreads);

        Assert.True(usedThreads >= 1);
        Assert.Equal(sequential.Nx, parallel.Nx);
        Assert.Equal(sequential.Ny, parallel.Ny);
        Assert.Equal(sequential.Steps, parallel.Steps);
        Assert.Equal(sequential.FinalField.Length, parallel.FinalField.Length);

        for (int i = 0; i < sequential.FinalField.Length; i++)
        {
            Assert.Equal(sequential.FinalField[i], parallel.FinalField[i], precision: 10);
        }
    }
}
