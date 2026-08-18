using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Serialization;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class ColdLedgerCinematicController : MonoBehaviour
    {
        [SerializeField] private FactoryMissionController missionController;
        [SerializeField] private PlayableDirector insertionDirector;
        [FormerlySerializedAs("extractionDirector")]
        [SerializeField] private PlayableDirector extractionOutroDirector;
        [SerializeField] private PlayableDirector extractionApproachDirector;
        [SerializeField] private GameObject cinematicSquadDoubles;

        private FactoryMissionState lastState = (FactoryMissionState)byte.MaxValue;

        private void OnEnable()
        {
            if (missionController == null)
                missionController = FactoryMissionController.Instance;
        }

        private void OnDisable()
        {
            InputManager.CinematicInputBlocked = false;
            if (cinematicSquadDoubles != null)
                cinematicSquadDoubles.SetActive(false);
        }

        private void Update()
        {
            if (missionController == null)
            {
                missionController = FactoryMissionController.Instance;
                if (missionController == null)
                    return;
            }

            FactoryMissionState state = missionController.State;
            bool insertionActive = state == FactoryMissionState.Insertion;
            bool extractionApproachActive = state == FactoryMissionState.ExtractionActive;
            bool extractionOutroActive = state == FactoryMissionState.Completed;
            InputManager.CinematicInputBlocked = insertionActive || extractionOutroActive;

            if (cinematicSquadDoubles != null)
                cinematicSquadDoubles.SetActive(insertionActive);

            if (insertionActive)
                SynchronizeDirector(insertionDirector, missionController.StateStartedServerTime);
            else if (extractionApproachActive)
                SynchronizeDirector(extractionApproachDirector, missionController.StateStartedServerTime);
            else if (extractionOutroActive)
                SynchronizeDirector(extractionOutroDirector, missionController.StateStartedServerTime);

            if (lastState != state)
            {
                if (!insertionActive)
                    StopDirector(insertionDirector);
                if (!extractionApproachActive)
                    StopDirector(extractionApproachDirector);
                if (!extractionOutroActive)
                    StopDirector(extractionOutroDirector);
                lastState = state;
            }
        }

        public void Configure(
            FactoryMissionController controller,
            PlayableDirector insertion,
            PlayableDirector extractionApproach,
            PlayableDirector extractionOutro,
            GameObject squadDoubles)
        {
            missionController = controller;
            insertionDirector = insertion;
            extractionApproachDirector = extractionApproach;
            extractionOutroDirector = extractionOutro;
            cinematicSquadDoubles = squadDoubles;
        }

        private static void SynchronizeDirector(PlayableDirector director, double stateStartedServerTime)
        {
            if (director == null || director.playableAsset == null)
                return;

            double serverNow = GetServerTime();
            double elapsed = System.Math.Max(0d, serverNow - stateStartedServerTime);
            double duration = director.playableAsset.duration;
            if (duration > 0d && elapsed >= duration)
            {
                director.time = duration;
                director.Evaluate();
                return;
            }

            if (director.state != PlayState.Playing)
                director.Play();

            if (System.Math.Abs(director.time - elapsed) > 0.1d)
            {
                director.time = elapsed;
                director.Evaluate();
            }
        }

        private static void StopDirector(PlayableDirector director)
        {
            if (director != null && director.state == PlayState.Playing)
                director.Stop();
        }

        private static double GetServerTime()
        {
            if (Unity.Netcode.NetworkManager.Singleton != null
                && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                return Unity.Netcode.NetworkManager.Singleton.ServerTime.Time;
            }

            return Time.timeAsDouble;
        }
    }
}
