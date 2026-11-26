using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Fase 3: poça d'água (issue #31, Test Runner > EditMode): água rasa deixa o player lento;
/// em cima de uma folha boiando ele fica acima da água e anda normal.
/// </summary>
public class PocaSmokeTest
{
    [UnityTest]
    public IEnumerator AguaDeixaLentoEFolhaBoiandoNao()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        yield return StageTestUtils.GoToStage(3, "Poça d'água");
        StageTestUtils.Quiet();
        var player = GameObject.FindGameObjectWithTag("Player");
        var movement = player.GetComponent<PlayerMovement>();
        player.GetComponent<Health>().Invincible = true;

        var water = SlowZone.Active.Where(z => z.ZoneKind == SlowZone.Kind.Water).ToList();
        var leaves = Object.FindObjectsByType<FloatingLeaf>();
        Debug.Log($"[Smoke] poça: {water.Count} discos de água, {leaves.Length} folhas");
        Assert.GreaterOrEqual(water.Count, 6, "três poças");
        Assert.GreaterOrEqual(leaves.Length, 7, "folhas boiando");
        Assert.IsTrue(GameObject.Find("Backyard") != null, "o terreno do quintal continua");
        yield return StageTestUtils.Wait(0.5f);
        StageTestUtils.Capture("3_Poca");

        // Na água, longe das folhas: lento
        var main = water.OrderByDescending(z => z.Radius).First();
        Vector3 spot = main.transform.position;
        for (int i = 0; i < 40; i++)
        {
            float a = i * 0.7f, d = main.Radius * 0.6f * (i % 5) / 4f;
            var p = main.transform.position + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            if (leaves.All(l => Vector3.Distance(Flat(l.transform.position), Flat(p)) > 4.5f)) { spot = p; break; }
        }
        StageTestUtils.Teleport(player, spot + Vector3.up * 0.2f);
        yield return StageTestUtils.Wait(0.4f);
        Assert.IsTrue(SlowZone.IsIn(player.transform.position, SlowZone.Kind.Water), "dentro da água");
        Assert.AreEqual(PocaBuilder.WaterSlow, movement.SurfaceSpeedFactor, 0.02f, "lento na água");

        // Em cima de uma folha: acima da água, anda normal
        var leaf = leaves.OrderBy(l => Vector3.Distance(l.transform.position, main.transform.position)).First();
        var col = leaf.GetComponent<BoxCollider>();
        StageTestUtils.Teleport(player, new Vector3(col.bounds.center.x, col.bounds.max.y + 0.3f, col.bounds.center.z));
        yield return StageTestUtils.Wait(1.3f);
        Debug.Log($"[Smoke] poça: na folha y={player.transform.position.y:0.00}, velocidade x{movement.SurfaceSpeedFactor:0.00}");
        Assert.IsTrue(movement.IsGrounded, "parado em cima da folha");
        Assert.Greater(player.transform.position.y, PocaBuilder.WaterLevel + 0.1f, "acima da água");
        Assert.AreEqual(1f, movement.SurfaceSpeedFactor, 0.01f, "na folha anda normal");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
