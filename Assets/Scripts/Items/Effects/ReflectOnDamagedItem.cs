using UnityEngine;

/// <summary>
/// Devolve parte do dano recebido para quem atacou (Espinho de rosa).
/// Mais stacks = maior fração devolvida. Ignora dano sem fonte, próprio ou NoProc.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Espinho (devolve dano)", fileName = "NovoItemEspinho")]
public class ReflectOnDamagedItem : ItemData
{
    [Tooltip("Fração do dano devolvida por stack (0.5 = 50%, 2 stacks = 100%).")]
    [SerializeField, Min(0f)] float reflectPerStack = 0.5f;
    [Tooltip("Dano mínimo devolvido por stack.")]
    [SerializeField, Min(0f)] float minimumDamage = 0f;
    [SerializeField] Color lineColor = new(0.9f, 0.22f, 0.32f, 0.95f);

    public override void OnOwnerDamaged(Inventory inventory, int stacks, DamageInfo info)
    {
        if (info.NoProc || info.Source == null || info.Source.transform.IsChildOf(inventory.transform)) return;

        var attacker = info.Source.GetComponentInParent<IDamageable>();
        if (attacker == null || !attacker.IsAlive || ReferenceEquals(attacker, inventory.Health)) return;

        float reflected = Mathf.Max(minimumDamage * stacks, info.Amount * reflectPerStack * stacks);
        if (reflected <= 0f) return;

        Vector3 from = inventory.transform.position + Vector3.up;
        var enemy = info.Source.GetComponentInParent<EnemyBase>();
        Vector3 to = enemy != null
            ? enemy.Center
            : attacker is Component comp
                ? comp.transform.position + Vector3.up * 0.6f
                : info.Source.transform.position;

        ItemVfx.Line(from, to, lineColor, 0.08f, 0.18f);
        inventory.DealItemDamage(attacker, reflected, to);
    }
}
