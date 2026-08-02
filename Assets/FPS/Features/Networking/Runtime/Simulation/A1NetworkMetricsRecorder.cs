using System;
using System.IO;
using System.Text;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;

namespace FPS.NetworkSimulation
{
    /// <summary>
    /// Development harness recorder. It aggregates allocation-free frame samples into exact
    /// ten-second windows and logs current/median/p95 bandwidth plus network main-thread cost.
    /// </summary>
    public sealed class A1NetworkMetricsRecorder : MonoBehaviour
    {
        private const float WindowSeconds = 10f;
        private const int MaxFrameSamples = 1200;
        private const int MaxWindowSamples = 360;

        [Serializable]
        private sealed class MetricWindow
        {
            public float windowSeconds;
            public float uplinkKBps;
            public float uplinkP50;
            public float uplinkP95;
            public float downlinkKBps;
            public float downlinkP50;
            public float downlinkP95;
            public float networkMainThreadP50Ms;
            public float networkMainThreadP95Ms;
            public int rttMs;
            public int simulatedJitterMs;
            public int simulatedLossPercent;
            public long gcAllocationBytes;
            public int activeNetworkObjects;
            public int maxActiveNetworkObjects;
            public long spawnedNetworkObjectEvents;
            public float enemyReplicationEventsPerSecond;
        }

        private readonly double[] frameCostMs = new double[MaxFrameSamples];
        private readonly double[] frameSortBuffer = new double[MaxFrameSamples];
        private readonly double[] uplinkWindows = new double[MaxWindowSamples];
        private readonly double[] downlinkWindows = new double[MaxWindowSamples];
        private readonly double[] windowSortBuffer = new double[MaxWindowSamples];

        private ProfilerRecorder bytesSent;
        private ProfilerRecorder bytesReceived;
        private ProfilerRecorder transportPoll;
        private ProfilerRecorder incomingData;
        private ProfilerRecorder sendBatch;
        private ProfilerRecorder receiveBatch;
        private ProfilerRecorder networkBehaviourUpdate;
        private ProfilerRecorder gcAllocatedInFrame;
        private float windowStartedAt;
        private long windowBytesSent;
        private long windowBytesReceived;
        private long windowGcAllocationBytes;
        private long windowNetworkObjectEvents;
        private long windowEnemyReplicationEvents;
        private int previousActiveNetworkObjects = -1;
        private int previousActiveEnemyObjects = -1;
        private int maxActiveNetworkObjects;
        private int frameCount;
        private int windowCount;
        private StreamWriter artifactWriter;

