using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aplica no player o personagem escolhido no menu (PlayerCharacters.Selected): stats base,
/// modelo e kit de habilidades. Roda sozinho quando uma cena com o player carrega (depois
/// dos Awake, antes dos Start), então a HUD, o RunManager (vida da fase anterior) e o
/// inventário já começam com o personagem certo. Nada na cena precisa ser ligado à mão.
/// - Explorador: não muda nada (é o player da cena).
/// - Formiga: modelo de formiga, vida 70 e mais rápida, dash com recarga curta, agulha vira
///   mordida, R chama mais formigas e a passiva AntColonyCall.
/// - Besouro: modelo de besouro, vida 180 + armadura, lento; troca estilingue/agulha/dash/R
///   por BeetleHorn, BeetleCharge, BeetleShell e BeetleQuake (mesmas teclas).
/// Os modelos vêm de Resources (Assets/Art/Models/Characters/Resources/, gerados por
/// Tools/Blender/build_player_ant.py e build_player_beetle.py).
/// </summary>
[DisallowMultipleComponent]
public class PlayerCharacter : MonoBehaviour
{
    public PlayerCharacterInfo Info { get; private set; }
    public PlayerCharacterId Id => Info != null ? Info.Id : PlayerCharacterId.Explorer;
    /// <summary>Raiz do modelo de inseto (null no Explorador ou se o modelo não carregou).</summary>
    public Transform Model { get; private set; }
    public InsectPlayerVisual Visual { get; private set; }

    // ---------- Gatilho automático ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // Garantia para a primeira cena (o sceneLoaded dela também chama; aplicar é idempotente)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyToFirstScene() => ApplyToScenePlayer();

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScenePlayer();

