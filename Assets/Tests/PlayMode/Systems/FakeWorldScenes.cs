using System.Collections.Generic;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{
    /// <summary>
    /// 씬 없이 도는 테스트용 씬 로더 — 불러오기·내리기가 **즉시** 끝나서(`GameFlow` 전환이 그 자리에서 끝난다) 기존 흐름 테스트가 동기로 단언할 수 있다.
    /// <see cref="FailNext"/>로 다음 불러오기를 실패시켜 실패 복구를 본다. 실제 씬 동작은 `WorldScenesTests`·`GameBootTests`가 본다.
    /// </summary>
    sealed class FakeWorldScenes : IWorldScenes
    {
        int nextId = 1;
        public readonly List<string> Loaded = new List<string>();
        public readonly List<WorldHandle> Unloaded = new List<WorldHandle>();
        public readonly List<(WorldHandle world, bool running)> Running = new List<(WorldHandle, bool)>();
        public WorldHandle Active { get; private set; }

        /// <summary>참이면 다음 불러오기 하나를 실패시킨다.</summary>
        public bool FailNext;

        /// <summary>참이면 다음 불러오기 하나를 끝내지 않고 붙잡아 둔다 — 전환 중 잠금을 볼 때. <see cref="Release"/>로 끝낸다.</summary>
        public bool HoldNext;
        WorldSceneOp held;
        WorldHandle heldWorld;

        /// <summary>참이면 다음 불러오기 하나가 예외를 던진다 — 전환이 예외에도 잠금을 풀고 되돌리는지.</summary>
        public bool ThrowNext;

        public WorldSceneOp Load(string sceneName)
        {
            if (ThrowNext) { ThrowNext = false; throw new System.InvalidOperationException("테스트가 던짐: " + sceneName); }
            if (FailNext) { FailNext = false; return WorldSceneOp.Failed("테스트가 실패시킴: " + sceneName); }
            Loaded.Add(sceneName);
            var world = new WorldHandle(nextId++, sceneName);
            if (HoldNext) { HoldNext = false; held = new WorldSceneOp(); heldWorld = world; return held; }
            Active = world; // 실제 로더처럼 불러오면 곧바로 활성
            return WorldSceneOp.Done(world);
        }

        /// <summary>붙잡아 둔 불러오기를 끝낸다(성공).</summary>
        public void Release() { if (held == null) return; Active = heldWorld; held.Complete(heldWorld); held = null; }

        /// <summary>참이면 다음 내리기 하나를 실패시킨다(기록하지 않는다 — 그 월드는 남아 있다).</summary>
        public bool FailUnloadNext;

        public WorldSceneOp Unload(WorldHandle world)
        {
            if (FailUnloadNext) { FailUnloadNext = false; return WorldSceneOp.Failed("테스트가 내리기를 실패시킴: " + world); }
            Unloaded.Add(world);
            return WorldSceneOp.Done(world);
        }
        public void Activate(WorldHandle world) => Active = world;
        public void SetRunning(WorldHandle world, bool running) => Running.Add((world, running));
    }
}
