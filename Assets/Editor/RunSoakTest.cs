using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// "Run completa" automatizada (issue #62): roda a cena do jogo acelerada por alguns minutos
/// de run com o player invencível, matando os inimigos de tempos em tempos (migalhas, drops,
/// pools, itens "ao matar"), passa para a fase 2 (itens/vida levados, chefe que a fase define) e no fim
/// confere que não houve exceção e que o jogo continua saudável (inimigos dentro do limite,
/// dificuldade subindo). Imprime um resumo para balancear a curva de dificuldade.
/// Demora alguns minutos: Window > General > Test Runner > EditMode > RunSoakTest.
/// </summary>
public class RunSoakTest
{
    const float RunSeconds = 300f;     // tempo de run simulado na fase 1
    const float TimeScale = 4f;

    readonly List<string> exceptions = new();
    readonly List<string> errors = new();

    void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception) exceptions.Add(message + "\n" + stack.Split('\n').FirstOrDefault());
        else if (type == LogType.Error || type == LogType.Assert) errors.Add(message);
    }

    [UnityTest, Timeout(900000)]
    public IEnumerator RunCompletaSemErros()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceived += OnLog;
        try
        {
            for (int i = 0; i < 10; i++) yield return null;
            var player = GameObject.FindGameObjectWithTag("Player");
            var health = player.GetComponent<Health>();
            health.Invincible = true;
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            var stomp = Object.FindAnyObjectByType<StompDirector>();
            var rain = Object.FindAnyObjectByType<RainEvent>();
            var lupa = Object.FindAnyObjectByType<MagnifierEvent>();
            var wallet = player.GetComponent<PlayerWallet>();
            Time.timeScale = TimeScale;

            int maxAlive = 0, killed = 0;
            float nextKill = Time.time + 8f, nextReport = Time.time + 60f;
            var report = new List<string>();
            while (RunManager.Instance.RunTime < RunSeconds)
            {
                health.Invincible = true;                       // habilidades mexem nisso
                if (health.Current < health.Max * 0.5f) health.SetCurrent(health.Max); // pisão/chuva ignoram invencível? não deixa morrer
                maxAlive = Mathf.Max(maxAlive, spawner != null ? spawner.AliveCount : 0);
                if (Time.time >= nextKill)
                {
                    nextKill = Time.time + 8f;
                    foreach (var e in EnemyBase.Active.ToArray())
                        if (e != null && e.IsAlive && !e.IsBoss) { e.Health.Kill(player); killed++; }
                }
                if (Time.time >= nextReport)
                {
                    nextReport = Time.time + 60f;
                    var dm = DifficultyManager.Instance;
                    report.Add($"  {RunManager.Instance.RunTime / 60f:0.0} min: coef {dm.Coefficient:0.00} ({dm.LevelName}), vivos {spawner.AliveCount}, " +
                               $"nascidos {spawner.TotalSpawned}, vida inimigo x{dm.EnemyHealthMultiplier:0.00}, dano x{dm.EnemyDamageMultiplier:0.00}");
                }
                yield return null;
            }
            Time.timeScale = 1f;

            Debug.Log("[Soak] fase 1:\n" + string.Join("\n", report) +
                      $"\n  abates {killed}, kills do player {RunManager.Instance.Kills}, máx. vivos {maxAlive}, migalhas {wallet.TotalCollected}, " +
                      $"pisões {stomp?.StompCount}, chuvas {rain?.RainCount} (gotas {rain?.DropCount}), lupas {lupa?.Count}, " +
                      $"pool de inimigos {spawner.PooledObjectCount}");

            Assert.Greater(spawner.TotalSpawned, 50, "inimigos nasceram durante a run");
            Assert.LessOrEqual(maxAlive, 45, "respeitou o máximo de inimigos vivos");
            Assert.Greater(DifficultyManager.Instance.Coefficient, 2f, "dificuldade subiu com o tempo");
            Assert.Greater(wallet.TotalCollected, 0, "migalhas caíram dos abates");

            // ---- Fase 2: leva itens/vida/tempo, o chefe que a fase define nasce sem erro
            float runTime = RunManager.Instance.RunTime;
            int crumbs = wallet.Crumbs;
            RunManager.Instance.GoToNextStage();
            for (int i = 0; i < 20; i++) yield return null;
            player = GameObject.FindGameObjectWithTag("Player");
            player.GetComponent<Health>().Invincible = true;
            Assert.AreEqual(2, RunManager.Instance.Stage, "fase 2");
            Assert.GreaterOrEqual(RunManager.Instance.RunTime, runTime, "tempo da run continuou");
            Assert.AreEqual(crumbs, player.GetComponent<PlayerWallet>().Crumbs, "migalhas levadas");
            var bossSpawner = Object.FindAnyObjectByType<BossSpawner>();
            bossSpawner.SpawnBossAtStart();
            Assert.IsNotNull(bossSpawner.Spawned, "chefe da fase 2 nasceu");
            Assert.IsTrue(bossSpawner.Spawned.IsBoss);
            string bossName = bossSpawner.Spawned.GetType().Name;
            Time.timeScale = TimeScale;
            for (float end = Time.time + 60f; Time.time < end;) { player.GetComponent<Health>().Invincible = true; yield return null; }
            Time.timeScale = 1f;
            Debug.Log($"[Soak] fase 2: 60 s com o chefe {bossName} (vivo: {bossSpawner.Spawned != null && bossSpawner.Spawned.IsAlive})");
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
        }

        var uniqueErrors = errors.GroupBy(e => e.Split('\n')[0]).Select(g => $"  ({g.Count()}x) {g.Key}").ToList();
        Debug.Log($"[Soak] exceções: {exceptions.Count}, erros: {errors.Count}\n" + string.Join("\n", exceptions.Distinct().Take(10)) + "\n" + string.Join("\n", uniqueErrors.Take(15)));
        Assert.AreEqual(0, exceptions.Count, "nenhuma exceção durante a run:\n" + string.Join("\n", exceptions.Distinct().Take(5)));
        yield return new ExitPlayMode();
    }
}
