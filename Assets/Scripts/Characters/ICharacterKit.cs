using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 캐릭터 하나의 "키트" — 그 캐릭터의 Z 기본공격과 (필요하면) X 스킬·자원을 담당하는 컴포넌트.
    /// 원본은 `updatePlayer`가 `meta.weapon`으로 분기해서 캐릭터별 갱신 함수를 부르는데
    /// (project_test.html:3509~3538), 우리는 캐릭터마다 컴포넌트를 하나씩 두고
    /// <see cref="PlayerRig"/>가 선택된 것만 켠다.
    ///
    /// **구현 규칙**
    /// - `MonoBehaviour`로 만들고 플레이어 오브젝트에 붙인다. 켜고 끄는 건 `PlayerRig`가 한다 —
    ///   스스로 `enabled`를 바꾸지 말 것.
    /// - 키 입력은 반드시 `Core.GameInput`을 통해서 읽는다(`Input.GetKey` 직접 호출 금지).
    /// - 이동 방식이 다르면 <see cref="RequiredMoveMode"/>로 알린다 — `PlayerRig`가 전환 시 적용한다.
    /// </summary>
    public interface ICharacterKit
    {
        /// <summary>이 키트가 담당하는 캐릭터.</summary>
        CharacterId Character { get; }

        /// <summary>
        /// 이 캐릭터가 쓰는 수평 이동 방식. 마법사·메카닉은 <c>Instant</c>, 섬영은 <c>Inertial</c>
        /// (원본 `bladeMove`). 드루이드는 평상시 <c>Instant</c>(맹금 변신은 티어라 0차 범위 밖).
        /// </summary>
        CharacterMover2D.MoveMode RequiredMoveMode { get; }

        /// <summary>이 캐릭터로 전환됐을 때 1회. 자원(마나·스택)·쿨다운 초기화 용도.</summary>
        void OnSelected();

        /// <summary>다른 캐릭터로 바뀔 때 1회. 진행 중이던 차지·표시물 정리 용도.</summary>
        void OnDeselected();
    }
}
