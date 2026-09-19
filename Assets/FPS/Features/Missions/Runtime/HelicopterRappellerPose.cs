using UnityEngine;
namespace FPS
{
    /// <summary>A hand-authored fast-rope pose for the project's shared Generic bone names.</summary>
    public sealed class HelicopterRappellerPose : MonoBehaviour
    {
        public Transform pelvis, leftShoulder, leftElbow, leftHand, rightShoulder, rightElbow, rightHand;
        public Transform leftHip, leftKnee, leftFoot, rightHip, rightKnee, rightFoot;
        public GameObject readyWeapon;
        public AnimationClip armedIdle, armedWalk;
        public bool IsArmed => readyWeapon != null && readyWeapon.activeInHierarchy;
        [SerializeField] private Transform[] joints;
        [SerializeField] private Quaternion[] restRotations;
        [SerializeField] private Vector3 pelvisRest;
        [SerializeField] private Quaternion leftFootRest, rightFootRest;
        [SerializeField] private Transform[] poseTransforms;
        [SerializeField] private Vector3[] posePositions;
        [SerializeField] private Quaternion[] poseRotations;
        public void CaptureRest()
        {
            poseTransforms=GetComponentsInChildren<Transform>(true);
            posePositions=new Vector3[poseTransforms.Length];
            poseRotations=new Quaternion[poseTransforms.Length];
            for(int i=0;i<poseTransforms.Length;i++)
            {
                posePositions[i]=poseTransforms[i].localPosition;
                poseRotations[i]=poseTransforms[i].localRotation;
            }
            joints = new[]{pelvis,leftShoulder,leftElbow,leftHand,rightShoulder,rightElbow,rightHand,leftHip,leftKnee,leftFoot,rightHip,rightKnee,rightFoot};
            restRotations=new Quaternion[joints.Length];
            for(int i=0;i<joints.Length;i++)if(joints[i]!=null)restRotations[i]=joints[i].localRotation;
            if(pelvis!=null)pelvisRest=pelvis.localPosition;
            if(leftFoot!=null)leftFootRest=Quaternion.Inverse(transform.rotation)*leftFoot.rotation;
            if(rightFoot!=null)rightFootRest=Quaternion.Inverse(transform.rotation)*rightFoot.rotation;
        }
        public void Pose(float hanging, float time, bool walking)
        {
            if(joints==null||joints.Length==0)CaptureRest();
            // Full reset makes seeking back from an armed pose deterministic.
            if(poseTransforms!=null)
                for(int i=0;i<poseTransforms.Length;i++)
                    if(poseTransforms[i]!=null && poseTransforms[i]!=transform)
                    {
                        poseTransforms[i].localPosition=posePositions[i];
                        poseTransforms[i].localRotation=poseRotations[i];
                    }
            bool grounded=time>=4.25f;
            if(readyWeapon!=null)readyWeapon.SetActive(grounded);
            if(grounded && armedIdle!=null)
            {
                var clip=walking && armedWalk!=null ? armedWalk : armedIdle;
                clip.SampleAnimation(gameObject, Mathf.Repeat(time-4.25f, Mathf.Max(.01f,clip.length)));
                return;
            }
            for(int i=0;i<joints.Length;i++)if(joints[i]!=null)joints[i].localRotation=restRotations[i];
            if(pelvis==null)return;
            pelvis.localPosition=pelvisRest;
            pelvis.position-=Vector3.up*(.08f*(1-hanging));
            float height=Vector3.Distance(pelvis.position,transform.position);
            float stride=walking?Mathf.Sin(time*9f)*.18f:0;
            // Targets are relative to the pelvis in metres; solve each limb in world space
            // to avoid assuming the bone's local twist axis is the same across characters.
            Limb(leftShoulder,leftElbow,leftHand,Vector3.Lerp(new Vector3(-.4f,.05f,.02f),new Vector3(-.5f,.55f,.05f),hanging),
                Vector3.Lerp(new Vector3(-.3f,-.14f,.08f),new Vector3(-.018f,.92f,.26f),hanging));
            Limb(rightShoulder,rightElbow,rightHand,Vector3.Lerp(new Vector3(.4f,.05f,.02f),new Vector3(.5f,.3f,.02f),hanging),
                Vector3.Lerp(new Vector3(.3f,-.14f,.08f),new Vector3(.018f,.55f,.26f),hanging));
            Limb(leftHip,leftKnee,leftFoot,new Vector3(-.19f,-.38f,.16f*hanging+stride),
                new Vector3(-.21f,-height+.12f+.08f*hanging,.07f+stride));
            Limb(rightHip,rightKnee,rightFoot,new Vector3(.19f,-.38f,.12f*hanging-stride),
                new Vector3(.21f,-height+.12f+.16f*hanging,-.06f-stride));
            if(leftFoot!=null)leftFoot.rotation=transform.rotation*leftFootRest;
            if(rightFoot!=null)rightFoot.rotation=transform.rotation*rightFootRest;
        }
        private void Limb(Transform upper,Transform middle,Transform end,Vector3 bend,Vector3 target)
        {
            if(upper==null||middle==null||end==null)return;
            Vector3 origin=pelvis.position;
            Vector3 goal=origin+transform.rotation*target;
            Vector3 pole=origin+transform.rotation*bend;
            float a=Vector3.Distance(upper.position,middle.position), b=Vector3.Distance(middle.position,end.position);
            Vector3 delta=goal-upper.position;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 axis=delta.normalized;
            Vector3 side=Vector3.ProjectOnPlane(pole-upper.position,axis).normalized;
            float along=(a*a+distance*distance-b*b)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Aim(upper,middle,upper.position+axis*along+side*height);
            Aim(middle,end,goal);
        }
        private static void Aim(Transform joint,Transform child,Vector3 target)
        {
            Vector3 from=child.position-joint.position,to=target-joint.position;
            if(from.sqrMagnitude>.000001f&&to.sqrMagnitude>.000001f)
                joint.rotation=Quaternion.FromToRotation(from,to)*joint.rotation;
        }
    }
}
