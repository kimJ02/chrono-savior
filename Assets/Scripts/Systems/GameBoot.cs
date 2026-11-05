using UnityEngine;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 게임 시작 담당 — **`00_Persistent`(빌드 첫 씬)에만 놓인다**(2026-11-04 씬 분할). 하는 일은 하나:
    /// **세이브 불러오기** — 다른 모든 컴포넌트의 Awake보다 먼저(<c>DefaultExecutionOrder(-1000)</c>).
    /// 예전엔 `AutoSave.Awake`가 '씬 빌더가 이 오브젝트를 먼저 만든다'는 생성 순서에 기대고 있었다.
    ///
    /// 월드 씬(`10_Lobby`·`20_Combat`)을 불러오고 내리는 건 <see cref="GameFlow"/>의 화면 전환이 맡는다(2026-11-05 씬 분할 2차 3-4) —
    /// 시작하면 타이틀(월드 없음)이고, 로비·사냥으로 갈 때 그 씬을 불러온다. 예전엔 여기서 월드 씬(로비·전투가 한 씬)을 한 번 불러왔다.
    /// PlayMode 테스트는 빈 씬에 `GameFlow`만 만들어 쓰므로 세이브를 읽지 않는다(테스트가 개발자의 세이브를 읽지 않게, `AutoSave` 주석 참고).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class GameBoot : MonoBehaviour
    {
        void Awake()
        {
            if (SaveService.Load()) Debug.Log($"[GameBoot] 세이브 불러옴 — {SaveService.SavePath}");
        }
    }
}
