#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>Review frames for the user: rest pose, leg hit and heavy chest hit at their peak, with blood.</summary>
    public sealed class EnemyHitReactionCapture
    {
        const string Output = "Reports/HitReaction-2026-11-13/";

        [UnityTest, Explicit("Writes review screenshots")]
        public IEnumerator CaptureReviewFrames()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Output);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Enemies/ZombieRig/Prefabs/ZombieRig.prefab");
            var enemy = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            enemy.GetComponentInChildren<Animator>().speed = 0f;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(1.6f, 1.35f, 2.4f);
            camera.transform.LookAt(new Vector3(0f, 0.95f, 0f));
            camera.fieldOfView = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.34f, 0.38f);
            var rt = new RenderTexture(960, 720, 24);
            camera.targetTexture = rt;
            for (int i = 0; i < 3; i++) yield return null;

            var health = enemy.GetComponent<EnemyHealth>();
            var segments = enemy.GetComponentsInChildren<HitboxSegment>(true);
            Vector3 shooter = camera.transform.position;
            yield return Shot(camera, rt, "01-rest.png");

            var leg = segments.First(s => s.Zone == HitboxZone.Leg);
            health.TakeDamage(new DamageInfo(5f, 1, 0, leg.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: HitboxZone.Leg, sourcePosition: shooter));
            yield return new WaitForSeconds(0.06f);
            yield return Shot(camera, rt, "02-leg-hit-peak.png");

            yield return new WaitForSeconds(1.8f);
            var chest = segments.First(s => s.Zone == HitboxZone.Chest);
            health.TakeDamage(new DamageInfo(health.MaxHealth * 0.3f, 1, 0, chest.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: HitboxZone.Chest, sourcePosition: shooter));
            yield return new WaitForSeconds(0.06f);
            yield return Shot(camera, rt, "03-heavy-chest-hit-peak.png");

            yield return new WaitForSeconds(0.12f);
            var head = segments.First(s => s.Zone == HitboxZone.Head);
            health.TakeDamage(new DamageInfo(8f, 1, 0, head.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: HitboxZone.Head, sourcePosition: shooter));
            yield return new WaitForSeconds(0.06f);
            yield return Shot(camera, rt, "04-head-hit-peak.png");

            camera.targetTexture = null;
            rt.Release();
            foreach (var o in new Object[] { enemy, sun.gameObject, camera.gameObject }) Object.Destroy(o);
            LogAssert.ignoreFailingMessages = false;
        }

        static IEnumerator Shot(Camera camera, RenderTexture rt, string file)
        {
            yield return new WaitForEndOfFrame();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Output, file), texture.EncodeToPNG());
            Object.Destroy(texture);
            Assert.That(File.Exists(Path.Combine(Output, file)), Is.True);
        }
    }
}
#endif
