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
            var anim = go.GetComponent<InsectAnimator>();
            Assert.IsNotNull(anim, $"{n}: InsectAnimator");
            Assert.IsNotNull(go.transform.Find(n == "BombardierBeetle" ? "Model" : "Body/Model"), $"{n}: Model");
            int expectedLegs = n == "MantisBoss" ? 4 : 6;
            Assert.AreEqual(expectedLegs, anim.LegCount, $"{n}: pernas");

            var leg = go.GetComponentInChildren<InsectAnimator>().transform.GetComponentsInChildren<Transform>();
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
}
