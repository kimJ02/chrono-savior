using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 자동 저장/로드. 원본이 `saveMeta()`를 부르는 지점들(런 종료 `:4357`, 로비 복귀 `:4389`,
    /// 강화 구매 `:7097` 등)에 대응한다 — **화면이 바뀔 때마다** 저장한다고 보면 된다.
    ///
    /// <see cref="SaveService"/>와 나눠 둔 이유는 테스트다. 저장/로드 로직 자체는 순수 함수라
    /// 테스트하기 쉽지만, "게임이 켜지면 실제 세이브 파일을 읽는다"가 컴포넌트에 섞여 있으면
    /// PlayMode 테스트가 **개발자의 진짜 세이브 파일을 읽고 덮어쓴다.** 그래서 자동 동작은 이
    /// 컴포넌트에만 두고, 씬 빌더만 이걸 붙인다(테스트는 안 붙인다).
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoSave : MonoBehaviour
    {
        void Awake()
        {
            // 다른 컴포넌트의 Awake보다 먼저 읽혀야 한다는 보장은 없지만, 프로필을 실제로 쓰는 건
            // 전부 로비 진입 이후(Start 뒤)라 문제되지 않는다. PlayerHealth만 Awake에서 구독하는데
            // 그건 인스턴스가 아니라 `ProfileService.Current`를 그때그때 읽는 구조가 아니라서
            // 여기서 교체가 끝난 뒤 붙는 게 맞다 — 씬 빌더가 이 오브젝트를 먼저 만든다.
            if (SaveService.Load()) Debug.Log($"[AutoSave] 세이브 불러옴 — {SaveService.SavePath}");
        }

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
