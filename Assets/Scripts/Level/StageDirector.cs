using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Monta a fase atual na cena Main antes dos outros sistemas acordarem: liga o quintal ou o
/// cenário da fase (com o NavMesh dele), põe o player no começo, troca a tabela de inimigos e o
/// chefe, liga/desliga os perigos (pisão, chuva, lupa, bolhas, atmosfera do quintal), acerta a
/// luz (dia/noite, sempre noite ou dentro de casa) e os extras (vaga-lume lanterna, migalhas no
/// chão). Quem é a fase: <see cref="RunManager.UpcomingStage"/> na <see cref="StageSequence"/>.
/// </summary>
[DefaultExecutionOrder(-500)]
public class StageDirector : MonoBehaviour
{
    [SerializeField] StageSequence sequence;
    [Tooltip("Raiz do quintal na cena (terreno, cerca, NavMesh).")]
    [SerializeField] GameObject backyard;
    [Tooltip("Modelo do vaga-lume que vira lanterna do player (Firefly.fbx).")]
    [SerializeField] GameObject lanternModel;

    /// <summary>A fase montada agora (null sem sequência).</summary>
    public static StageDefinition Current { get; private set; }
    /// <summary>A fase atual é a última da sequência (a próxima volta para o começo).</summary>
    public static bool IsFinalStage { get; private set; }
    public static StageSequence Sequence { get; private set; }

    /// <summary>Cenário instanciado da fase (null = só o quintal).</summary>
    public GameObject Level { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Current = null; IsFinalStage = false; Sequence = null; }

    void Awake()
    {
        Sequence = sequence;
        int stage = RunManager.UpcomingStage;
        Current = sequence != null ? sequence.ForStage(stage) : null;
        IsFinalStage = sequence != null && sequence.IsFinal(stage);
        if (Current == null) return;
        BuildLevel(Current);
        ApplyEnemies(Current);
        ApplyHazards(Current);
        ApplyLighting(Current);
        if (Current.PlayerLantern) PlayerLantern.Attach(GameObject.FindGameObjectWithTag("Player"), lanternModel);
    }

    void Start()
    {
        if (Current != null && Current.ScatteredCrumbs > 0) ScatterCrumbs(Current.ScatteredCrumbs);
    }

    void OnDestroy()
    {
        if (Current != null && Sequence == sequence) { Current = null; Sequence = null; }
    }

    // ---------- Cenário ----------

    void BuildLevel(StageDefinition def)
    {
        if (backyard != null) backyard.SetActive(def.UseBackyard);
        if (def.LevelPrefab == null) return;
        Level = Instantiate(def.LevelPrefab, Vector3.zero, Quaternion.identity);
        Level.name = def.LevelPrefab.name;

        var spawn = FindDeep(Level.transform, "PlayerSpawn");
        var player = GameObject.FindGameObjectWithTag("Player");
        if (spawn == null || player == null) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
        if (cc != null) cc.enabled = true;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // ---------- Inimigos e perigos ----------

    static void ApplyEnemies(StageDefinition def)
    {
        if (def.Enemies.Count > 0)
            foreach (var s in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include)) s.ReplaceEntries(def.Enemies);
        if (def.Boss != null)
            foreach (var b in FindObjectsByType<BossSpawner>(FindObjectsInactive.Include)) b.OverrideBoss(def.Boss);
    }

    static void ApplyHazards(StageDefinition def)
    {
        foreach (var s in FindObjectsByType<StompDirector>(FindObjectsInactive.Include)) s.enabled = def.HumanStomps;
        foreach (var r in FindObjectsByType<RainEvent>(FindObjectsInactive.Include)) r.enabled = def.Rain;
        foreach (var m in FindObjectsByType<MagnifierEvent>(FindObjectsInactive.Include)) m.enabled = def.Magnifier;
        foreach (var b in FindObjectsByType<SoapBubbleStream>(FindObjectsInactive.Include)) b.gameObject.SetActive(def.SoapBubbles);
        foreach (var a in FindObjectsByType<BackyardAtmosphere>(FindObjectsInactive.Include)) a.enabled = def.BackyardAtmosphere;
    }

    // ---------- Luz ----------

    static void ApplyLighting(StageDefinition def)
    {
        var cycle = FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        if (def.FogDensity > 0f) RenderSettings.fogDensity = def.FogDensity;
        if (cycle != null)
        {
            cycle.SunTint = def.SunTint;
            cycle.LightScale = def.LightScale;
            cycle.LockedPhase = def.Lighting == StageDefinition.LightingMode.AlwaysNight ? 0.75f : -1f;
        }
        if (def.Lighting == StageDefinition.LightingMode.DayNight) return;

        // fases fechadas: sem céu (fundo da câmera na cor da neblina)
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = def.FogColor;
        }
        RenderSettings.fogColor = def.FogColor;
        if (cycle != null) cycle.NightFog = def.FogColor;
        if (def.Lighting != StageDefinition.LightingMode.Indoor) return;

        // dentro de casa: sem ciclo, luz de lâmpada fixa vindo de cima
        if (cycle != null) cycle.enabled = false;
        var sun = cycle != null ? cycle.GetComponent<Light>() : RenderSettings.sun;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(62f, 35f, 0f);
            sun.color = new Color(1f, 0.92f, 0.78f) * def.SunTint;
            sun.intensity = 1.1f * def.LightScale;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = def.IndoorSky * def.LightScale;
        RenderSettings.ambientEquatorColor = def.IndoorEquator * def.LightScale;
        RenderSettings.ambientGroundColor = def.IndoorGround * def.LightScale;
    }

    // ---------- Migalhas ----------

    static void ScatterCrumbs(int count)
    {
        var dropper = FindAnyObjectByType<CrumbDropper>();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (dropper == null || player == null || !NavMesh.SamplePosition(player.transform.position, out var start, 3f, NavMesh.AllAreas)) return;
        var path = new NavMeshPath();
        int placed = 0;
        for (int attempt = 0; attempt < count * 8 && placed < count; attempt++)
        {
            // de cima: cai no chão ou em cima do que der para subir (balcão)
            Vector3 p = new(Random.Range(-45f, 45f), 20f, Random.Range(-45f, 45f));
            if (!NavMesh.SamplePosition(p, out var hit, 25f, NavMesh.AllAreas)) continue;
            // só onde o player alcança (nada dentro/em cima de armário)
            if (!NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            dropper.Drop(1, hit.position + Vector3.up * 0.6f);
            placed++;
        }
    }

#if UNITY_EDITOR
    public void EditorSetup(StageSequence seq, GameObject backyardRoot, GameObject lantern)
    {
        sequence = seq; backyard = backyardRoot; lanternModel = lantern;
    }
#endif
}
