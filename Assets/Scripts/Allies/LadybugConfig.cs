using UnityEngine;

/// <summary>
/// Números e materiais das joaninhas aliadas e da margarida onde elas são compradas.
/// Crie em Assets > Create > Jogo > Joaninhas. Um asset só, usado pelo spawner da
/// margarida, pelas joaninhas e pelo RunManager (para elas passarem de fase).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Joaninhas", fileName = "LadybugConfig")]
public class LadybugConfig : ScriptableObject
{
    [Header("Compra (margarida)")]
    [Tooltip("Preço da primeira joaninha na dificuldade 1 (cresce com a dificuldade).")]
    [Min(1)] public int baseCost = 25;
    [Tooltip("Cada joaninha que o player já tem encarece a próxima (0.5 = +50% por joaninha).")]
    [Min(0f)] public float costPerOwned = 0.5f;
    [Tooltip("Máximo de joaninhas orbitando o player.")]
    [Min(1)] public int maxLadybugs = 6;

    [Header("Órbita")]
    [Tooltip("Distância do player (metros); cresce um pouco com mais joaninhas.")]
    [Min(0.5f)] public float orbitRadius = 1.7f;
    [Tooltip("Altura acima dos pés do player.")]
    public float orbitHeight = 1.8f;
    [Tooltip("Graus por segundo girando em volta do player.")]
    public float orbitSpeed = 90f;

    [Header("Tiro")]
    [Tooltip("Só atira em inimigos até essa distância da joaninha.")]
    [Min(1f)] public float range = 22f;
    [Tooltip("Segundos entre tiros de cada joaninha.")]
    [Min(0.1f)] public float fireInterval = 1.1f;
    [Tooltip("Dano de cada tiro em múltiplos do dano do player (0.45 = 45%).")]
    [Min(0f)] public float damageCoefficient = 0.45f;
    [Min(1f)] public float projectileSpeed = 45f;

    [Header("Visual")]
    [Tooltip("Material opaco das joaninhas, da margarida e do tiro (as cores vêm do código).")]
    public Material bodyMaterial;
    [Tooltip("Material do rastro do tiro.")]
    public Material trailMaterial;
    public Color shotColor = new(1f, 0.5f, 0.15f, 1f);

    /// <summary>Preço da próxima joaninha para quem já tem `owned`.</summary>
    public int CostFor(int owned)
    {
        float coeff = DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseCost * coeff * (1f + costPerOwned * Mathf.Max(0, owned))));
    }
}
