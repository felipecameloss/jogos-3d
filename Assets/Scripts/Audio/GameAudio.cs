using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de áudio procedural: música ambiente dinâmica (dia/noite) e efeitos sonoros
/// para tiros, impactos, mortes, migalhas, itens, baús, santuários e pisões.
/// Respeita os sliders de GameSettings (Master, Music e SFX).
/// </summary>
public class GameAudio : MonoBehaviour
{
    const int SampleRate = 22050;

    AudioSource dayMusicSource;
    AudioSource nightMusicSource;
    AudioSource sfxSource;

    AudioClip dayLoopClip;
    AudioClip nightLoopClip;
    AudioClip shotClip;
    AudioClip hitClip;
    AudioClip playerHurtClip;
    AudioClip enemyDeathClip;
    AudioClip crumbClip;
    AudioClip itemPickupClip;
    AudioClip chestOpenClip;
    AudioClip shrineWinClip;
    AudioClip shrineFailClip;
    AudioClip stompImpactClip;

    DayNightCycle dayNight;
    ThornSlingshot slingshot;
    Inventory inventory;
    PlayerWallet wallet;
    StompDirector stompDirector;

    float lastHitTime;
    float lastDeathTime;
    float lastCrumbTime;
    readonly HashSet<HumanStomp> hookedStomps = new();

    void Awake()
    {
        dayMusicSource = CreateSource(loop: true);
        nightMusicSource = CreateSource(loop: true);
        sfxSource = CreateSource(loop: false);

        BuildClips();
        dayMusicSource.clip = dayLoopClip;
        nightMusicSource.clip = nightLoopClip;
    }

    void OnEnable()
    {
        Health.OnAnyDamaged += HandleDamaged;
        EnemyBase.OnAnyEnemyDied += HandleEnemyDied;
        Chest.OnAnyOpened += HandleChestOpened;
        Shrine.OnAnyUsed += HandleShrineUsed;
        GameSettings.Changed += ApplyVolumes;
    }

    void Start()
    {
        dayNight = FindAnyObjectByType<DayNightCycle>();
        HookPlayerSystems();
        ApplyVolumes();

        dayMusicSource.Play();
        nightMusicSource.Play();
    }

    void OnDisable()
    {
        Health.OnAnyDamaged -= HandleDamaged;
        EnemyBase.OnAnyEnemyDied -= HandleEnemyDied;
        Chest.OnAnyOpened -= HandleChestOpened;
        Shrine.OnAnyUsed -= HandleShrineUsed;
        GameSettings.Changed -= ApplyVolumes;
        UnhookPlayerSystems();
    }

    void OnDestroy()
    {
        foreach (var s in hookedStomps)
            if (s != null) s.OnImpact -= HandleStompImpact;
        hookedStomps.Clear();
    }

    void Update() => ApplyVolumes();