        private void OnEnable()
        {
            bytesSent = ProfilerRecorder.StartNew(ProfilerCategory.Network, "Total Bytes Sent", 1);
            bytesReceived = ProfilerRecorder.StartNew(ProfilerCategory.Network, "Total Bytes Received", 1);
            transportPoll = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "NetworkManager.TransportPoll", 1);
            incomingData = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "NetworkManager.HandleIncomingData", 1);
            sendBatch = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "NetworkMessageManager.SendBatch", 1);
            receiveBatch = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "NetworkMessageManager.ReceiveBatchBatch", 1);
            networkBehaviourUpdate = ProfilerRecorder.StartNew(
                ProfilerCategory.Scripts, "NetworkBehaviour.NetworkBehaviourUpdate", 1);
            gcAllocatedInFrame = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            windowStartedAt = Time.realtimeSinceStartup;

            string artifactDirectory = GetArgument("-a1ArtifactDir");
            string peer = GetArgument("-a1PeerId") ?? GetArgument("-a1Role") ?? "unknown";
            if (!string.IsNullOrWhiteSpace(artifactDirectory))
            {
                try
                {
                    Directory.CreateDirectory(artifactDirectory);
                    artifactWriter = new StreamWriter(
                        Path.Combine(artifactDirectory, $"{peer}.metrics.jsonl"), append: false,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
                }
                catch (IOException exception)
                {
                    Debug.LogError($"[A1Metrics] artifact_open_failed:{exception.Message}");
                }
            }
        }

        private void Update()
        {
            windowBytesSent += Math.Max(0L, bytesSent.LastValue);
            windowBytesReceived += Math.Max(0L, bytesReceived.LastValue);

            double costMs = (Math.Max(0L, transportPoll.LastValue)
                    + Math.Max(0L, incomingData.LastValue)
                    + Math.Max(0L, sendBatch.LastValue)
                    + Math.Max(0L, receiveBatch.LastValue)
                    + Math.Max(0L, networkBehaviourUpdate.LastValue))
                / 1_000_000.0;
            windowGcAllocationBytes += Math.Max(0L, gcAllocatedInFrame.LastValue);

            NetworkManager manager = NetworkManager.Singleton;
            int activeNetworkObjects = GetActiveNetworkObjectCount(manager);
            int activeEnemyObjects = GetActiveEnemyNetworkObjectCount(manager);
            if (previousActiveNetworkObjects >= 0)
                windowNetworkObjectEvents += Math.Abs(activeNetworkObjects - previousActiveNetworkObjects);
            if (previousActiveEnemyObjects >= 0)
                windowEnemyReplicationEvents += Math.Abs(activeEnemyObjects - previousActiveEnemyObjects);
            previousActiveNetworkObjects = activeNetworkObjects;
            previousActiveEnemyObjects = activeEnemyObjects;
            maxActiveNetworkObjects = Math.Max(maxActiveNetworkObjects, activeNetworkObjects);

            if (frameCount < frameCostMs.Length)
                frameCostMs[frameCount++] = costMs;

            float elapsed = Time.realtimeSinceStartup - windowStartedAt;
            if (elapsed >= WindowSeconds)
                SealWindow(elapsed);
        }

        private void SealWindow(float elapsed)
        {
            double uplinkKbPerSecond = windowBytesSent / Math.Max(0.001, elapsed) / 1024.0;
            double downlinkKbPerSecond = windowBytesReceived / Math.Max(0.001, elapsed) / 1024.0;
            int index = windowCount % MaxWindowSamples;
            uplinkWindows[index] = uplinkKbPerSecond;
            downlinkWindows[index] = downlinkKbPerSecond;
            windowCount++;

            double frameP50 = Percentile(frameCostMs, frameCount, 0.50, frameSortBuffer);
            double frameP95 = Percentile(frameCostMs, frameCount, 0.95, frameSortBuffer);
            int validWindows = Math.Min(windowCount, MaxWindowSamples);
            double uplinkP50 = Percentile(uplinkWindows, validWindows, 0.50, windowSortBuffer);
            double uplinkP95 = Percentile(uplinkWindows, validWindows, 0.95, windowSortBuffer);
            double downlinkP50 = Percentile(downlinkWindows, validWindows, 0.50, windowSortBuffer);
            double downlinkP95 = Percentile(downlinkWindows, validWindows, 0.95, windowSortBuffer);
            double enemyReplicationEventsPerSecond = windowEnemyReplicationEvents / Math.Max(0.001, elapsed);

            NetworkManager manager = NetworkManager.Singleton;
            int activeNetworkObjects = GetActiveNetworkObjectCount(manager);
            ulong remoteId = manager != null && manager.IsServer
                ? FirstRemoteClientId(manager)
                : NetworkManager.ServerClientId;
            ulong rttMs = manager != null && manager.IsListening && manager.NetworkConfig.NetworkTransport != null
                ? manager.NetworkConfig.NetworkTransport.GetCurrentRtt(remoteId)
                : 0;
            NetworkSimulator simulator = GetComponent<NetworkSimulator>();
            INetworkSimulatorPreset preset = simulator != null ? simulator.ConnectionPreset : null;

            var metricWindow = new MetricWindow
            {
                windowSeconds = elapsed,
                uplinkKBps = (float)uplinkKbPerSecond,
                uplinkP50 = (float)uplinkP50,
                uplinkP95 = (float)uplinkP95,
                downlinkKBps = (float)downlinkKbPerSecond,
                downlinkP50 = (float)downlinkP50,
                downlinkP95 = (float)downlinkP95,
                networkMainThreadP50Ms = (float)frameP50,
                networkMainThreadP95Ms = (float)frameP95,
                rttMs = (int)rttMs,
                simulatedJitterMs = preset?.PacketJitterMs ?? 0,
                simulatedLossPercent = preset?.PacketLossPercent ?? 0,
                gcAllocationBytes = windowGcAllocationBytes,
                activeNetworkObjects = activeNetworkObjects,
                maxActiveNetworkObjects = maxActiveNetworkObjects,
                spawnedNetworkObjectEvents = Math.Max(0L, windowNetworkObjectEvents),
                enemyReplicationEventsPerSecond = (float)enemyReplicationEventsPerSecond
            };
            string metricsJson = "[A1Metrics] " + JsonUtility.ToJson(metricWindow);
            Debug.Log(metricsJson);
            if (artifactWriter != null)
                artifactWriter.WriteLine(metricsJson.Substring(metricsJson.IndexOf('{')));

            windowStartedAt = Time.realtimeSinceStartup;
            windowBytesSent = 0;
            windowBytesReceived = 0;
            windowGcAllocationBytes = 0;
            windowNetworkObjectEvents = 0;
            windowEnemyReplicationEvents = 0;
            frameCount = 0;
        }

        private static int GetActiveNetworkObjectCount(NetworkManager manager)
        {
            return manager?.SpawnManager?.SpawnedObjectsList?.Count ?? 0;
        }

        private static int GetActiveEnemyNetworkObjectCount(NetworkManager manager)
        {
            if (manager?.SpawnManager?.SpawnedObjectsList == null)
                return 0;

            int count = 0;
            foreach (NetworkObject networkObject in manager.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject != null && networkObject.GetComponent<EnemyAI>() != null)
                    count++;
            }

            return count;
        }

        private static ulong FirstRemoteClientId(NetworkManager manager)
        {
            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (clientId != manager.LocalClientId)
                    return clientId;
            }
            return manager.LocalClientId;
        }

        private static double Percentile(double[] values, int count, double percentile, double[] buffer)
        {
            if (count <= 0)
                return 0.0;

            Array.Copy(values, buffer, count);
            Array.Sort(buffer, 0, count);
            int index = Mathf.Clamp(Mathf.CeilToInt((float)(percentile * count)) - 1, 0, count - 1);
            return buffer[index];
        }

        private void OnDisable()
        {
            bytesSent.Dispose();
            bytesReceived.Dispose();
            transportPoll.Dispose();
            incomingData.Dispose();
            sendBatch.Dispose();
            receiveBatch.Dispose();
            networkBehaviourUpdate.Dispose();
            gcAllocatedInFrame.Dispose();
            artifactWriter?.Dispose();
            artifactWriter = null;
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return null;
        }
    }
}
