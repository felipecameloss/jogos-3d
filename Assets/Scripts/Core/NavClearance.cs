using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Folga em volta de um ponto do NavMesh: distância até a borda mais perto (parede, buraco).
/// Quem fura o NavMesh ao nascer (cano de saída, baús, santuários) só nasce com folga, senão
/// fecha um túnel estreito (formigueiro) ou uma passagem (embaixo do deck).
/// </summary>
public static class NavClearance
{
    /// <summary>O ponto tem pelo menos `radius` metros até a borda do NavMesh.</summary>
    public static bool Has(Vector3 point, float radius)
    {
        if (radius <= 0f) return true;
        return NavMesh.FindClosestEdge(point, out var edge, NavMesh.AllAreas) && edge.distance >= radius;
    }
}
