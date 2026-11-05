using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 월드 씬(`10_Lobby`·`20_Combat`)을 겹쳐 불러오고 내리는 일 — `GameFlow`의 씬 전환이 이 계약만 쓴다(2026-11-05 씬 분할 2차 3-3).
    /// 실제 구현은 <see cref="SceneWorldScenes"/>(Unity `SceneManager` 경계만 감쌈), 테스트는 씬 없이 성공·실패를 정하는 가짜를 쓴다 —
    /// 흐름 테스트가 진짜 씬을 불러오지 않게. 직렬화 플래그('씬 로드 끄기')는 실제 씬 설정 실수로 이어질 수 있어 쓰지 않는다.
    /// </summary>
    public interface IWorldScenes
    {
        /// <summary>
        /// 그 씬을 겹쳐 불러오기 시작한다 — 끝나면 <see cref="WorldSceneOp.IsDone"/>, 성공이면 <see cref="WorldSceneOp.World"/>가 새 씬.
        /// **불러온 씬은 곧바로 활성 씬이 된다**(첫 Update보다 먼저) — 새 월드가 첫 프레임에 만드는 것(몹·투사체)이 이전 씬에 생기지 않게.
        /// </summary>
        WorldSceneOp Load(string sceneName);

        /// <summary>그 월드를 내리기 시작한다.</summary>
        WorldSceneOp Unload(WorldHandle world);

        /// <summary>활성 씬으로 — Unity는 새 오브젝트를 활성 씬에 만든다. 전환이 실패해 이전 월드로 되돌릴 때 쓴다(새 월드는 불러오면서 이미 활성).</summary>
        void Activate(WorldHandle world);

        /// <summary>그 월드의 루트를 켜고 끈다 — 전환 중 이전 월드를 멈춰 두고(Update·물리·정적 등록이 빠진다), 실패하면 다시 켠다.</summary>
        void SetRunning(WorldHandle world, bool running);
    }

    /// <summary>
    /// 불러온 월드 씬 하나 — **씬 이름이 아니라 그 씬 자체**를 가리킨다. 재도전은 같은 이름의 씬을 하나 더 불러오므로 이름으로 찾으면 옛 씬과 헷갈린다(Codex 3단계 지적).
    /// </summary>
    public readonly struct WorldHandle
    {
        public readonly int Id;
        public readonly string SceneName;

        public WorldHandle(int id, string sceneName) { Id = id; SceneName = sceneName; }

        /// <summary>아무 월드도 아님(타이틀·부팅 직후).</summary>
        public bool IsNone => Id == 0;

        public static readonly WorldHandle None = default;

        public override string ToString() => IsNone ? "(없음)" : $"{SceneName}#{Id}";
    }

    /// <summary>진행 중인 불러오기·내리기 — 끝났는지, 성공했는지, (불러오기면) 새 월드.</summary>
    public sealed class WorldSceneOp
    {
        public bool IsDone { get; private set; }
        public bool Succeeded { get; private set; }
        public WorldHandle World { get; private set; }
        public string Error { get; private set; }

        public void Complete(WorldHandle world) { World = world; Succeeded = true; IsDone = true; }
        public void Fail(string error) { Error = error; Succeeded = false; IsDone = true; }

        public static WorldSceneOp Done(WorldHandle world) { var op = new WorldSceneOp(); op.Complete(world); return op; }
        public static WorldSceneOp Failed(string error) { var op = new WorldSceneOp(); op.Fail(error); return op; }
    }

    /// <summary>
    /// 실제 구현 — `SceneManager`로 겹쳐 불러오고(Additive) 내린다. 불러온 씬은 `sceneLoaded`로 받은 **그 Scene**을 번호(<see cref="WorldHandle.Id"/>)에 묶어 둔다.
    /// </summary>
    public sealed class SceneWorldScenes : IWorldScenes
    {
        readonly Dictionary<int, Scene> scenes = new Dictionary<int, Scene>();
        /// <summary>멈춰 둔 월드에서 **멈출 때 켜져 있던** 루트 — 다시 켤 때 그것만 켠다(원래 꺼진 채 저장된 루트를 켜지 않게).</summary>
        readonly Dictionary<int, List<GameObject>> paused = new Dictionary<int, List<GameObject>>();
        int nextId = 1;

        public WorldSceneOp Load(string sceneName)
        {
            var op = new WorldSceneOp();
            if (!Application.CanStreamedLevelBeLoaded(sceneName)) return WorldSceneOp.Failed($"빌드 목록에 없는 씬: {sceneName}");

            int id = nextId++;
            void OnLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != sceneName || mode != LoadSceneMode.Additive) return;
                SceneManager.sceneLoaded -= OnLoaded;
                scenes[id] = scene;
                // 바로 활성 씬으로 — sceneLoaded는 이 씬의 첫 Update보다 먼저 온다. 흐름이 다음 프레임에 정하면 그 사이 한 프레임 동안
                // 새 월드가 만든 것(스포너의 첫 웨이브 등)이 이전 활성 씬에 생긴다(3단계 종합 리뷰 때 실제로 잡힘).
                SceneManager.SetActiveScene(scene);
                op.Complete(new WorldHandle(id, sceneName));
            }
            SceneManager.sceneLoaded += OnLoaded;
            AsyncOperation async;
            try { async = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
            catch (System.Exception e) { SceneManager.sceneLoaded -= OnLoaded; return WorldSceneOp.Failed($"{sceneName} 불러오기 예외: {e.Message}"); }
            if (async == null) { SceneManager.sceneLoaded -= OnLoaded; return WorldSceneOp.Failed($"{sceneName} 불러오기를 시작하지 못했다"); }
            return op;
        }

        public WorldSceneOp Unload(WorldHandle world)
        {
            if (!scenes.TryGetValue(world.Id, out var scene) || !scene.isLoaded)
            {
                scenes.Remove(world.Id); paused.Remove(world.Id);
                return WorldSceneOp.Done(world); // 이미 없다
            }
            var op = new WorldSceneOp();
            AsyncOperation async;
            try { async = SceneManager.UnloadSceneAsync(scene); }
            catch (System.Exception e) { return WorldSceneOp.Failed($"{world} 내리기 예외: {e.Message}"); }
            // 시작하지 못하면 기록을 그대로 둔다 — 그 씬은 남아 있으니 핸들로 다시 내리거나 켤 수 있게.
            if (async == null) return WorldSceneOp.Failed($"{world} 내리기를 시작하지 못했다");
            async.completed += _ =>
            {
                scenes.Remove(world.Id); paused.Remove(world.Id); // 실제로 내려간 뒤에 잊는다
                op.Complete(world);
            };
            return op;
        }

        public void Activate(WorldHandle world)
        {
            if (scenes.TryGetValue(world.Id, out var scene) && scene.isLoaded) SceneManager.SetActiveScene(scene);
        }

        public void SetRunning(WorldHandle world, bool running)
        {
            if (!scenes.TryGetValue(world.Id, out var scene) || !scene.isLoaded) return;
            if (!running)
            {
                if (paused.ContainsKey(world.Id)) return; // 이미 멈춰 있다
                var wasOn = new List<GameObject>();
                foreach (var root in scene.GetRootGameObjects())
                    if (root.activeSelf) { wasOn.Add(root); root.SetActive(false); }
                paused[world.Id] = wasOn;
            }
            else if (paused.TryGetValue(world.Id, out var wasOn))
            {
                paused.Remove(world.Id);
                foreach (var root in wasOn) if (root != null) root.SetActive(true);
            }
        }

        /// <summary>그 월드의 실제 씬 — 통합 테스트가 핸들이 가리키는 씬을 확인할 때.</summary>
        public bool TryGetScene(WorldHandle world, out Scene scene) => scenes.TryGetValue(world.Id, out scene);
    }
}
