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

        public WorldSceneOp Load(string sceneName)
        {
            if (FailNext) { FailNext = false; return WorldSceneOp.Failed("테스트가 실패시킴: " + sceneName); }
            Loaded.Add(sceneName);
            return WorldSceneOp.Done(new WorldHandle(nextId++, sceneName));
        }

        public WorldSceneOp Unload(WorldHandle world) { Unloaded.Add(world); return WorldSceneOp.Done(world); }
        public void Activate(WorldHandle world) => Active = world;
        public void SetRunning(WorldHandle world, bool running) => Running.Add((world, running));
    }
}
