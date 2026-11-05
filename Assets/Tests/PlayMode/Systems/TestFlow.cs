using UnityEngine;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{
    /// <summary>
    /// 빈 테스트 씬에 게임 흐름을 세운다 — 실제 게임에선 `GameFlow`는 상주 씬(`00_Persistent`), `CombatDirector`는 전투 씬(`20_Combat`)에
    /// 있다(2026-11-05 씬 분할 2차 1단계). 사냥을 시작하려면 둘 다 있어야 해서 같이 만든다. 이름이 "Test"로 시작해 각 테스트의 TearDown이 지운다.
    /// 실제 씬 구성 그대로의 검사는 `GameBootTests`(진짜 씬을 부팅)가 한다.
    /// 씬 로더는 가짜(<see cref="FakeWorldScenes"/>, 즉시 끝남)를 넣는다 — 화면 전환이 그 자리에서 끝나 단언을 바로 할 수 있다(2026-11-05 3-4).
    /// </summary>
    static class TestFlow
    {
        /// <param name="player">사냥 시작·보스 필드 진입 때 되돌릴 플레이어(실제 씬에선 직렬화 참조). 없으면 플레이어 리셋을 건너뛴다.</param>
        public static GameFlow Create(GameObject player = null)
        {
            var director = new GameObject("TestCombatDirector").AddComponent<CombatDirector>();
            director.Player = player;
            var flow = new GameObject("TestGameFlow").AddComponent<GameFlow>();
            flow.Scenes = new FakeWorldScenes();
            return flow;
        }
    }
}
