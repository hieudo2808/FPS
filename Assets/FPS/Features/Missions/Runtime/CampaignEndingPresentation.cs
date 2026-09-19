using UnityEngine;

namespace FPS
{
    /// <summary>A short, synchronized view of the existing service dock after evacuation.</summary>
    public sealed class CampaignEndingPresentation : MonoBehaviour
    {
        public Camera shotCamera;
        public Transform startPose;
        public Transform endPose;
        private void Awake() { if(shotCamera!=null) shotCamera.enabled=false; }
        private void OnDisable() { if(shotCamera!=null) shotCamera.enabled=false; }
        private void LateUpdate()
        {
            var campaign=CampaignMissionController.Instance;
            bool ending=campaign!=null&&campaign.IsSpawned&&campaign.State.phase==CampaignPhase.Completed;
            if(shotCamera==null)return;
            shotCamera.enabled=ending;
            if(!ending||startPose==null||endPose==null)return;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((float)(campaign.Now-campaign.PhaseStarted)/campaign.settings.endingSeconds));
            shotCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPose.position,endPose.position,t),Quaternion.Slerp(startPose.rotation,endPose.rotation,t));
        }
    }
}
