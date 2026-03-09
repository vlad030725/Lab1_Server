using Core;

namespace Tests;

public class ExplicitHeatSolverTests
{
    [Fact]
    public void GetStableDtMax_ReturnsExpectedValue()
    {
        double dtMax = ExplicitHeatSolver.GetStableDtMax(alpha: 0.5, h: 0.01);

        Assert.Equal(0.00005, dtMax, precision: 10);
    }

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

    [Fact]
    public void RunSequential_WithUniformDirichletBoundaries_PreservesUniformField()
    {
        var solver = new ExplicitHeatSolver();
        var config = new SimulationConfig
        {
            Width = 0.4,
            Height = 0.4,
            H = 0.1,
            Dt = 0.001,
            TotalTime = 0.01,
            Alpha = 0.5,
            InitialTemperature = 42.0,
            G1_Left = BoundarySpec.Dirichlet(42.0),
            G2_Right = BoundarySpec.Dirichlet(42.0),
            G3_Bottom = BoundarySpec.Dirichlet(42.0),
            G4_Top = BoundarySpec.Dirichlet(42.0)
        };

        SimulationResult result = solver.RunSequential(config);

        Assert.All(result.FinalField, value => Assert.Equal(42.0, value, precision: 10));
    }

    [Fact]
    public void RunSequential_WithZeroFluxNeumannBoundaries_PreservesUniformField()
    {
        var solver = new ExplicitHeatSolver();
        var config = new SimulationConfig
        {
            Width = 0.4,
            Height = 0.4,
            H = 0.1,
            Dt = 0.001,
            TotalTime = 0.01,
            Alpha = 0.5,
            InitialTemperature = 17.5,
            G1_Left = BoundarySpec.Neumann(0.0),
            G2_Right = BoundarySpec.Neumann(0.0),
            G3_Bottom = BoundarySpec.Neumann(0.0),
            G4_Top = BoundarySpec.Neumann(0.0)
        };

        SimulationResult result = solver.RunSequential(config);

        Assert.All(result.FinalField, value => Assert.Equal(17.5, value, precision: 10));
    }

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

    [Fact]
    public void RunSequential_AppliesInitialPointTemperature()
    {
        var solver = new ExplicitHeatSolver();
        var config = new SimulationConfig
        {
            Width = 1.0,
            Height = 1.0,
            H = 0.01,
            Dt = 1e-5,
            TotalTime = 1e-5,
            Alpha = 0.5,
            InitialTemperature = 20.0,
            G1_Left = BoundarySpec.Neumann(0.0),
            G2_Right = BoundarySpec.Neumann(0.0),
            G3_Bottom = BoundarySpec.Neumann(0.0),
            G4_Top = BoundarySpec.Neumann(0.0),
            InitialPointTemperatures = new[]
            {
                new InitialPointTemperature(90, 90, 1000.0)
            }
        };

        SimulationResult result = solver.RunSequential(config);
        int centerIdx = 90 * result.Nx + 90;

        Assert.True(result.FinalField[centerIdx] > 20.0);
    }

    [Fact]
    public void RunSequential_Throws_WhenInitialPointIndexIsOutOfRange()
    {
        var solver = new ExplicitHeatSolver();
        var config = new SimulationConfig
        {
            Width = 1.0,
            Height = 1.0,
            H = 0.01,
            Dt = 5e-5,
            TotalTime = 0.01,
            Alpha = 0.5,
            InitialPointTemperatures = new[]
            {
                new InitialPointTemperature(200, 90, 1000.0)
            }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => solver.RunSequential(config));
    }
}
