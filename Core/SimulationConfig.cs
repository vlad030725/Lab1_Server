namespace Core;

public sealed record SimulationConfig
{
    public double Width { get; init; } = 1.0;
    public double Height { get; init; } = 1.0;

    /// <summary>Шаг сетки (hx=hy=h)</summary>
    public double H { get; init; } = 0.01;

    /// <summary>Шаг по времени</summary>
    public double Dt { get; init; } = 5e-5;

    /// <summary>Общее время моделирования</summary>
    public double TotalTime { get; init; } = 0.2;

    /// <summary>Коэффициент температуропроводности alpha</summary>
    public double Alpha { get; init; } = 0.5;

    /// <summary>Начальная температура по всей пластине</summary>
    public double InitialTemperature { get; init; } = 20.0;

    // Границы: Г1-левая, Г2-правая, Г3-нижняя, Г4-верхняя
    public BoundarySpec G1_Left { get; init; } = BoundarySpec.Neumann(0.0);
    public BoundarySpec G2_Right { get; init; } = BoundarySpec.Neumann(0.0);
    public BoundarySpec G3_Bottom { get; init; } = BoundarySpec.Dirichlet(100.0);
    public BoundarySpec G4_Top { get; init; } = BoundarySpec.Dirichlet(20.0);

    /// <summary>Точечные начальные условия U^0_{i,j} = T</summary>
    public IReadOnlyList<InitialPointTemperature> InitialPointTemperatures { get; init; } =
        Array.Empty<InitialPointTemperature>();
}
