using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 자동 저장. 원본이 `saveMeta()`를 부르는 지점들(런 종료 `:4357`, 로비 복귀 `:4389`,
    /// 강화 구매 `:7097` 등)에 대응한다 — **화면이 바뀔 때마다** 저장한다고 보면 된다.
    /// 불러오기는 게임 시작 담당 <see cref="GameBoot"/>가 맨 먼저 한다(2026-11-04 씬 분할 — 예전엔 여기 Awake에서
    /// '씬 빌더가 이 오브젝트를 먼저 만든다'는 생성 순서에 기대고 있었다).
    ///
    /// <see cref="SaveService"/>와 나눠 둔 이유는 테스트다. 저장 로직 자체는 순수 함수라 테스트하기 쉽지만,
    /// 자동 동작이 컴포넌트에 섞여 있으면 PlayMode 테스트가 **개발자의 진짜 세이브 파일을 덮어쓴다.**
    /// 그래서 자동 동작은 `00_Persistent` 씬에만 놓인 이 컴포넌트와 GameBoot에만 두고, 테스트는 둘 다 안 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoSave : MonoBehaviour
    {
        void OnEnable() => GameState.Changed += HandleSceneChanged;
        void OnDisable() => GameState.Changed -= HandleSceneChanged;

        /// <summary>원본은 런이 끝날 때(:4357)와 로비로 돌아올 때(:4389) 저장한다.</summary>
        void HandleSceneChanged(GameScene scene)
        {
            if (scene == GameScene.Result || scene == GameScene.Lobby) SaveService.Save();
        }

        // 알트+F4로 꺼도 마지막 상태가 남게 — 원본은 매 변경마다 저장이라 이 문제가 없다.
        void OnApplicationQuit() => SaveService.Save();
    }
}
