# Blood spray

- `Prefabs/BloodSprayEffect.prefab`, `Materials/BloodSplatParticle.mat`, `Textures/BloodSprayNormals.tif`
- Source: "Unity Particle Pack 5.x" by Unity Technologies (title in its `Readme.asset`), previously under
  `Assets/ThirdParty/UnityTechnologies/EffectExamples/Blood/`. Moved here on 2026-11-13 with
  `AssetDatabase.MoveAsset`, so GUIDs and references are unchanged.
- License: the pack ships no license file. Its readme says the sample assets "can be used in your
  games". Treat it as Unity Asset Store content under the Asset Store EULA (inference, not verified).
- Used by: `EnemyHitReactionPresenter.bloodPrefab` on every enemy prefab. The presenter turns the
  prefab's looping off on each spawned copy so a wound plays one cycle.
