using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FPS
{
    /// <summary>Owner-only viewmodel sampled from the medical action's server progress.</summary>
    public sealed class SelfInjectionAnimation : MonoBehaviour
    {
        [SerializeField] private GameObject viewmodelPrefab;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private GameObject healViewmodelPrefab;
        [SerializeField] private AnimationClip healClip;
        [SerializeField] private Transform viewCamera;
        [SerializeField] private Renderer[] originalHands;
        private GameObject viewmodel;
        private PlayableGraph graph;
        private AnimationClipPlayable motion;
        private bool[] previousHidden;
        private bool playingHeal;
        public bool IsPlaying { get; private set; }

        public bool Play(float progress) => Play(progress, false);
        public bool PlayHeal(float progress) => Play(progress, true);

        private bool Play(float progress, bool healing)
        {
            var prefab = healing ? healViewmodelPrefab : viewmodelPrefab;
            var animation = healing ? healClip : clip;
            if (!isActiveAndEnabled || prefab == null || animation == null || viewCamera == null
                || originalHands == null || originalHands.Length == 0) return false;
            if (viewmodel != null && playingHeal != healing)
            {
                Stop();
                graph.Destroy();
                viewmodel.SetActive(false);
                Destroy(viewmodel);
                viewmodel = null;
            }
            if (viewmodel == null)
            {
                playingHeal = healing;
                viewmodel = Instantiate(prefab, viewCamera, false);
                var animator = viewmodel.GetComponentInChildren<Animator>(true);
                if (animator == null)
                { Destroy(viewmodel); return false; }
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create(healing ? "Self Heal" : "Self Injection");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                motion = AnimationClipPlayable.Create(graph, animation);
                motion.SetSpeed(0);
                AnimationPlayableOutput.Create(graph, "Medical hands", animator).SetSourcePlayable(motion);
                previousHidden = new bool[originalHands.Length];
            }
            if (!IsPlaying)
            {
                for (var i = 0; i < originalHands.Length; i++)
                    if (originalHands[i] != null)
                    { previousHidden[i] = originalHands[i].forceRenderingOff; originalHands[i].forceRenderingOff = true; }
                viewmodel.SetActive(true);
                graph.Play();
                IsPlaying = true;
            }
            motion.SetTime(Mathf.Clamp01(progress) * animation.length);
            graph.Evaluate(0);
            return true;
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            for (var i = 0; i < originalHands.Length; i++)
                if (originalHands[i] != null) originalHands[i].forceRenderingOff = previousHidden[i];
            if (graph.IsValid()) graph.Stop();
            if (viewmodel != null) viewmodel.SetActive(false);
            IsPlaying = false;
        }

        private void OnDisable() => Stop();
        private void OnDestroy()
        {
            Stop();
            if (graph.IsValid()) graph.Destroy();
            if (viewmodel != null) Destroy(viewmodel);
        }
    }
}
