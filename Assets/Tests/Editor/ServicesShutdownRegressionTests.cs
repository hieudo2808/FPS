using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class ServicesShutdownRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void CancelledShutdownCompletesItsTaskWithoutThrowingFromTheScheduler()
        {
            using var fixture = new SessionFixture();
            using var cancellation = new CancellationTokenSource();
            Task stop = fixture.Stop(cancellation.Token);
            Assert.That(stop.IsCompleted, Is.False);
            cancellation.Cancel();
            Assert.DoesNotThrow(fixture.Scheduler.RunLast);
            Assert.That(stop.IsCanceled, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CompletedShutdownRemovesTheCallbackBeforeExitTokenCancellation()
        {
            using var fixture = new SessionFixture();
            using var cancellation = new CancellationTokenSource();
            Task stop = fixture.Stop(cancellation.Token);
            fixture.Invoke("OnManagerStopped", true);
            fixture.Scheduler.RunLast(); // NGO stopped; finish after the frame.
            Assert.That(stop.IsCompletedSuccessfully, Is.True);
            Assert.That(fixture.Scheduler.Pending, Is.Empty);
            cancellation.Cancel();
            Assert.That(fixture.Scheduler.Pending, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DisposingStoppedSessionRemovesStartAndStopCallbacks()
        {
            using var fixture = new SessionFixture();
            fixture.Set("m_StartNetworkManagerActionId", (long?)fixture.Scheduler.Add(() => Assert.Fail("Stale start callback ran.")));
            fixture.Set("m_StopNetworkManagerActionId", (long?)fixture.Scheduler.Add(() => Assert.Fail("Stale stop callback ran.")));
            fixture.Invoke("Dispose");
            Assert.That(fixture.Scheduler.Pending, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        // Reflection is confined to these tests because NetworkManagerSession
        // and its constructor's scheduler interface are SDK implementation details.
        private sealed class SessionFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly Type sessionType;
            private readonly object session;
            public readonly SchedulerStub Scheduler;

            public SessionFixture()
            {
                root = new GameObject("ServicesShutdownRegression");
                root.SetActive(false);
                var manager = root.AddComponent<NetworkManager>();
                sessionType = typeof(ISession).Assembly.GetType("Unity.Services.Multiplayer.NetworkManagerSession", true);
                var constructor = sessionType.GetConstructors()[0];
                var parameters = constructor.GetParameters();
                var createProxy = typeof(DispatchProxy).GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
                object scheduler = createProxy.MakeGenericMethod(parameters[0].ParameterType, typeof(SchedulerStub)).Invoke(null, null);
                Scheduler = (SchedulerStub)scheduler;
                session = constructor.Invoke(new[] { scheduler, manager, Enum.Parse(parameters[2].ParameterType, "Host") });
                Set("m_StartNetworkManagerActionId", (long?)1L);
            }

            public Task Stop(CancellationToken cancellation) => (Task)sessionType.GetMethod("StopAsync")
                .Invoke(session, new object[] { cancellation });
            public object Invoke(string method, params object[] args) => sessionType
                .GetMethod(method, BindingFlags.Public | PrivateInstance).Invoke(session, args);
            public void Set(string field, object value) => sessionType.GetField(field, PrivateInstance).SetValue(session, value);

            public void Dispose()
            {
                try { Invoke("Dispose"); }
                finally { Object.DestroyImmediate(root); }
            }
        }

        public class SchedulerStub : DispatchProxy
        {
            public readonly Dictionary<long, Action> Pending = new Dictionary<long, Action>();
            private long nextId = 100;
            private long lastId;

            public long Add(Action action)
            {
                lastId = ++nextId;
                Pending.Add(lastId, action);
                return lastId;
            }

            public void RunLast()
            {
                Action action = Pending[lastId];
                Pending.Remove(lastId);
                action();
            }

            protected override object Invoke(MethodInfo method, object[] args)
            {
                switch (method.Name)
                {
                    case "ScheduleAction": return Add((Action)args[0]);
                    case "CancelAction": Pending.Remove((long)args[0]); return null;
                    default: throw new NotSupportedException(method.Name);
                }
            }
        }
    }
}
