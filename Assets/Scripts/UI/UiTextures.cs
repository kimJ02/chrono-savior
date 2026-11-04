using UnityEngine;

namespace ChronoSavior.UI
{
    /// <summary>
    /// Figma에서 받은 UI 이미지를 **런타임에 들고 있는 유일한 지점**.
    ///
    /// ## 왜 컴포넌트인가
    /// <see cref="UiTheme"/>은 정적 클래스라 에셋 참조를 직렬화할 수 없고, 런타임에
    /// `AssetDatabase`를 쓸 수도 없다(에디터 전용 API라 빌드에서 사라진다). 그래서 씬에 있는
    /// 이 컴포넌트가 인스펙터로 텍스처를 받아(씬 빌더가 꽂는다) `Awake`에서 `UiTheme`에 넘긴다 —
    /// 프로젝트가 이미 쓰는 방식(`spawner.orbSprite`, `rig.characterSprites`)과 같은 패턴이다.
    ///
    /// 텍스처가 비어 있어도 화면은 깨지지 않는다 — `UiTheme`의 그리기 함수들이 null을 확인하고
    /// 도형·색으로 대체한다. 아트가 아직 없는 자리(타이틀 배경·로고)가 실제로 그 상태다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiTextures : MonoBehaviour
    {
        [Header("회귀창 (Figma 31:4)")]
        [Tooltip("회색 창 프레임 512×288. 제목 아래 구분선이 그림에 포함돼 있다.")]
        public Texture2D windowFrame;
        [Tooltip("깃발 32×32 — 최고 스테이지")]
        public Texture2D iconFlag;
        [Tooltip("별 32×32 — 최고 레벨")]
        public Texture2D iconStar;
        [Tooltip("해골 32×32 — 총 처치 수")]
        public Texture2D iconSkull;
        [Tooltip("시간의 파편 32×32 — 획득 가능한 파편")]
        public Texture2D iconShard;

        [Header("타이틀 (Figma 35:2)")]
        [Tooltip("메뉴 버튼 플레이트 256×64. 모서리가 깎인 형태라 9-슬라이스로 늘린다.")]
        public Texture2D menuPlate;

        [Header("전투 HUD 스킬 슬롯 (Figma \"보스 HUD\" 124:3 — 181:2~181:4)")]
        [Tooltip("Z 기본공격 칸 32×32 — 회색 틀 + 칼")]
        public Texture2D skillSlotAttack;
        [Tooltip("X 전문화 스킬 칸 32×32 — 회색 틀 + 마법진")]
        public Texture2D skillSlotSkill;
        [Tooltip("C 점프 칸 32×32 — 회색 틀 + 위 화살표")]
        public Texture2D skillSlotJump;

        void Awake() => UiTheme.SetTextures(this);
    }
}
