using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.Tests
{
    public class EnemyAttackAnimationEventTests
    {
        private const string AttackStateName = "Attack";
        private const string AttackImpactEvent = "ApplyAttackHit";

        private static readonly string[] RuntimeEnemyAttackControllers =
        {
            "Assets/Prefabs/Enemies/ZombieAnimation/ZombieAnim.controller",
            "Assets/Art/Animations/Screamer/Screamer.controller"
        };

        [Test]
        public void RuntimeEnemyAttackClips_InvokeDamageThroughAnimationEvent()
        {
            foreach (string controllerPath in RuntimeEnemyAttackControllers)
            {
                AnimationClip attackClip = LoadAttackClip(controllerPath);
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(attackClip);

                AnimationEvent impactEvent = events.FirstOrDefault(evt => evt.functionName == AttackImpactEvent);
                Assert.IsNotNull(impactEvent,
                    $"{controllerPath} Attack state should call {AttackImpactEvent} from the attack clip impact frame.");

                Assert.Greater(impactEvent.time, attackClip.length * 0.15f,
                    $"{controllerPath} impact event should not fire at attack startup/hand raise.");
                Assert.Less(impactEvent.time, attackClip.length * 0.95f,
                    $"{controllerPath} impact event should fire before the clip is effectively over.");
            }
        }

        private static AnimationClip LoadAttackClip(string controllerPath)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            Assert.IsNotNull(controller, $"Missing animator controller at {controllerPath}.");

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                ChildAnimatorState state = layer.stateMachine.states
                    .FirstOrDefault(childState => childState.state.name == AttackStateName);

                if (state.state != null)
                {
                    var clip = state.state.motion as AnimationClip;
                    Assert.IsNotNull(clip, $"{controllerPath} Attack state should use an AnimationClip motion.");
                    return clip;
                }
            }

            Assert.Fail($"{controllerPath} should contain an Attack animator state.");
            return null;
        }
    }
}
