using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Reflection;
using FPS.BT;

namespace FPS.Tests
{
    public class ScreamerTests
    {
        private GameObject gameObject;
        private SI_Screamer screamer;

        [SetUp]
        public void SetUp()
        {
            // Khởi tạo GameObject và Component Screamer
            gameObject = new GameObject("Screamer");
            screamer = gameObject.AddComponent<SI_Screamer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void TestScreamer_ScreamRoutine_YieldsCorrectly()
        {
            // Gỉa lập gọi UseAbility() để chạy ScreamRoutine
            screamer.UseAbility();

            // Lấy coroutine ScreamRoutine thông qua reflection để chạy từng bước (manual tick)
            var method = typeof(SI_Screamer).GetMethod("ScreamRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, "Không tìm thấy hàm ScreamRoutine trong SI_Screamer");
            
            var coroutine = (IEnumerator)method.Invoke(screamer, null);
            
            // Chạy bước đầu tiên của coroutine
            bool hasNext = coroutine.MoveNext();

            // KỲ VỌNG: Coroutine phải dừng ở yield return (trả về true) và trạng thái IsScreaming = true
            // Với code lỗi hiện tại: coroutine sẽ chạy hết tuột và trả về false do các lệnh if lồng rỗng
            Assert.True(screamer.IsScreaming, "Screamer phải chuyển trạng thái IsScreaming = true");
            Assert.True(hasNext, "ScreamRoutine phải yield chờ screamDuration chứ không được chạy hết ngay lập tức");
        }

        [Test]
        public void TestScreamer_DoesNotHaveEmptyUpdateOverride()
        {
            // Kiểm tra xem lớp SI_Screamer có khai báo đè hàm Update() trống rỗng hay không
            var method = typeof(SI_Screamer).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            
            // KỲ VỌNG: Lớp SI_Screamer không nên tự định nghĩa hàm Update() rỗng để tránh che khuất hàm của lớp cha
            Assert.Null(method, "SI_Screamer không được chứa hàm Update() trống rỗng làm che khuất logic lớp cha");
        }

        [Test]
        public void TestConditions_PlayerDetection_UpdatesWhenPlayerSpawnsLate()
        {
            // Thiết lập node điều kiện tầm xa
            var node = new IsPlayerInRange();
            
            // Set range = 30f via reflection
            var rangeField = typeof(IsPlayerInRange).GetField("range", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(rangeField, "Không tìm thấy field range trong IsPlayerInRange");
            rangeField.SetValue(node, 30f);
            
            // Gán gameObject giả lập vào node của UniBT
            var enemyGo = new GameObject("ScreamerEnemy");
            node.Run(enemyGo);

            // Chạy OnAwake khi CHƯA có player nào trong scene
            var awakeMethod = typeof(IsPlayerInRange).GetMethod("OnAwake", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(awakeMethod, "Không tìm thấy hàm OnAwake trong IsPlayerInRange");
            awakeMethod.Invoke(node, null);

            // Chạy IsUpdatable() -> KỲ VỌNG: Trả về false vì không có player
            var isUpdatableMethod = typeof(IsPlayerInRange).GetMethod("IsUpdatable", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(isUpdatableMethod, "Không tìm thấy hàm IsUpdatable trong IsPlayerInRange");
            bool initialResult = (bool)isUpdatableMethod.Invoke(node, null);
            Assert.False(initialResult, "IsPlayerInRange phải trả về false khi không có player");

            // Tạo player trong tầm (cách 10m)
            var playerGo = new GameObject("Player1");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0, 0, 10);
            playerGo.AddComponent<PlayerHealth>(); // Cần để PlayerProfiler nhận diện là không tử vong

            // Chạy lại IsUpdatable() -> KỲ VỌNG: Phải cập nhật động và trả về true (do player nằm trong tầm 30m)
            // Với code cũ: Nó sẽ trả về false do cache player = null từ OnAwake
            bool finalResult = (bool)isUpdatableMethod.Invoke(node, null);
            
            // Cleanup
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);

            Assert.True(finalResult, "IsPlayerInRange phải tự động cập nhật khi player xuất hiện muộn");
        }

        [Test]
        public void TestActions_MultiplayerTargeting_TargetsNearestPlayer()
        {
            // Thiết lập PlayerProfiler
            var profilerGo = new GameObject("PlayerProfiler");
            var profiler = profilerGo.AddComponent<PlayerProfiler>();
            
            // Gọi Awake để thiết lập Instance singleton
            var awakeMethod = typeof(PlayerProfiler).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(awakeMethod, "Không tìm thấy Awake trong PlayerProfiler");
            awakeMethod.Invoke(profiler, null);

            // Tạo 2 players: Player1 cách 20m, Player2 cách 5m
            var enemyGo = new GameObject("ScreamerEnemy");
            enemyGo.transform.position = Vector3.zero;
            enemyGo.AddComponent<NavMeshAgent>();

            var player1 = new GameObject("Player1");
            player1.tag = "Player";
            player1.transform.position = new Vector3(0, 0, 20);
            player1.AddComponent<PlayerHealth>();

            var player2 = new GameObject("Player2");
            player2.tag = "Player";
            player2.transform.position = new Vector3(0, 0, 5);
            player2.AddComponent<PlayerHealth>();

            // Gọi RefreshPlayers qua reflection để PlayerProfiler nhận diện cả 2 players
            var refreshMethod = typeof(PlayerProfiler).GetMethod("RefreshPlayers", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(refreshMethod, "Không tìm thấy hàm RefreshPlayers trong PlayerProfiler");
            refreshMethod.Invoke(profiler, null);

            // In danh sách player trong profiler để debug
            Debug.Log($"--- DEBUG PLAYERS IN PROFILER (Count={profiler.AllProfiles.Count}) ---");
            foreach (var p in profiler.AllProfiles)
            {
                bool isDead = p.cachedHealth != null && p.cachedHealth.IsDead;
                bool isTransformNull = p.playerTransform == null;
                bool activeInHierarchy = p.playerTransform != null && p.playerTransform.gameObject.activeInHierarchy;
                Debug.Log($"Player: {p.playerTransform?.gameObject.name}, Pos: {p.playerTransform?.position}, IsDead: {isDead}, Active: {activeInHierarchy}");
            }

            // Thiết lập Action ChasePlayer
            var action = new ChasePlayer();
            
            // Gán gameObject cho action
            action.Run(enemyGo);

            // Chạy OnUpdate lần đầu
            var updateMethod = typeof(ChasePlayer).GetMethod("OnUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(updateMethod, "Không tìm thấy OnUpdate trong ChasePlayer");
            updateMethod.Invoke(action, null);

            // Lấy trường player được chọn qua reflection
            var playerField = typeof(ChasePlayer).GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(playerField, "Không tìm thấy field player trong ChasePlayer");
            Transform targetPlayer = (Transform)playerField.GetValue(action);

            try
            {
                // KỲ VỌNG: Sẽ nhắm mục tiêu vào Player2 vì gần hơn (5m so với 20m)
                // Với code cũ: GameObject.FindGameObjectWithTag có thể trả về bất kỳ ai, không tối ưu khoảng cách
                Assert.NotNull(targetPlayer, "Phải tìm thấy player mục tiêu");
                Assert.AreEqual("Player2", targetPlayer.gameObject.name, "Screamer phải nhắm vào người chơi gần nhất");
            }
            finally
            {
                // Reset singleton instance to null để tránh ảnh hưởng test sau
                var instanceProp = typeof(PlayerProfiler).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null)
                {
                    instanceProp.SetValue(null, null);
                }

                // Cleanup sau khi Assert để tránh lỗi truy xuất đối tượng đã bị hủy
                Object.DestroyImmediate(profilerGo);
                Object.DestroyImmediate(enemyGo);
                Object.DestroyImmediate(player1);
                Object.DestroyImmediate(player2);
            }
        }
    }
}
