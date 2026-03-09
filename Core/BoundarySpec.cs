namespace Core;

public sealed record BoundarySpec(
    BoundaryKind Kind,
    double Value1,
    double Value2 = 0.0
)
{
    // Dirichlet: Value1 = T
    public static BoundarySpec Dirichlet(double temperature) => new(BoundaryKind.Dirichlet, temperature);

    // Neumann: Value1 = dT/dn
    public static BoundarySpec Neumann(double dTdn) => new(BoundaryKind.Neumann, dTdn);

    // Robin: Value1 = beta, Value2 = Tenv
    public static BoundarySpec Robin(double beta, double ambientTemperature) => new(BoundaryKind.Robin, beta, ambientTemperature);
}