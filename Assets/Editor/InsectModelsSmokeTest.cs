using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Teste de fumaça dos modelos FBX nos prefabs: peças, referências e pernas animando.</summary>
public class InsectModelsSmokeTest
{
    [UnityTest]
    public IEnumerator InimigosComModeloAnimam()
    {
        LogAssert.ignoreFailingMessages = true; // erros de outros sistemas não derrubam este teste
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;

        var player = GameObject.FindGameObjectWithTag("Player").transform;
        foreach (var n in new[] { "WorkerAnt", "SoldierAnt", "Aphid", "BombardierBeetle", "MantisBoss" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{n}.prefab");
            var pos = player.position + player.forward * 14f;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 10f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
            var go = Object.Instantiate(prefab, pos, Quaternion.identity);
            var anim = go.GetComponent<ProceduralInsectAnimator>();
            Assert.IsNotNull(anim, $"{n}: ProceduralInsectAnimator");
            Assert.IsNotNull(go.transform.Find(n == "BombardierBeetle" ? "Model" : "Body/Model"), $"{n}: Model");
            int expectedLegs = n == "MantisBoss" ? 4 : 6;
            Assert.AreEqual(expectedLegs, anim.LegCount, $"{n}: pernas");

            var leg = go.GetComponentInChildren<ProceduralInsectAnimator>().transform.GetComponentsInChildren<Transform>();
            Transform leg1 = null;
            foreach (var t in leg) if (t.name.StartsWith("Leg_")) { leg1 = t; break; }
            var rest = leg1.localRotation;
            float maxMove = 0f, maxAngle = 0f;
            for (float end = Time.time + 2.5f; Time.time < end;)
            {
                yield return null;
                if (go == null) break;
                maxMove = Mathf.Max(maxMove, anim.MoveAmount);
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(rest, leg1.localRotation));
            }
            Debug.Log($"[Smoke] {n}: moveAmount max {maxMove:0.00}, perna girou até {maxAngle:0.0}°");
            if (n != "MantisBoss" && n != "BombardierBeetle") Assert.Greater(maxAngle, 3f, $"{n}: perna animou");

            if (n == "BombardierBeetle")
            {
                var so = new SerializedObject(go.GetComponent<BombardierBeetle>());
                Assert.AreEqual("Nozzle", ((Transform)so.FindProperty("nozzle").objectReferenceValue).name);
            }
            if (n == "MantisBoss")
            {
                var so = new SerializedObject(go.GetComponent<MantisBoss>());
                Assert.AreEqual("Claw_L", ((Transform)so.FindProperty("clawLeft").objectReferenceValue).name);
                Assert.AreEqual(2, so.FindProperty("clawRenderers").arraySize);
            }
            Object.Destroy(go);
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SquashFlashEBobDoCorpo()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;

        var player = GameObject.FindGameObjectWithTag("Player").transform;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/WorkerAnt.prefab");
        var pos = player.position + player.forward * 14f;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 10f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
        var go = Object.Instantiate(prefab, pos, Quaternion.identity);
        var anim = go.GetComponent<ProceduralInsectAnimator>();
        yield return null;
        var model = anim.Model;
        Assert.IsNotNull(model, "achou a raiz do modelo pela convenção de nomes");
        Vector3 restScale = model.localScale;

        // bob: andando, o Body do modelo sobe e desce
        var body = model.Find("Body");
        float minY = float.MaxValue, maxY = float.MinValue;
        for (float end = Time.time + 1.2f; Time.time < end;)
        {
            yield return null;
            minY = Mathf.Min(minY, body.localPosition.y); maxY = Mathf.Max(maxY, body.localPosition.y);
        }
        Debug.Log($"[Smoke] bob: {minY:0.000}..{maxY:0.000} (move {anim.MoveAmount:0.00})");
        Assert.Greater(maxY - minY, 0.005f, "corpo balança andando");

        // dano que não vem do player (o GameFeel não pisca): o animador faz squash + flash
        var health = go.GetComponent<Health>();
        health.TakeDamage(new DamageInfo(5f, null, go.transform.position, Vector3.zero));
        float maxSquash = 0f; bool flashed = false;
        for (float end = Time.time + 0.15f; Time.time < end;)
        {
            yield return null;
            maxSquash = Mathf.Max(maxSquash, Mathf.Abs(anim.CurrentSquash));
            flashed |= go.TryGetComponent(out HitFlash f) && f.IsFlashing;
        }
        Debug.Log($"[Smoke] squash max {maxSquash:0.00}, flash={flashed}");
        Assert.Greater(maxSquash, 0.05f, "squash ao levar dano");
        Assert.IsTrue(flashed, "flash branco ao levar dano");
        for (float end = Time.time + 0.4f; Time.time < end;) yield return null;
        Assert.That(Vector3.Distance(model.localScale, restScale), Is.LessThan(0.001f), "volta ao tamanho normal");

        Object.Destroy(go);
        yield return new ExitPlayMode();
    }
}
