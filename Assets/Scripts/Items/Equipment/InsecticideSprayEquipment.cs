using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spray de inseticida: um jato em cone na direção da mira por um segundo. Machuca e envenena
/// todo inimigo dentro do cone (dá para varrer a mira durante o jato) e, quando acaba, deixa
/// uma nuvem de inseticida (<see cref="InsecticideCloud"/>) no fim do cone, que continua
/// machucando os inimigos e também o player se ele entrar nela. Bom para fechar o caminho
/// por onde os inimigos vêm e recuar.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Equipamentos/Spray de inseticida", fileName = "SprayDeInseticida")]
public class InsecticideSprayEquipment : EquipmentData
{
    [Header("Jato")]
    [SerializeField, Min(0.1f)] float sprayDuration = 1f;
    [SerializeField, Min(0.02f)] float tickInterval = 0.1f;
    [Tooltip("Alcance do cone em metros.")]
    [SerializeField, Min(1f)] float range = 8f;
    [Tooltip("Metade da abertura do cone, em graus.")]
    [SerializeField, Range(5f, 80f)] float coneAngle = 32f;
    [Tooltip("Dano por tick em múltiplos do dano do player.")]
    [SerializeField, Min(0f)] float damagePerTick = 0.3f;
    [Tooltip("Veneno em cada inimigo atingido, uma vez por jato (múltiplos do dano do player, total).")]
    [SerializeField, Min(0f)] float poisonDamage = 1.5f;
    [SerializeField, Min(0.1f)] float poisonDuration = 4f;
    [SerializeField] Color mistColor = new(0.86f, 0.95f, 0.8f, 0.35f);

    [Header("Nuvem")]
    [SerializeField, Min(0.5f)] float cloudRadius = 3.2f;
    [SerializeField, Min(1f)] float cloudLifetime = 7f;
    [Tooltip("Distância do player até o centro da nuvem, em frações do alcance.")]
    [SerializeField, Range(0.2f, 1f)] float cloudDistance = 0.6f;
    [Tooltip("Dano por segundo nos inimigos dentro da nuvem (múltiplos do dano do player).")]
    [SerializeField, Min(0f)] float cloudEnemyDps = 0.5f;
    [Tooltip("Dano por segundo no player se ficar dentro da nuvem (pontos de vida).")]
    [SerializeField, Min(0f)] float cloudPlayerDps = 8f;
    [Tooltip("Material transparente das bolas da nuvem (a cor vem do código).")]
    [SerializeField] Material cloudMaterial;

    public float Range => range;
    public float SprayDuration => sprayDuration;

    static readonly RaycastHit[] hits = new RaycastHit[16];

    public override bool Activate(EquipmentSlot slot)
    {
        slot.StartCoroutine(SprayRoutine(slot));
        return true;
    }

    IEnumerator SprayRoutine(EquipmentSlot slot)
    {
        var poisoned = new HashSet<EnemyBase>();
        Vector3 dir = FlatAim(slot);
        float nextTick = 0f;
        for (float t = 0f; t < sprayDuration; t += Time.deltaTime)
        {
            if (!Alive(slot)) yield break;
            if (t >= nextTick)
            {
                nextTick += tickInterval;
                dir = FlatAim(slot);
                Tick(slot, dir, poisoned);
            }
            yield return null;
        }
        if (!Alive(slot)) yield break;

        // Fim do jato: a névoa assenta numa nuvem no fim do cone
        Vector3 center = Ground(slot, slot.transform.position + dir * (range * cloudDistance));
        InsecticideCloud.Spawn(center, cloudRadius, cloudLifetime, slot.PlayerDamage * cloudEnemyDps, cloudPlayerDps,
            slot.gameObject, cloudMaterial);
    }

    static bool Alive(EquipmentSlot slot) => slot != null && (slot.Health == null || slot.Health.IsAlive);

    /// <summary>Direção da mira no plano (o jato sai reto, rente ao chão).</summary>
    static Vector3 FlatAim(EquipmentSlot slot)
    {
        Vector3 d = slot.AimPoint - slot.Chest;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) d = slot.transform.forward;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
    }

    void Tick(EquipmentSlot slot, Vector3 dir, HashSet<EnemyBase> poisoned)
    {
        Vector3 origin = slot.Chest;

        // Névoa: bolinhas espalhadas pelo cone, maiores mais longe
        for (int i = 0; i < 3; i++)
        {
            float d = Random.Range(0.8f, range);
            Vector3 side = Quaternion.Euler(0f, Random.Range(-coneAngle, coneAngle) * 0.85f, 0f) * dir;
            Vector3 p = origin + side * d + Vector3.up * Random.Range(-0.5f, 0.25f);
            ItemVfx.Explosion(p, 0.3f + d * 0.09f, mistColor, tickInterval * 3.5f);
        }

        float damage = slot.PlayerDamage * damagePerTick;
        for (int i = EnemyBase.Active.Count - 1; i >= 0; i--)
        {
            if (i >= EnemyBase.Active.Count) continue;
            var e = EnemyBase.Active[i];
            if (e == null || !e.IsAlive || !InCone(origin, dir, e.Center)) continue;
            Deal(slot, e.Health, damage, e.Center);
            if (poisonDamage > 0f && e.IsAlive && poisoned.Add(e))
                StatusEffects.On(e).ApplyPoison(slot.PlayerDamage * poisonDamage, poisonDuration, slot.gameObject);
        }
    }

    /// <summary>
    /// O ponto está no cone (no plano, com folga para bichos grandes cujo centro fica fora
    /// mas o corpo não) e numa altura que o jato alcança.
    /// </summary>
    public bool InCone(Vector3 origin, Vector3 flatDir, Vector3 point)
    {
        if (Mathf.Abs(point.y - origin.y) > 3f) return false;
        Vector3 to = point - origin;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > range + 0.6f) return false;
        if (dist < 0.9f) return true; // colado no player: o jato pega de qualquer jeito
        float slack = Mathf.Atan2(0.6f, dist) * Mathf.Rad2Deg;
        return Vector3.Angle(flatDir, to) <= coneAngle + slack;
    }

    /// <summary>Chão embaixo do ponto (ignora player, inimigos e bolhas de sabão no caminho).</summary>
    static Vector3 Ground(EquipmentSlot slot, Vector3 point)
    {
        float best = float.MaxValue;
        Vector3 ground = new(point.x, slot.transform.position.y, point.z);
        int n = Physics.RaycastNonAlloc(point + Vector3.up * 4f, Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = hits[i].collider;
            if (col.transform.IsChildOf(slot.transform) || col.GetComponentInParent<EnemyBase>() != null) continue;
            if (col.GetComponent<DriftingBubble>() != null) continue;
            if (hits[i].distance < best) { best = hits[i].distance; ground = hits[i].point; }
        }
        return ground;
    }

    static void Deal(EquipmentSlot slot, Health target, float amount, Vector3 point)
    {
        if (target == null || !target.IsAlive || amount <= 0f) return;
        if (slot.Inventory != null) slot.Inventory.DealItemDamage(target, amount, point, ignoreArmor: true);
        else target.TakeDamage(new DamageInfo(amount, slot.gameObject, point, Vector3.zero) { NoProc = true, IgnoreArmor = true });
    }
}
