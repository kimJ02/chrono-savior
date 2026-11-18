using UnityEditor;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 지역 보스전 **바로 입장 테스트용** 에디터 메뉴(`ChronoSavior/보스전 테스트/…`) — Play 중에만 동작한다.
    /// 100마리 토벌을 매번 다시 하지 않고도 보스 패턴만 빠르게 반복 확인할 수 있게 한다
    /// (2026-11-18, 2지역 보스 "부서진 여신상" 잡기의 무적 버그를 사용자가 실제 플레이로 찾아낸 뒤 —
    /// 기기를 git으로 맞춰 가며 검증을 반복하는 과정을 더 빠르게 하려고 만들었다).
    ///
    /// 게임 코드·씬은 건드리지 않는다. CLAUDE.md의 "원본에 없던 임시 디버그 키를 되살리지 말 것"은
    /// **게임 안 입력**(R 부활·Tab 캐릭터 전환·숫자키) 얘기라, 빌드에 들어가지 않는 에디터 메뉴는
    /// 해당하지 않는다(`MechanicTestMenu`와 같은 근거 — 그 파일 머리 주석 참고).
    ///
    /// `GameFlow.StartRun(region, RunMode.Boss, …, spendDay: false)`를 직접 부른다 —
    /// `Systems/EnemySpawner.cs`에 이미 "평소엔 안 타지만 `StartRun(region, RunMode.Boss)`로 바로
    /// 보스전을 여는 경로(테스트 등)를 테스트가 쓸 수 있다"고 적혀 있던 바로 그 경로다. 지역 토벌
    /// 100마리·보스 해금 여부는 전혀 확인하지 않는다 — 그 검사는 로비 UI(`StageSelectScreen`)에만
    /// 있고 `GameFlow.StartRun`/`CombatDirector.BeginField` 자체엔 없다. 그래서 진행 세이브를 전혀
    /// 건드리지 않고(멸망까지 하루도 안 깎는다, `spendDay: false`) 곧바로 보스방에 들어갈 수 있다.
    ///
    /// 타이틀·로비·결과 화면 등 전투 중이 아닌 어디서 눌러도 된다(`GameFlow.StartRun`이 전투 씬을
    /// 새로 불러온다). 이미 전투 씬이 떠 있어도(재도전과 같은 경로) 그대로 새로 불러온다.
    /// </summary>
    public static class BossFieldTestMenu
    {
        const string Root = "ChronoSavior/보스전 테스트/";

        // 지금 보스 전용 스크립트가 있는 지역만 이름까지 적는다(1·2지역). 다른 지역은 아직 공용
        // 보스(`MonsterCatalog.bossPrefab`)를 쓰므로 필요해지면 여기 한 줄만 더 추가하면 된다.
        [MenuItem(Root + "1지역 보스전 바로 입장 (덩어리 지배자)", priority = 1)] static void Region1() => Enter(1);
        [MenuItem(Root + "2지역 보스전 바로 입장 (부서진 여신상)", priority = 2)] static void Enter2() => Enter(2);

        static void Enter(int region)
        {
            if (!RequirePlaying()) return;

            var flow = Object.FindFirstObjectByType<GameFlow>();
            if (flow == null)
            {
                Debug.LogError("[보스전 테스트] GameFlow를 못 찾았다 — 00_Persistent가 떠 있는지 확인.");
                return;
            }

            bool accepted = flow.StartRun(region, RunMode.Boss, ok =>
            {
                if (ok) Debug.Log($"[보스전 테스트] {region}지역 보스전 입장 완료 — 토벌 0부터 바로 보스(진행 세이브는 안 건드림, 하루도 안 깎임)");
                else Debug.LogWarning($"[보스전 테스트] {region}지역 보스전 입장 실패 — 위 콘솔 오류 확인");
            }, spendDay: false);

            if (!accepted) Debug.LogWarning("[보스전 테스트] 지금은 받아들이지 않았다 — 화면 전환 중이면 끝난 뒤 다시 시도.");
        }

        static bool RequirePlaying()
        {
            if (EditorApplication.isPlaying) return true;
            EditorUtility.DisplayDialog("보스전 테스트", "Play 중에만 쓸 수 있어요. ▶를 누른 뒤 다시 골라 주세요.", "확인");
            return false;
        }
    }
}
