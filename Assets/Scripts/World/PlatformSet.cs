using UnityEngine;

namespace ChronoSavior.World
{
    /// <summary>
    /// 이 발판 묶음이 **어느 무대의 것인지** 표시한다. 원본이 런을 시작할 때
    /// `platforms = mode === 'normal' ? NORMAL_PLATFORMS : BOSS_PLATFORMS`(project_test.html:4316)로
    /// 통째로 갈아끼우는 것에 대응한다.
    ///
    /// 씬에는 두 묶음이 **다 만들어져 있고** 한쪽만 켜진다 — 런타임에 발판을 생성/파괴하면
    /// 물리 콜라이더가 프레임 중간에 사라져 서 있던 대상이 튀거나 빠진다.
    /// **켜고 끄는 건 같은 씬의 전투 담당(`Systems/CombatDirector`)이 직렬화 참조로 한다**(2026-11-05 씬 분할 2차 3-0). 예전엔 정적 목록이
    /// 모든 씬의 세트를 들고 있어서, 재도전 때 옛·새 전투 씬이 잠깐 겹치면 옛 씬의 발판까지 다시 켰다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlatformSet : MonoBehaviour
    {
        [Tooltip("체크하면 보스전 무대. 해제하면 일반 사냥 무대.")]
        public bool bossArena;

        /// <summary>
        /// 씬이 뜰 때 **지금 무대(<see cref="FieldLayout.IsBossArena"/>)에 맞춰 스스로 켜고 끈다** — 담당이 무대를 정하기 전 한 프레임 동안
        /// 두 세트가 같이 보이지 않게. 그래서 두 세트 모두 씬에 켜진 채로 저장돼 있어야 한다(꺼진 채 저장되면 Awake가 안 불린다 — 2026-11-04 발견).
        /// 씬 검증(`Editor/SceneSetupValidator`)이 저장 상태를 확인한다.
        /// </summary>
        void Awake() => gameObject.SetActive(bossArena == FieldLayout.IsBossArena);
    }
}
