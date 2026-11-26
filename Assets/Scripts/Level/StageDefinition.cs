using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Uma fase da run (quintal, horta, poça, deck, formigueiro, cozinha): cenário, luz, perigos,
/// inimigos e chefe. O <see cref="StageDirector"/> aplica a da fase atual quando a cena carrega.
/// Tudo vive na mesma cena (Main); a fase só liga/desliga e troca o que muda.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Fase", fileName = "NovaFase")]
public class StageDefinition : ScriptableObject
{
    public enum LightingMode
    {
        /// <summary>Ciclo de dia/noite normal (ao ar livre).</summary>
        DayNight,
        /// <summary>Hora travada à noite e mais escuro (embaixo do deck, formigueiro).</summary>
        AlwaysNight,
        /// <summary>Dentro de casa: sem sol nem ciclo, luz fixa de lâmpada.</summary>
        Indoor,
    }

    [Header("Fase")]
    [SerializeField] string displayName = "Quintal";
    [TextArea, SerializeField] string description;

    [Header("Cenário")]
    [Tooltip("Mantém o quintal da cena (terreno, cerca e NavMesh). Desligado = só o cenário abaixo.")]
    [SerializeField] bool useBackyard = true;
    [Tooltip("Cenário/enfeites da fase, criados na origem. Sem o quintal, precisa de NavMeshSurface assado " +
             "e de um filho PlayerSpawn (onde o player começa).")]
    [SerializeField] GameObject levelPrefab;
    [Tooltip("Horizonte, flora, poças e nuvens do quintal (BackyardAtmosphere).")]
    [SerializeField] bool backyardAtmosphere = true;

    [Header("Luz e clima")]
    [SerializeField] LightingMode lighting = LightingMode.DayNight;
    [Tooltip("Lugar escuro (embaixo do deck, formigueiro): conta como noite para os vaga-lumes.")]
    [SerializeField] bool dark;
    [Tooltip("Multiplica a cor do sol/lua (e da lâmpada, dentro de casa).")]
    [SerializeField] Color sunTint = Color.white;
    [Tooltip("Multiplica a luz do sol/lua e a luz ambiente (fases escuras < 1).")]
    [SerializeField, Range(0f, 2f)] float lightScale = 1f;
    [Tooltip("Densidade da neblina (0 = a da cena).")]
    [SerializeField, Min(0f)] float fogDensity = 0f;
    [Tooltip("Cor da neblina e do fundo da câmera nas fases fechadas (AlwaysNight/Indoor).")]
    [SerializeField] Color fogColor = new(0.05f, 0.05f, 0.06f);
    [Tooltip("Dentro de casa: luz ambiente (céu, horizonte, chão).")]
    [SerializeField] Color indoorSky = new(0.8f, 0.72f, 0.6f), indoorEquator = new(0.6f, 0.52f, 0.42f), indoorGround = new(0.3f, 0.25f, 0.2f);

    [Header("Perigos e eventos")]
    [SerializeField] bool humanStomps = true;
    [SerializeField] bool rain = true;
    [Tooltip("Criança com lupa (precisa de sol).")]
    [SerializeField] bool magnifier = true;
    [Tooltip("Vizinho soprando bolhas de sabão por cima da cerca.")]
    [SerializeField] bool soapBubbles = true;

    [Header("Inimigos")]
    [Tooltip("Tabela de inimigos da fase. Vazia = a da cena (quintal).")]
    [SerializeField] List<EnemySpawner.SpawnEntry> enemies = new();
    [Tooltip("Chefe da fase. Vazio = o rodízio do BossSpawner.")]
    [SerializeField] EnemyBase boss;

    [Header("Extras")]
    [Tooltip("Um vaga-lume amigo voa com o player iluminando (formigueiro).")]
    [SerializeField] bool playerLantern;
    [Tooltip("Migalhas espalhadas pelo chão no começo da fase (cozinha).")]
    [SerializeField, Min(0)] int scatteredCrumbs;

    public string DisplayName => displayName;
    public string Description => description;
    public bool UseBackyard => useBackyard;
    public GameObject LevelPrefab => levelPrefab;
    public bool BackyardAtmosphere => backyardAtmosphere;
    public LightingMode Lighting => lighting;
    public bool Dark => dark;
    public Color SunTint => sunTint;
    public float LightScale => lightScale;
    public float FogDensity => fogDensity;
    public Color FogColor => fogColor;
    public Color IndoorSky => indoorSky;
    public Color IndoorEquator => indoorEquator;
    public Color IndoorGround => indoorGround;
    public bool HumanStomps => humanStomps;
    public bool Rain => rain;
    public bool Magnifier => magnifier;
    public bool SoapBubbles => soapBubbles;
    public IReadOnlyList<EnemySpawner.SpawnEntry> Enemies => enemies;
    public EnemyBase Boss => boss;
    public bool PlayerLantern => playerLantern;
    public int ScatteredCrumbs => scatteredCrumbs;
}
