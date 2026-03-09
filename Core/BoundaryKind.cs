namespace Core;

public enum BoundaryKind
{
    /// <summary>Граничное условие 1-го рода (Дирихле): T = const</summary>
    Dirichlet = 1,

    /// <summary>Граничное условие 2-го рода (Нейман): dT/dn = const</summary>
    Neumann = 2,

    /// <summary>Граничное условие 3-го рода (Робин): dT/dn = -beta*(T - Tenv)</summary>
    Robin = 3
}