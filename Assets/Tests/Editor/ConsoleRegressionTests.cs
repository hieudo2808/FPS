using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using SdkSessionState = Unity.Services.Multiplayer.SessionState;

namespace FPS.Tests
{
    public class ConsoleRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [TestCase("Brimstone")]
        [TestCase("Clove")]
        [TestCase("Gekko")]
        [TestCase("Sage")]
        public void HiddenThirdPersonPresentationAcceptsNetworkUpdatesWithoutWarnings(string character)
        {
            string path = $"Assets/FPS/Features/Characters/Content/Players/{character}/{character}Player.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visibility = root.GetComponent<PlayerVisibilityController>();
                var weapons = root.GetComponent<WeaponManager>();
                string visibilityLog = $"[PlayerVisual] root={root.name} firstPersonArms={visibility.FirstPersonArms.name} slots=True";
                LogAssert.Expect(LogType.Log, visibilityLog);
                visibility.SetupVisibility(true);
                Assert.That(weapons.CharacterAnimation.isActiveAndEnabled, Is.False);
                visibility.SetThirdPersonAiming(true);
                visibility.SetThirdPersonWeaponAnimationFloat("ReloadPlaybackSpeed", 1.5f);
                visibility.SetThirdPersonWeaponAnimationFloat("ReloadNormalizedTime", 0.5f);
                visibility.TriggerThirdPersonWeaponAnimation("Reload");
                visibility.RefreshWeaponPresentation(0);

                visibility.SetupVisibility(false);
                Animator body = weapons.CharacterAnimation;
                Assert.That(body.isActiveAndEnabled, Is.True);
                body.Rebind();
                body.Update(0f);
                visibility.SetThirdPersonAiming(true);
                Assert.That(body.GetBool("Aiming"), Is.True, "Visible bodies must still receive aim state.");
                visibility.SetThirdPersonWeaponAnimationFloat("ReloadPlaybackSpeed", 1.5f);
                visibility.TriggerThirdPersonWeaponAnimation("Reload");

                LogAssert.Expect(LogType.Log, visibilityLog);
                visibility.SetupVisibility(true);
                visibility.SetThirdPersonAiming(false);
                visibility.SetThirdPersonWeaponAnimationFloat("ReloadNormalizedTime", 0.9f);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase(LobbyExceptionReason.LobbyNotFound)]
        [TestCase(LobbyExceptionReason.PlayerNotFound)]
        public void LeavingAnAlreadyRemovedLobbyIsSuccessful(LobbyExceptionReason reason)
        {
            ISession session = NewSession(out SessionStub stub);
            stub.LeaveResult = Task.FromException(new LobbyServiceException(reason, "Already removed"));
            Task leave = LeaveSession(session);
            Assert.That(leave.IsCompletedSuccessfully, Is.True);
            Assert.That(stub.LeaveCalls, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnexpectedLobbyFailuresRemainVisible()
        {
            ISession session = NewSession(out SessionStub stub);
            stub.LeaveResult = Task.FromException(new LobbyServiceException(LobbyExceptionReason.Unauthorized, "Unauthorized"));
            LogAssert.Expect(LogType.Warning, "[Session] Unexpected leave failure: Unauthorized");
            Assert.That(LeaveSession(session).IsCompletedSuccessfully, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(SdkSessionState.Disconnected)]
        [TestCase(SdkSessionState.Deleted)]
        public void TerminalSessionsAreNotLeftAgain(SdkSessionState state)
        {
            ISession session = NewSession(out SessionStub stub);
            stub.State = state;
            Assert.That(LeaveSession(session).IsCompletedSuccessfully, Is.True);
            Assert.That(stub.LeaveCalls, Is.Zero);
        }

        [Test]
        public void ConcurrentLeavesShareOneRequestAndDoNotClearANewSession()
        {
            var root = new GameObject("SessionLeaveRegression");
            root.SetActive(false);
            var completion = new TaskCompletionSource<bool>();
            try
            {
                var manager = root.AddComponent<NetworkGameManager>();
                ISession session = NewSession(out SessionStub stub);
                stub.LeaveResult = completion.Task;
                SetSession(manager, session);
                Task first = LeaveCurrentSession(manager);
                Task second = LeaveCurrentSession(manager);
                Assert.That(second, Is.SameAs(first));
                Assert.That(stub.LeaveCalls, Is.EqualTo(1));

                ISession replacement = NewSession(out _);
                SetSession(manager, replacement);
                completion.SetResult(true);
                Assert.That(first.IsCompletedSuccessfully, Is.True);
                Assert.That(typeof(NetworkGameManager).GetField("currentSession", PrivateInstance).GetValue(manager), Is.SameAs(replacement));
            }
            finally
            {
                completion.TrySetResult(true);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplicationQuitDoesNotDuplicateTheSdkLeave()
        {
            var root = new GameObject("SessionQuitRegression");
            root.SetActive(false);
            try
            {
                var manager = root.AddComponent<NetworkGameManager>();
                ISession session = NewSession(out SessionStub stub);
                SetSession(manager, session);
                typeof(NetworkGameManager).GetMethod("OnApplicationQuit", PrivateInstance).Invoke(manager, null);
                Assert.That(LeaveCurrentSession(manager).IsCompletedSuccessfully, Is.True);
                Assert.That(stub.LeaveCalls, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Task LeaveSession(ISession session) => (Task)typeof(NetworkGameManager)
            .GetMethod("LeaveSessionBestEffortAsync", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { session });

        private static Task LeaveCurrentSession(NetworkGameManager manager) => (Task)typeof(NetworkGameManager)
            .GetMethod("LeaveCurrentSessionBestEffortAsync", PrivateInstance).Invoke(manager, null);

        private static void SetSession(NetworkGameManager manager, ISession session) => typeof(NetworkGameManager)
            .GetField("currentSession", PrivateInstance).SetValue(manager, session);

        private static ISession NewSession(out SessionStub stub)
        {
            ISession session = DispatchProxy.Create<ISession, SessionStub>();
            stub = (SessionStub)session;
            return session;
        }

        // A proxy keeps these lifecycle tests independent of unrelated ISession
        // members and avoids contacting Unity Services from the Editor test run.
        public class SessionStub : DispatchProxy
        {
            public SdkSessionState State = SdkSessionState.Connected;
            public Task LeaveResult = Task.CompletedTask;
            public int LeaveCalls;

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod.Name)
                {
                    case "get_State": return State;
                    case "LeaveAsync":
                        LeaveCalls++;
                        return LeaveResult;
                    default: throw new NotSupportedException(targetMethod.Name);
                }
            }
        }
    }

    // Used by the editor automation to retain a concise result and NUnit XML.
    public sealed class ConsoleRegressionResultRecorder : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string summary = $"{result.ResultState}: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}";
            UnityEditor.SessionState.SetString("FPS.ConsoleRegressionResult", summary);
            TestRunnerApi.SaveResultToFile(result, "Library/ConsoleRegressionResults.xml");
            Debug.Log("Console regression " + summary);
        }
    }
}
