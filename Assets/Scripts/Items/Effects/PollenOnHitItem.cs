using UnityEngine;

/// <summary>
/// Chance de o acerto soltar uma nuvem de pólen em volta do alvo (Pólen). Quem fica dentro
/// fica confuso: para de atacar e anda sem rumo (chefes são imunes).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Nuvem de pólen ao acertar", fileName = "NovoItemPolen")]
public class PollenOnHitItem : ItemData
{
    [Tooltip("Chance por stack (0.12 = 12%). Stacks somam sem passar de 100%.")]
    [SerializeField, Range(0f, 1f)] float chancePerStack = 0.12f;
    [SerializeField, Min(0.5f)] float radius = 3.5f;
    [SerializeField, Min(0f)] float radiusPerExtraStack = 0.5f;
    [Tooltip("Quanto tempo a nuvem fica no ar (segundos).")]
    [SerializeField, Min(0.5f)] float cloudDuration = 2.5f;
    [Tooltip("Quanto tempo o inimigo fica confuso depois de sair da nuvem (segundos).")]
    [SerializeField, Min(0.1f)] float confuseDuration = 2f;
    [SerializeField, Min(0f)] float confusePerExtraStack = 0.5f;
    [Tooltip("Máximo de nuvens ao mesmo tempo: a mais velha some quando sai uma nova.")]
    [SerializeField, Min(1)] int maxClouds = 4;
    [Tooltip("Material transparente das bolas da nuvem (a cor vem do código).")]
    [SerializeField] Material cloudMaterial;

    public override void OnHit(Inventory inventory, int stacks, Health target, DamageInfo info)
    {
        if (target == null || !Inventory.Roll(chancePerStack, stacks)) return;

        while (PollenCloud.Active.Count >= maxClouds && PollenCloud.Active.Count > 0)
            PollenCloud.Active[0].FadeOut();

        int extra = stacks - 1;
        Vector3 center = target.TryGetComponent(out EnemyBase enemy) ? enemy.Center : info.HitPoint;
        PollenCloud.Spawn(center,
            radius + radiusPerExtraStack * extra,
            cloudDuration,
            confuseDuration + confusePerExtraStack * extra,
            cloudMaterial);
    }
}