    AudioSource CreateSource(bool loop)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        return src;
    }

    void HookPlayerSystems()
    {
        slingshot = FindAnyObjectByType<ThornSlingshot>();
        if (slingshot != null) slingshot.OnFired += HandleShot;

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        inventory = playerObj != null ? playerObj.GetComponent<Inventory>() : FindAnyObjectByType<Inventory>();
        if (inventory != null) inventory.OnItemAdded += HandleItemAdded;

        wallet = playerObj != null ? playerObj.GetComponent<PlayerWallet>() : FindAnyObjectByType<PlayerWallet>();
        if (wallet != null) wallet.OnCrumbsChanged += HandleCrumbsChanged;

        stompDirector = FindAnyObjectByType<StompDirector>();
        if (stompDirector != null) stompDirector.OnStompStarted += HandleStompStarted;
    }

    void UnhookPlayerSystems()
    {
        if (slingshot != null) slingshot.OnFired -= HandleShot;
        if (inventory != null) inventory.OnItemAdded -= HandleItemAdded;
        if (wallet != null) wallet.OnCrumbsChanged -= HandleCrumbsChanged;
        if (stompDirector != null) stompDirector.OnStompStarted -= HandleStompStarted;
    }

    void ApplyVolumes()
    {
        float musicVol = Mathf.Clamp01(GameSettings.MusicVolume) * 0.42f;
        float daylight = dayNight != null ? dayNight.Daylight : 1f;
        dayMusicSource.volume = musicVol * daylight;
        nightMusicSource.volume = musicVol * (1f - daylight);
    }

    void PlaySfx(AudioClip clip, float volume = 1f, float pitchMin = 1f, float pitchMax = 1f)
    {
        float sfxVol = Mathf.Clamp01(GameSettings.SfxVolume);
        if (clip == null || sfxVol <= 0.001f) return;
        sfxSource.pitch = pitchMin < pitchMax ? Random.Range(pitchMin, pitchMax) : pitchMin;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * sfxVol);
    }

    void HandleShot() => PlaySfx(shotClip, 0.55f, 0.94f, 1.08f);

    void HandleDamaged(Health target, DamageInfo info)
    {
        if (target == null || info.Amount <= 0f) return;
        if (target.CompareTag("Player"))
        {
            PlaySfx(playerHurtClip, 0.72f, 0.95f, 1.05f);
            return;
        }
        if (Time.unscaledTime - lastHitTime < 0.055f) return;
        lastHitTime = Time.unscaledTime;
        PlaySfx(hitClip, 0.45f, 0.92f, 1.12f);
    }

    void HandleEnemyDied(EnemyBase enemy, DamageInfo info)
    {
        if (Time.unscaledTime - lastDeathTime < 0.06f) return;
        lastDeathTime = Time.unscaledTime;
        PlaySfx(enemyDeathClip, 0.6f, 0.9f, 1.08f);
    }

    void HandleCrumbsChanged(int total, int delta)
    {
        if (delta <= 0 || Time.unscaledTime - lastCrumbTime < 0.07f) return;
        lastCrumbTime = Time.unscaledTime;
        PlaySfx(crumbClip, 0.42f, 0.97f, 1.12f);
    }

    void HandleItemAdded(ItemData item, int count) => PlaySfx(itemPickupClip, 0.75f);
    void HandleChestOpened(Chest chest, ItemData item) => PlaySfx(chestOpenClip, 0.7f);
    void HandleShrineUsed(Shrine shrine, bool success) => PlaySfx(success ? shrineWinClip : shrineFailClip, 0.7f);

    void HandleStompStarted(HumanStomp stomp)
    {
        if (stomp == null || !hookedStomps.Add(stomp)) return;
        stomp.OnImpact += HandleStompImpact;
    }

    void HandleStompImpact(HumanStomp stomp) => PlaySfx(stompImpactClip, 0.9f, 0.96f, 1.04f);

    void BuildClips()
    {
        dayLoopClip = BuildDayAmbientLoop(12f);
        nightLoopClip = BuildNightAmbientLoop(12f);
        shotClip = BuildShot();
        hitClip = BuildHit();
        playerHurtClip = BuildPlayerHurt();
        enemyDeathClip = BuildEnemyDeath();
        crumbClip = BuildCrumb();
        itemPickupClip = BuildArpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.08f, 0.2f);
        chestOpenClip = BuildArpeggio(new[] { 392f, 493.88f, 587.33f, 783.99f }, 0.09f, 0.24f);
        shrineWinClip = BuildArpeggio(new[] { 440f, 554.37f, 659.25f, 880f }, 0.095f, 0.26f);
        shrineFailClip = BuildSweep("ShrineFail", 210f, 110f, 0.28f, 0.45f, noiseMix: 0.2f);
        stompImpactClip = BuildStompImpact();
    }

    static AudioClip CreateClip(string name, float duration, System.Func<float, int, float> sampleAt)
    {
        int len = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        var data = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / SampleRate;
            data[i] = Mathf.Clamp(sampleAt(t, i), -1f, 1f);
        }
        var clip = AudioClip.Create(name, len, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildDayAmbientLoop(float duration)
    {
        // Progressão acústica pentatônica suave em Dó maior, looping sem clique (harmônicos inteiros em 12s).
        float[] chords = { 261.63f, 329.63f, 392f, 440f, 392f, 329.63f };
        float[] melody = { 523.25f, 659.25f, 587.33f, 783.99f, 659.25f, 523.25f, 440f, 587.33f };
        return CreateClip("DayAmbient", duration, (t, _) =>
        {
            float fade = Mathf.Sin(Mathf.PI * (t / duration));
            float baseEnv = 0.7f + 0.3f * fade;

            int ci = Mathf.FloorToInt(t / (duration / chords.Length)) % chords.Length;
            float fRoot = chords[ci];
            float pad = Mathf.Sin(2f * Mathf.PI * fRoot * 0.5f * t) * 0.16f
                      + Mathf.Sin(2f * Mathf.PI * fRoot * 0.75f * t) * 0.08f;

            float stepDur = duration / melody.Length;
            int mi = Mathf.FloorToInt(t / stepDur) % melody.Length;
            float local = t - mi * stepDur;
            float pluckEnv = Mathf.Exp(-local * 3.8f) * Mathf.Clamp01(local / 0.015f);
            float fMel = melody[mi];
            float pluck = (Mathf.Sin(2f * Mathf.PI * fMel * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * fMel * t)) * pluckEnv * 0.18f;

            return (pad + pluck) * baseEnv;
        });
    }

    static AudioClip BuildNightAmbientLoop(float duration)
    {
        float[] pads = { 220f, 174.61f, 196f, 164.81f };
        return CreateClip("NightAmbient", duration, (t, _) =>
        {
            float step = duration / pads.Length;
            int pi = Mathf.FloorToInt(t / step) % pads.Length;
            float local = t - pi * step;
            float padEnv = Mathf.Sin(Mathf.PI * Mathf.Clamp01(local / step));
            float pad = (Mathf.Sin(2f * Mathf.PI * pads[pi] * 0.5f * t) * 0.18f
                      + Mathf.Sin(2f * Mathf.PI * pads[pi] * t) * 0.07f) * (0.35f + 0.65f * padEnv);

            // Grilo noturno rítmico e suave a cada ~1.5s.
            float cycle = t % 1.5f;
            float cricket = 0f;
            if (cycle < 0.24f)
            {
                float pulse = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 18f * cycle));
                cricket = Mathf.Sin(2f * Mathf.PI * 3950f * t) * pulse * 0.045f;
            }
            return pad + cricket;
        });
    }

    static AudioClip BuildShot() =>
        BuildSweep("Shot", 420f, 150f, 0.13f, 0.5f, noiseMix: 0.28f);

    static AudioClip BuildHit() =>
        BuildSweep("Hit", 260f, 95f, 0.1f, 0.55f, noiseMix: 0.35f);

    static AudioClip BuildPlayerHurt() =>
        BuildSweep("PlayerHurt", 170f, 65f, 0.18f, 0.6f, noiseMix: 0.18f);

    static AudioClip BuildEnemyDeath() =>
        BuildSweep("EnemyDeath", 340f, 75f, 0.22f, 0.55f, noiseMix: 0.3f);

    static AudioClip BuildCrumb()
    {
        return CreateClip("Crumb", 0.14f, (t, _) =>
        {
            float env = Mathf.Exp(-t * 24f) * Mathf.Clamp01(t / 0.004f);
            float wave = Mathf.Sin(2f * Mathf.PI * 1174.66f * t) * 0.7f
                       + Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.3f;
            return wave * env * 0.45f;
        });
    }

    static AudioClip BuildStompImpact()
    {
        uint seed = 918273645u;
        return CreateClip("StompImpact", 0.52f, (t, _) =>
        {
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 16) & 0xFFFF) / 32768f - 1f;
            float subEnv = Mathf.Exp(-t * 6.5f) * Mathf.Clamp01(t / 0.01f);
            float rumbleEnv = Mathf.Exp(-t * 10f);
            float freq = Mathf.Lerp(92f, 34f, Mathf.Clamp01(t / 0.35f));
            float sub = Mathf.Sin(2f * Mathf.PI * freq * t) * subEnv * 0.72f;
            return sub + noise * rumbleEnv * 0.28f;
        });
    }

    static AudioClip BuildSweep(string name, float startFreq, float endFreq, float duration, float gain, float noiseMix)
    {
        uint seed = 135792468u;
        return CreateClip(name, duration, (t, _) =>
        {
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 16) & 0xFFFF) / 32768f - 1f;
            float p = Mathf.Clamp01(t / duration);
            float freq = Mathf.Lerp(startFreq, endFreq, p);
            float env = Mathf.Pow(1f - p, 1.8f) * Mathf.Clamp01(t / 0.005f);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
            return Mathf.Lerp(tone, noise, noiseMix) * env * gain;
        });
    }

    static AudioClip BuildArpeggio(float[] notes, float noteStep, float tailDuration)
    {
        float total = noteStep * notes.Length + tailDuration;
        return CreateClip("Arpeggio", total, (t, _) =>
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float dt = t - i * noteStep;
                if (dt < 0f) continue;
                float env = Mathf.Exp(-dt * 11f) * Mathf.Clamp01(dt / 0.006f);
                sum += (Mathf.Sin(2f * Mathf.PI * notes[i] * dt) + 0.25f * Mathf.Sin(4f * Mathf.PI * notes[i] * dt)) * env * 0.22f;
            }
            return sum;
        });
    }
}
