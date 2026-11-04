using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.World
{
    /// <summary>
    /// 카메라 배경을 그 지역 하늘색으로 맞춘다 — 원본 `drawBackground()`가 캔버스에 먼저 깔는
    /// 밤 하늘(`regionSkyTint(r)`, project_test.html:4483 부근)에 해당한다.
    ///
    /// ## 왜 필요했나
    /// 씬 빌더가 카메라 배경을 `Color.white`로 두고 있었다(초기 스캐폴딩의 자리표시자).
    /// 원본은 어두운 하늘을 먼저 그리니 흰 배경이 보일 일이 없는데, 우리는 그 렌더링을 이식하지
    /// 않은 채 흰색으로 지우고 있었다. 그 결과:
    ///
    ///   1. 갈색 발판선·몹이 흰 바탕에서 눈에 찌르고,
    ///   2. **로비 판이 `rgba(14,11,20,.92)`라 8%가 통과하는데 그게 흰색**이어서 UI 전체가
    ///      뿌옇게 뜨고 발판선이 판을 가로질러 보였다.
    ///
    /// 두 번째가 사용자가 "UI가 깨져 보인다"고 지적한 증상의 직접적인 원인이다 —
    /// UI 코드가 아니라 **카메라 배경**이 문제였다.
    ///
    /// ## 아직 이식하지 않은 것
    /// 원본 하늘은 3단 **세로 그라데이션** + 별 + 달 + 대나무·토리이·등불이다. 카메라는 단색으로만
    /// 지울 수 있어서 가운데 색 하나를 쓴다. 그라데이션과 장식은 원본 렌더링 구간(`:4482`~`:6215`)
    /// 이라 여전히 범위 밖이다 — 필요해지면 전체 화면 쿼드로 올리면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class SkyBackground : MonoBehaviour
    {
        Camera cam;
        int appliedRegion = -1;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            Apply();
        }

        /// <summary>
        /// 지역은 런을 시작할 때 바뀐다. 이벤트로 받지 않고 매 프레임 비교하는 이유는,
        /// **로비에서도 1지역 하늘이 보여야** 하고(런 밖에서는 `RunState.Region`이 그대로다)
        /// 지역이 바뀌는 경로가 여러 개라 한 곳에서 이벤트를 쏘기 어렵기 때문이다.
        /// 비교는 int 하나라 비용이 없다.
        /// </summary>
        void Update() => Apply();

        void Apply()
        {
            int region = RunState.Region;
            if (region == appliedRegion || cam == null) return;
            appliedRegion = region;
            cam.backgroundColor = RegionConfig.SkyColor(region);
        }
    }
}
