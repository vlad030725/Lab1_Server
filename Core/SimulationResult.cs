namespace Core;

public sealed record SimulationResult(
    int Nx,
    int Ny,
    double H,
    double Dt,
    int Steps,
    double[] FinalField
);
