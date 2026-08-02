using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FPS
{
    public static class NetworkDiagnostics
    {
        public static event Action<string, SessionState, string, SessionPlayerId> EventEmitted;

        [Serializable]
        private struct EventRecord
        {
            public string eventName;
            public string sessionState;
            public string reason;
            public string player;
            public double time;
        }

        private static readonly object Sync = new();
        private static StreamWriter writer;
        private static string sessionSalt;

        public static void BeginSession()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (Sync)
            {
                EndSessionLocked();
                byte[] salt = new byte[16];
                using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                    random.GetBytes(salt);
                sessionSalt = Convert.ToBase64String(salt);

                try
                {
                    string directory = Path.Combine(Application.persistentDataPath, "NetworkDiagnostics");
                    Directory.CreateDirectory(directory);
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                    string runId = GetArgument("-a1RunId") ?? "runtime";
                    string peerId = GetArgument("-a1PeerId") ?? "peer";
                    string safeRunId = SanitizeFilePart(runId);
                    string safePeerId = SanitizeFilePart(peerId);
                    writer = new StreamWriter(Path.Combine(directory, $"network-{safeRunId}-{safePeerId}-{timestamp}.jsonl"), append: false,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                    {
                        AutoFlush = true
                    };
                }
                catch (Exception exception)
                {
                    writer = null;
                    Debug.LogWarning($"[NetworkDiagnostics] JSONL unavailable: {exception.Message}");
                }
            }
#endif
        }

        public static void EndSession()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (Sync)
                EndSessionLocked();
#endif
        }

        public static void Emit(string eventName, SessionState state, string reason = "", SessionPlayerId playerId = default)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EventEmitted?.Invoke(eventName ?? string.Empty, state, reason ?? string.Empty, playerId);
            var record = new EventRecord
            {
                eventName = eventName ?? string.Empty,
                sessionState = state.ToString(),
                reason = reason ?? string.Empty,
                player = Pseudonymize(playerId),
                time = Time.realtimeSinceStartupAsDouble
            };
            string json = JsonUtility.ToJson(record);
            GameLog.Info(() => $"[NetworkEvent] {json}");
            lock (Sync)
                writer?.WriteLine(json);
#endif
        }

        private static string Pseudonymize(SessionPlayerId playerId)
        {
            if (!playerId.IsValid)
                return string.Empty;

            string input = (sessionSalt ?? string.Empty) + ":" + playerId.Value.ToString(CultureInfo.InvariantCulture);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var builder = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static string SanitizeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }

        private static void EndSessionLocked()
        {
            writer?.Dispose();
            writer = null;
            sessionSalt = null;
        }
    }
}
