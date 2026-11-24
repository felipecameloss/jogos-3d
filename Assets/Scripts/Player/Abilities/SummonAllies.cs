using UnityEngine;
using UnityEngine.AI;

/// <summary>Especial: chama formigas aliadas que caçam os inimigos por alguns segundos.</summary>
public class SummonAllies : PlayerAbility
{
    [Header("Formigas aliadas")]
    [SerializeField] AllyAnt allyPrefab;
    [SerializeField, Min(1)] int count = 3;
    [SerializeField, Min(1f)] float lifetime = 10f;

    public AllyAnt AllyPrefab => allyPrefab;
    public int Count => count;
    public float Lifetime => lifetime;

    /// <summary>Quantas formigas e por quanto tempo (personagem Formiga chama mais).</summary>
    public void SetSquad(int allies, float seconds)
    {
        count = Mathf.Max(1, allies);
        lifetime = Mathf.Max(1f, seconds);
    }

    protected override bool TryActivate()
    {
        if (allyPrefab == null) return false;
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            Vector3 around = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.5f;
            if (!NavMesh.SamplePosition(around, out var hit, 3f, NavMesh.AllAreas)) continue;
            var ally = Instantiate(allyPrefab, hit.position, Quaternion.LookRotation(AimForward));
            ally.Init(gameObject, lifetime);
            spawned++;
        }
        return spawned > 0; // sem chão em volta: não gasta o cooldown
    }
}
