using System.IO;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.Tests
{
    public sealed class HelicopterRappelTests
    {
        const string RopePath = "Assets/FPS/Features/World/Content/Vehicles/Helicopters/RappelRope/Prefabs/RappelRope.prefab";

        static void SaveAuthoredScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty && scene.path.StartsWith("Assets/FPS/Scenes/")) EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void RopeBendFollowsTheCabinDoorSill()
        {
            SaveAuthoredScenes();
            var scene=SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity");
            bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene("Assets/FPS/Scenes/GameScene.unity",OpenSceneMode.Additive);
            try
            {
            var rig=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<HelicopterInsertionRappel>(true)).Single();
            foreach(var anchor in rig.ropeAnchors)
            {
                float lane=rig.transform.InverseTransformPoint(anchor.position).z;
                var floor=new System.Collections.Generic.List<Vector3>();
                foreach(var filter in rig.doorModel.GetComponentsInChildren<MeshFilter>(true))
                {
                    var vertices=filter.sharedMesh.vertices.Select(v=>rig.transform.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
                    var triangles=filter.sharedMesh.triangles;
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                        if(Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y)<.85f || a.y<.8f || a.y>.9f)continue;
                        var slice=new System.Collections.Generic.List<Vector3>();
                        foreach(var edge in new[]{(a,b),(b,c),(c,a)})
                            if((edge.Item1.z-lane)*(edge.Item2.z-lane)<=0 && Mathf.Abs(edge.Item2.z-edge.Item1.z)>.00001f)
                                slice.Add(Vector3.Lerp(edge.Item1,edge.Item2,(lane-edge.Item1.z)/(edge.Item2.z-edge.Item1.z)));
                        if(slice.Count==2)floor.AddRange(slice.Where(v=>v.x<-.6f && v.x>-1.8f));
                    }
                }
                var lead=anchor.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="RopeLead");
                var bend=rig.transform.InverseTransformPoint(anchor.position);
                Assert.That(bend.x,Is.EqualTo(floor.Min(v=>v.x)).Within(.01f),"The bend sits at the outer door sill.");
                Assert.That(bend.y-.03f,Is.EqualTo(floor.Max(v=>v.y)).Within(.01f),"The rope rests on the sill.");
                var points=lead.sharedMesh.vertices.Select(v=>rig.transform.InverseTransformPoint(lead.transform.TransformPoint(v))).ToArray();
                float endX=points.Min(v=>v.x);
                var end=points.Where(v=>v.x<endX+.0001f).ToArray();
                var center=new Vector3(endX,(end.Min(v=>v.y)+end.Max(v=>v.y))*.5f,(end.Min(v=>v.z)+end.Max(v=>v.z))*.5f);
                Assert.That(Vector3.Distance(center,bend),Is.LessThan(.001f),"Horizontal and vertical rope ends meet without a gap.");
            }
            }
            finally
            {
            SaveAuthoredScenes();
            if(opened)EditorSceneManager.CloseScene(scene,true);
            }
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator GameSceneRopesAndCharacterTemplatesRenderAtTheNewTouchdownTimes()
        {
            SaveAuthoredScenes();
            var scene = SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity");
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/FPS/Scenes/GameScene.unity", OpenSceneMode.Additive);
            var root = new GameObject("AuthoredRappelProbe") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = null;
            RenderTexture target = null;
            var snapshots = new System.Collections.Generic.Dictionary<SkinnedMeshRenderer, Mesh>();
            var previousTarget = RenderTexture.active;
            try
            {
                var source = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HelicopterInsertionRappel>(true)).Single();
                var rig = Object.Instantiate(source, root.transform);
                rig.gameObject.SetActive(true);
                rig.ropes = source.ropes.Select(r => Object.Instantiate(r, root.transform)).ToArray();
                rig.rappellers = source.rappellers.Select(r => Object.Instantiate(r, root.transform)).ToArray();
                camera = Object.Instantiate(source.shotCamera, root.transform);
                rig.shotCamera = camera;
                var party = source.rappellerCharacters.Select((character, slot) => new CampaignInsertionParticipant
                {
                    playerId = (ulong)slot + 1, character = character, connected = true,
                    arrival = source.arrivals[slot].position, rotation = source.arrivals[slot].rotation
                }).ToArray();
                rig.SetParticipants(party);
                foreach (var participant in party)
                {
                    string character = participant.character.ToString();
                    var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"Assets/FPS/Features/Characters/Content/Players/{character}/{character}Player.prefab"), root.transform);
                    player.transform.SetPositionAndRotation(participant.arrival, participant.rotation);
                    foreach (var playerCamera in player.GetComponentsInChildren<Camera>(true)) playerCamera.enabled = false;
                    foreach (var listener in player.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                    var weapons = player.GetComponent<WeaponManager>();
                    Assert.That(weapons.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Warden), Is.True);
                    var visibility = player.GetComponent<PlayerVisibilityController>();
                    visibility.SetupVisibility(true);
                    var firstPersonRenderers = weapons.CurrentWeapon.GetComponentsInChildren<Renderer>(true)
                        .Concat(visibility.FirstPersonArms.GetComponentsInChildren<Renderer>(true)).ToArray();
                    Assert.That(firstPersonRenderers, Is.Not.Empty);
                    foreach (var renderer in firstPersonRenderers)
                        Assert.That(camera.cullingMask & (1 << renderer.gameObject.layer), Is.Zero,
                            $"{character}: the insertion camera must exclude the first-person {renderer.name}.");
                }
                Assert.That(camera.cullingMask & (1 << LayerMask.NameToLayer("ThirdPerson")), Is.Not.Zero,
                    "The cinematic still renders third-person bodies and held weapons.");
                target = new RenderTexture(1280, 720, 24);
                camera.targetTexture = target;
                Directory.CreateDirectory("Documents/HelicopterInsertion/RappelRope");
                var measurements = new System.Collections.Generic.List<string>();
                var samples = new System.Collections.Generic.List<(float time, int slot)>();
                foreach (float time in new[] { 7.9f, 8.3f, 9.75f, 10.64f, 10.65f, 10.85f, 11.10f, 11.35f, 11.65f, 12f, 16.15f,
                    17.79f,17.8f,18.3f,18.8f,19.25f,19.6f,19.79f,19.8f,20.6f,21.5f,24f })
                    samples.Add((time, 0));
                for (int slot = 0; slot < party.Length; slot++)
                    foreach (float offset in new[] { .5f, .9f, 1.3f, 2.5f, 3.5f })
                        samples.Add((8.2f + slot % 2 * .7f + slot / 2 * 4.6f + .65f + rig.descentSeconds + offset, slot));
                foreach (var sample in samples)
                {
                    float time = sample.time;
                    rig.Evaluate(time);
                    foreach (var rope in rig.ropes)
                    {
                        Assert.That(rope.GetComponent<LineRenderer>(), Is.Null);
                        var renderer = rope.GetComponentInChildren<MeshRenderer>(true);
                        Assert.That(renderer, Is.Not.Null);
                        Assert.That(renderer.sharedMaterial.name, Is.EqualTo("RappelRope_Woven"));
                    }
                    for (int slot = 0; slot < party.Length; slot++)
                    {
                        float touchdown = 8.2f + slot % 2 * .7f + slot / 2 * 4.6f + .65f + 1.8f;
                        Assert.That(rig.Actors[slot].IsArmed, Is.EqualTo(time >= touchdown+.45f));
                    }
                    var actor = rig.Actors[sample.slot];
                    measurements.Add($"{party[sample.slot].character} {time:F3}: root={actor.transform.position:F4} leftFoot={actor.leftFoot.position:F4} rightFoot={actor.rightFoot.position:F4} leftHand={actor.leftHand.position:F4} rightHand={actor.rightHand.position:F4}");
                    foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var baked = new Mesh();
                        skin.BakeMesh(baked, false);
                        measurements.Add($"skin={skin.name} bottom={baked.vertices.Min(v => (skin.transform.position+skin.transform.rotation*v).y):F4} footBinding={skin.bones.Contains(actor.leftFoot)}");
                        Object.DestroyImmediate(baked);
                    }
                    Vector3 focus = actor.transform.position + Vector3.up * .9f;
                    camera.transform.SetPositionAndRotation(focus + new Vector3(-2.6f, .4f, -3.5f), Quaternion.LookRotation(new Vector3(2.6f, -.4f, 3.5f)));
                    camera.fieldOfView = 38;
                    if(time==7.9f || time==8.3f)
                    {
                        var focusAnchor=rig.ropeAnchors[0].position+rig.transform.right*.5f;
                        camera.transform.SetPositionAndRotation(focusAnchor+rig.transform.rotation*new Vector3(-2,1.5f,-2),Quaternion.LookRotation(focusAnchor-(focusAnchor+rig.transform.rotation*new Vector3(-2,1.5f,-2))));
                    }
                    if(time>=17.8f)
                    {
                        Vector3 courtyard=party.Aggregate(Vector3.zero,(sum,p)=>sum+p.arrival)/party.Length+Vector3.up*4;
                        camera.transform.SetPositionAndRotation(courtyard+new Vector3(-8,3,-10),Quaternion.LookRotation(new Vector3(8,-3,10)));
                        camera.fieldOfView=60;
                        for(int lane=0;lane<rig.ropes.Length;lane++)
                        {
                            var rope=rig.ropes[lane];
                            var renderer=rope.GetComponentInChildren<MeshRenderer>(true);
                            Assert.That(rope.gameObject.activeSelf,Is.EqualTo(time<19.8f),"The hanging rope is stowed before the door closes.");
                            if(time<19.8f)
                            {
                                float top=rig.ropeAnchors[lane].position.y;
                                float ground=party.Average(p=>p.arrival.y)-.11f;
                                float retrieved=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-17.8f)/2));
                                Assert.That(renderer.bounds.max.y,Is.EqualTo(top).Within(.01f),"The top stays attached at the sill while hauling.");
                                Assert.That(renderer.bounds.min.y,Is.EqualTo(Mathf.Lerp(ground,top,retrieved)).Within(.01f),"The lower end rises into the cabin.");
                                var filter=rope.GetComponentInChildren<MeshFilter>(true);
                                Assert.That(filter.transform.TransformVector(Vector3.right*filter.sharedMesh.bounds.size.x).magnitude,
                                    Is.EqualTo(.06f).Within(.005f),"Hauling preserves the rope diameter even when tilted.");
                            }
                            var anchor=rig.floorAttachments[lane];
                            Assert.That(anchor.gameObject.activeInHierarchy,Is.True,"The metal floor attachment stays on the aircraft.");
                            var lead=anchor.transform.parent.Find("RopeLead").GetComponent<Renderer>();
                            Assert.That(lead.enabled,Is.EqualTo(time<19.8f),"The floor lead stays connected until retrieval completes.");
                        }
                    }
                    if(time==19.25f)
                    {
                        Vector3 focusAnchor=rig.ropeAnchors[0].position+rig.transform.right*.5f;
                        Vector3 eye=focusAnchor+rig.transform.rotation*new Vector3(-2,1.5f,-2);
                        camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focusAnchor-eye));
                        camera.fieldOfView=38;
                        foreach(var rope in rig.ropes)
                        {
                            var filter=rope.GetComponentInChildren<MeshFilter>();
                            int lane=System.Array.IndexOf(rig.ropes,rope);
                            Assert.That(filter.sharedMesh.vertices.Min(v=>Vector3.Distance(filter.transform.TransformPoint(v),rig.ropeAnchors[lane].position)),
                                Is.LessThan(.045f),"The rope stays joined to the floor lead at the sill during retrieval.");
                        }
                    }
                    foreach(var skin in rig.Actors.SelectMany(a=>a.GetComponentsInChildren<SkinnedMeshRenderer>()))
                    {
                        if(!snapshots.TryGetValue(skin,out var baked))
                        {
                            baked=new Mesh(); snapshots.Add(skin,baked);
                            var preview=new GameObject("BakedPoseReview",typeof(MeshFilter),typeof(MeshRenderer));
                            preview.transform.SetParent(skin.transform,false);
                            var scale=skin.transform.lossyScale;
                            preview.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                            preview.GetComponent<MeshFilter>().sharedMesh=baked;
                            preview.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                            skin.forceRenderingOff=true;
                        }
                        skin.BakeMesh(baked,false);
                    }
                    yield return null;
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                    try
                    {
                        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                        image.Apply();
                        File.WriteAllBytes($"Documents/HelicopterInsertion/RappelRope/Unity_{time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}.png", image.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(image); }
                }
                File.WriteAllLines("Documents/HelicopterInsertion/RappelRope/LandingMeasurements.txt", measurements);
                rig.Evaluate(11.35f);
                Assert.That(Vector3.Distance(rig.Actors[0].transform.position,rig.Actors[1].transform.position),
                    Is.GreaterThan(.75f),"The two landing lanes must not cross.");
                for(int slot=0;slot<party.Length;slot++)
                {
                    float touchdown=8.2f+slot%2*.7f+slot/2*4.6f+.65f+rig.descentSeconds;
                    rig.Evaluate(touchdown-.001f);
                    Vector3 before=rig.Actors[slot].transform.position;
                    rig.Evaluate(touchdown+.001f);
                    Assert.That(Vector3.Distance(before,rig.Actors[slot].transform.position),Is.LessThan(.02f),"No root teleport at touchdown.");
                    var equippedActor=rig.Actors[slot];
                    Assert.That(equippedActor.armedEquip,Is.Not.Null,"Every character has its own insertion equip clip.");
                    Assert.That(equippedActor.armedEquip.isLooping,Is.False,"Equip plays once before armed idle.");
                    Assert.That(equippedActor.armedEquip.length,Is.EqualTo(1.8333334f).Within(.01f),"Equip uses the action duration, not the longer idle pose keys.");
                    foreach(var binding in AnimationUtility.GetCurveBindings(equippedActor.armedEquip).Where(b=>b.type==typeof(Transform)))
                        Assert.That(string.IsNullOrEmpty(binding.path)||equippedActor.transform.Find(binding.path)!=null,Is.True,binding.path);
                    rig.Evaluate(touchdown+.55f);
                    Vector3 equipPosition=equippedActor.transform.position;
                    Vector3 handAtStart=equippedActor.leftHand.position;
                    rig.Evaluate(touchdown+1.3f);
                    Assert.That(Vector3.Distance(handAtStart,equippedActor.leftHand.position),Is.GreaterThan(.05f),"The equip clip moves the hand instead of displaying an idle pose.");
                    Assert.That(Vector3.Distance(equipPosition,equippedActor.transform.position),Is.LessThan(.001f),"Feet stay at the landing point while equipping.");
                    Vector3 equippedHand=equippedActor.leftHand.position;
                    rig.Evaluate(touchdown+equippedActor.EquipEnd+1);
                    rig.Evaluate(touchdown+1.3f);
                    Assert.That(Vector3.Distance(equippedHand,equippedActor.leftHand.position),Is.LessThan(.001f),"Seeking back restores the equip pose.");
                    foreach(float boundary in new[]{.45f,equippedActor.EquipEnd})
                    {
                        rig.Evaluate(touchdown+boundary-.001f);
                        Vector3 left=equippedActor.leftHand.position,right=equippedActor.rightHand.position;
                        rig.Evaluate(touchdown+boundary+.001f);
                        Assert.That(Vector3.Distance(left,equippedActor.leftHand.position),Is.LessThan(.06f),"Left hand transitions continuously through equip.");
                        Assert.That(Vector3.Distance(right,equippedActor.rightHand.position),Is.LessThan(.06f),"Right hand transitions continuously through equip.");
                    }
                    rig.Evaluate(touchdown+equippedActor.EquipEnd+.01f);
                    var planted=new[]{equippedActor.leftFoot,equippedActor.rightFoot};
                    var plantedPositions=planted.Select(j=>j.position).ToArray();
                    foreach(float time in new[]{touchdown+equippedActor.EquipEnd+.3f,touchdown+equippedActor.EquipEnd+1.2f,20f,24f})
                    {
                        rig.Evaluate(time);
                        Assert.That(Vector3.Distance(equippedActor.transform.position,party[slot].arrival-Vector3.up*.15f),
                            Is.LessThan(.001f),"Equipped actors stand at their own landing points, including during departure.");
                        Assert.That(Quaternion.Angle(equippedActor.transform.rotation,party[slot].rotation),Is.LessThan(.01f),
                            "The landing turn finishes before equip; idle does not turn toward a walking path.");
                        for(int joint=0;joint<planted.Length;joint++)
                            Assert.That(Vector3.Distance(plantedPositions[joint],planted[joint].position),Is.LessThan(.01f),
                                $"{party[slot].character} {planted[joint].name}: allow subtle idle sway, but no walk cycle or foot sliding.");
                    }
                    foreach(float offset in new[]{0f,.2f,.5f,.8f,1.1f,1.8f,equippedActor.EquipEnd+.2f,equippedActor.EquipEnd+1.2f})
                    {
                        rig.Evaluate(touchdown+offset);
                        var landedActor=rig.Actors[slot];
                        var body=landedActor.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.bones.Contains(landedActor.leftFoot));
                        var baked=new Mesh();body.BakeMesh(baked,false);
                        float bottom=baked.vertices.Min(v=>(body.transform.position+body.transform.rotation*v).y);
                        Object.DestroyImmediate(baked);
                        Assert.That(bottom,Is.EqualTo(party[slot].arrival.y-.15f).Within(.035f),$"{party[slot].character} soles at {offset:F2}s after touchdown.");
                    }
                }
                rig.Evaluate(20f);
                for(int slot=0;slot<party.Length;slot++)
                    Assert.That(Vector3.Distance(rig.Actors[slot].transform.position,party[slot].arrival-Vector3.up*.15f),Is.LessThan(.01f),
                        "Every character remains at their own landing point after equipping.");
                for(int first=0;first<party.Length;first++)
                    for(int second=first+1;second<party.Length;second++)
                        Assert.That(Vector3.Distance(rig.Actors[first].transform.position,rig.Actors[second].transform.position),
                            Is.GreaterThan(.75f),"Successive pairs must not overlap after removing the walk.");
                rig.StopPresentation();
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (camera != null) camera.targetTexture = null;
                if (target != null) Object.DestroyImmediate(target);
                Object.DestroyImmediate(root);
                foreach(var mesh in snapshots.Values)Object.DestroyImmediate(mesh);
                SaveAuthoredScenes();
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ImportedRopesAndFourActorsFollowTheFasterDescentWhenSeeking()
        {
            var root = new GameObject("RappelProbe") { hideFlags = HideFlags.HideAndDontSave };
            Transform Child(string name, Transform parent)
            {
                var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                child.transform.SetParent(parent, false);
                return child.transform;
            }
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RopePath);
                Assert.That(prefab, Is.Not.Null);
                var rig = Child("Aircraft", root.transform).gameObject.AddComponent<HelicopterInsertionRappel>();
                rig.visuals = Child("Visuals", rig.transform).gameObject;
                rig.approach = Child("Approach", root.transform);
                rig.hover = Child("Hover", root.transform);
                rig.hover.position = Vector3.up * 12;
                rig.departure = Child("Departure", root.transform);
                rig.ropeAnchors = new[] { Child("Anchor0", rig.transform), Child("Anchor1", rig.transform) };
                rig.ropeAnchors[0].localPosition = Vector3.left * .5f;
                rig.ropeAnchors[1].localPosition = Vector3.right * .5f;
                rig.ropes = new[] { Object.Instantiate(prefab, root.transform).transform, Object.Instantiate(prefab, root.transform).transform };
                var pose = Child("Template", root.transform).gameObject.AddComponent<HelicopterRappellerPose>();
                pose.readyWeapon = Child("Weapon", pose.transform).gameObject;
                rig.rappellers = new[] { pose };
                rig.rappellerCharacters = new[] { PlayerCharacterId.Brimstone };
                rig.arrivals = new[] { Child("Ground", root.transform) };
                rig.arrivals[0].position = Vector3.up * .15f;
                var party = new CampaignInsertionParticipant[4];
                for (int slot = 0; slot < party.Length; slot++)
                    party[slot] = new CampaignInsertionParticipant { playerId = (ulong)slot + 1, character = PlayerCharacterId.Brimstone,
                        connected = true, arrival = Vector3.up * .15f, rotation = Quaternion.identity };
                rig.SetParticipants(party);

                Assert.That(rig.descentSeconds, Is.EqualTo(1.8f));
                rig.Evaluate(9.75f);
                float hangY = rig.ropeAnchors[0].position.y - 1.95f;
                Assert.That(rig.Actors[0].transform.position.y, Is.EqualTo(hangY * .5f).Within(.01f));
                Assert.That(rig.Actors[0].IsArmed, Is.False);
                for (int lane = 0; lane < 2; lane++)
                {
                    var renderer = rig.ropes[lane].GetComponentInChildren<MeshRenderer>();
                    Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Standard"));
                    Assert.That(renderer.sharedMaterial.GetTexture("_BumpMap"), Is.Not.Null);
                    Assert.That(renderer.bounds.max.y, Is.EqualTo(rig.ropeAnchors[lane].position.y).Within(.01f));
                    Assert.That(renderer.bounds.min.y, Is.EqualTo(.04f).Within(.01f));
                    var filter=rig.ropes[lane].GetComponentInChildren<MeshFilter>();
                    Assert.That(filter.transform.TransformVector(Vector3.right*filter.sharedMesh.bounds.size.x).magnitude,
                        Is.EqualTo(.06f).Within(.005f));
                }
                for (int slot = 0; slot < 4; slot++)
                {
                    rig.Evaluate(8.2f + slot % 2 * .7f + slot / 2 * 4.6f + .65f + 1.8f + .01f);
                    Assert.That(rig.Actors[slot].transform.position.y, Is.EqualTo(0).Within(.01f));
                    Assert.That(rig.Actors[slot].IsArmed, Is.False, "Hands release the rope before drawing the weapon.");
                    rig.Evaluate(8.2f + slot % 2 * .7f + slot / 2 * 4.6f + .65f + 1.8f + .46f);
                    Assert.That(rig.Actors[slot].IsArmed, Is.True);
                }
                rig.Evaluate(17.79f);
                var hauled=rig.ropes[0].GetComponentInChildren<MeshFilter>();
                var ropeRenderer=hauled.GetComponent<Renderer>();
                float fullLength=ropeRenderer.bounds.size.y;
                float previousLength=fullLength;
                foreach(float time in new[]{17.8f,18.3f,18.8f,19.3f,19.79f})
                {
                    rig.Evaluate(time);
                    Assert.That(rig.Actors.All(a=>a.IsArmed),Is.True,"All four actors release the ropes before hauling starts.");
                    Assert.That(ropeRenderer.bounds.max.y,Is.EqualTo(rig.ropeAnchors[0].position.y).Within(.01f),"The rope remains attached to the aircraft.");
                    Assert.That(ropeRenderer.bounds.size.y,Is.LessThanOrEqualTo(previousLength+.01f),"The hanging length decreases as the lower end is pulled upward.");
                    previousLength=ropeRenderer.bounds.size.y;
                }
                Assert.That(previousLength,Is.LessThan(fullLength*.001f),"Retrieval reaches the sill before hiding the rope.");
                foreach(float time in new[]{19.8f,20f,21.2f,24f})
                {
                    rig.Evaluate(time);
                    Assert.That(rig.ropes.All(r=>!r.gameObject.activeSelf),Is.True,"No rope is left on the ground after retrieval.");
                    Assert.That(rig.ropeAnchors.All(a=>a.gameObject.activeSelf),Is.True,"The floor attachment remains with the aircraft.");
                }
                rig.Evaluate(9.75f);
                Assert.That(hauled.sharedMesh,Is.SameAs(prefab.GetComponentInChildren<MeshFilter>().sharedMesh),"Retrieval keeps the original imported mesh.");
                Assert.That(rig.ropes[0].gameObject.activeSelf,Is.True,"Seeking backwards redeploys the rope.");
                Assert.That(ropeRenderer.bounds.min.y,Is.EqualTo(.04f).Within(.01f));
                Assert.That(rig.Actors[0].IsArmed, Is.False, "Seeking backwards must restore the hanging pose.");
                party[0].connected = party[2].connected = false;
                rig.SetParticipants(party);
                rig.Evaluate(12);
                Assert.That(rig.ropes[0].gameObject.activeSelf, Is.False);
                Assert.That(rig.ropes[1].gameObject.activeSelf, Is.True);
                rig.descentSeconds = 0;
                rig.Evaluate(9);
                Assert.That(rig.Actors[1].transform.position.y, Is.Not.NaN);
                rig.StopPresentation();
                Assert.That(rig.ropes[1].gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