    static void ApplyToScenePlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || player.GetComponent<PlayerCharacter>() != null) return;
        Apply(player, PlayerCharacters.SelectedInfo);
    }

    /// <summary>Aplica o personagem no player (uma vez só por player).</summary>
    public static PlayerCharacter Apply(GameObject player, PlayerCharacterInfo info)
    {
        var existing = player.GetComponent<PlayerCharacter>();
        if (existing != null) return existing;
        var pc = player.AddComponent<PlayerCharacter>();
        pc.Info = info ?? PlayerCharacters.Explorer;
        if (pc.Info.Id == PlayerCharacterId.Explorer) return pc;

        try
        {
            pc.ApplyStats();
            pc.ApplyModel();
            if (pc.Info.Id == PlayerCharacterId.Ant) pc.ApplyAntKit();
            else if (pc.Info.Id == PlayerCharacterId.Beetle) pc.ApplyBeetleKit();
        }
        catch (System.Exception e)
        {
            // Um erro aqui não pode travar a run: o player continua com o que já foi aplicado
            Debug.LogException(e, player);
        }
        return pc;
    }

    // ---------- Stats ----------

    void ApplyStats()
    {
        if (TryGetComponent(out Health health))
        {
            health.SetMaxHealth(Info.MaxHealth, keepRatio: true);
            health.Armor = Info.Armor;
        }
        if (TryGetComponent(out CharacterStats stats)) stats.SetBase(Info.Damage, Info.AttackSpeed);
        if (TryGetComponent(out PlayerMovement movement)) movement.SetBaseSpeeds(Info.WalkSpeed, Info.SprintSpeed);
        // O Inventory leu a base no Awake: relê (antes do RunManager devolver os itens da fase anterior)
        if (TryGetComponent(out Inventory inventory)) inventory.RefreshBaseStats();
        if (TryGetComponent(out ThornSlingshot sling) && sling.Muzzle != null && sling.Muzzle != transform)
        {
            var p = sling.Muzzle.localPosition;
            sling.Muzzle.localPosition = new Vector3(p.x, Info.MuzzleHeight, p.z);
        }
    }

    // ---------- Modelo ----------

    void ApplyModel()
    {
        if (string.IsNullOrEmpty(Info.ModelResource)) return;
        var prefab = Resources.Load<GameObject>(Info.ModelResource);
        if (prefab == null)
        {
            Debug.LogError($"PlayerCharacter: modelo '{Info.ModelResource}' não encontrado em Resources; mantendo o modelo da cena.", this);
            return;
        }

        // Some com o modelo humano (e a animação dele)
        var oldModel = transform.Find("Model");
        if (oldModel != null) oldModel.gameObject.SetActive(false);
        if (TryGetComponent(out PlayerAnimator oldAnimator)) oldAnimator.enabled = false;

        var model = Instantiate(prefab, transform);
        model.name = "CharacterModel";
        var mt = model.transform;
        mt.localPosition = Vector3.zero;
        mt.localRotation = Quaternion.identity;
        mt.localScale = Vector3.one;

        Bounds raw = LocalBounds(mt);
        float scale = raw.size.z > 0.001f ? Info.ModelLength / raw.size.z : 1f;
        mt.localScale = Vector3.one * scale;
        Bounds scaled = LocalBounds(mt);
        // pés no chão (a raiz do player fica nos pés do CharacterController)
        mt.localPosition = new Vector3(0f, -scaled.min.y + 0.02f, 0f);
        Model = mt;

        // Nada do modelo colide nem mexe na mira (o CharacterController é o corpo)
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);

        Visual = model.AddComponent<InsectPlayerVisual>();
        Visual.FlipLift = scaled.size.y;
        var anim = model.AddComponent<ProceduralInsectAnimator>();
        anim.ConfigureGait(Info.ModelLength * 1.1f, Info.WalkSpeed * 0.5f);
    }

    /// <summary>Caixa das malhas do modelo no espaço local do player.</summary>
    Bounds LocalBounds(Transform model)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    // ---------- Kits ----------

    void ApplyAntKit()
    {
        var accent = Info.Color;
        if (TryGetComponent(out NeedleThrust needle))
        {
            needle.HideVisual();
            needle.SetHudAndCooldown("Mordida", accent, 2.5f);
        }
        if (TryGetComponent(out DashAbility dash)) dash.SetHudAndCooldown("Dash", dash.HudColor, 1.5f);
        if (TryGetComponent(out SummonAllies summon))
        {
            summon.SetSquad(5, 12f);
            summon.SetHudAndCooldown("Colônia", summon.HudColor, 12f);
            gameObject.AddComponent<AntColonyCall>().Init(summon.AllyPrefab);
        }
        if (Visual != null && needle != null) gameObject.AddComponent<AbilityKick>().Init(needle, Visual, 0.4f, 10f);
    }

    void ApplyBeetleKit()
    {
        TryGetComponent(out ThornSlingshot sling);
        TryGetComponent(out NeedleThrust needle);
        TryGetComponent(out DashAbility dash);
        TryGetComponent(out SummonAllies summon);

        // Mesmas teclas: M1, M2, Shift, R
        var horn = gameObject.AddComponent<BeetleHorn>();
        horn.Init(sling != null ? sling.AttackAction : null, Visual);

        var charge = gameObject.AddComponent<BeetleCharge>();
        charge.Configure(needle != null ? needle.Action : null, 5f, 1, "Investida", "M2", new Color(0.95f, 0.55f, 0.25f));
        charge.SetVisual(Visual);

        var shell = gameObject.AddComponent<BeetleShell>();
        shell.Configure(dash != null ? dash.Action : null, 9f, 2, "Carapaça", "Shift", new Color(0.55f, 0.85f, 1f));
        shell.SetVisual(Visual);

        var quake = gameObject.AddComponent<BeetleQuake>();
        quake.Configure(summon != null ? summon.Action : null, 12f, 3, "Terremoto", "R", new Color(0.85f, 0.6f, 0.35f));
        quake.SetVisual(Visual);

        // O kit antigo fica desligado (a HUD ignora habilidades desligadas)
        if (sling != null) sling.enabled = false;
        if (needle != null) { needle.HideVisual(); needle.enabled = false; }
        if (dash != null) dash.enabled = false;
        if (summon != null) summon.enabled = false;
    }
}
