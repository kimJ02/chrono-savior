using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.World
{
    /// <summary>
    /// 카메라가 플레이어를 따라간다. 원본은 mapW 2600px(26유닛)짜리 옆으로 긴
    /// 레벨이라 스프린트 1 스펙(docs/sprints/01-combat-core.md)이 처음 가정한 "필드 전체가 한 화면에 고정" 카메라로는
    /// 발판/플레이어가 너무 작아져서 실질적으로 원본과 달라진다 — 실제 발판을 넣으면서
    /// 스크롤 카메라로 바꾼 의도적 변경이다(PROGRESS.md 로그 참고).
    ///
    /// **2026-10-24 C안**(`StageArtConfig`): Figma 도트를 선명하게 보이려고 월드를 960×540px(1유닛 = 128px) 그대로
    /// 그려서 정수배로 키운다(`PixelPerfectCamera`). 한 화면에 발판이 두 층 정도만 들어와서 **Y도 따라간다** —
    /// 예전엔 모든 발판이 세로로 한 화면에 들어와서 X만 따라갔다.
    /// Y는 서 있을 때만 그 높이로 맞추고, 공중에선 화면 밖으로 나가려 할 때만 움직인다(점프마다 화면이 출렁이지 않게).
    ///
    /// **화면 크기 설정(`PixelPerfectCamera` 128PPU·960×540·`UpscaleRenderTexture`·`StretchFill`, 카메라 크기)은 씬(`00_Persistent`)이 원본이다**
    /// (2026-11-05). 예전엔 이 컴포넌트의 Awake가 PPC를 붙이고 값을 덮어써서, 씬에는 기본값(100PPU·320×180)이 저장돼 있었다.
    /// 지금은 따라가기만 하고, 씬 값은 `Editor/SceneSetupValidator`가 <see cref="StageArtConfig"/>와 맞는지 검사한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        /// <summary>
        /// 따라갈 대상. 카메라는 `00_Persistent`, 플레이어는 `20_Combat`에 있어 씬끼리는 직렬화로 이을 수 없다(2026-11-04 씬 분할) —
        /// **대상 쪽이 스스로 등록한다**(<see cref="Track"/>/<see cref="Untrack"/>). 카메라는 매 프레임 이 값을 읽으므로
        /// 카메라와 대상 중 무엇이 먼저 켜져도 연결된다.
        /// </summary>
        public static Transform Target { get; private set; }

        public static void Track(Transform t) => Target = t;

        /// <summary>그 대상이 지금 등록된 대상일 때만 지운다 — 다른 대상이 이미 덮어썼으면 건드리지 않는다.</summary>
        public static void Untrack(Transform t)
        {
            if (Target == t) Target = null;
        }

        public float smoothTime = 0.15f;

        float velocityX;
        float velocityY;
        float followY;
        bool snapped;
        Vector3 basePos; // 흔들림을 뺀, 따라가는 위치 — 흔들림이 다음 프레임 따라가기에 섞이지 않게 따로 둔다

        /// <summary>이번 프레임 흔들림 오프셋(월드). 테스트가 읽는다.</summary>
        public Vector3 LastShakeOffset { get; private set; }

        /// <summary>원본 흔들림 px → 우리 월드 유닛. 화면 높이 대비 같은 비율(원본 720px ↔ 우리 4.22유닛).</summary>
        public const float ShakePxToUnits = StageArtConfig.ViewHalfHeight * 2f / HitFeel.OriginalViewHeightPx;
        Rigidbody2D targetBody;
        Transform bodyOwner;

        void LateUpdate()
        {
            var target = Target;
            if (target == null) return;
            if (bodyOwner != target) { bodyOwner = target; targetBody = target.GetComponent<Rigidbody2D>(); }

            float halfWidth = StageArtConfig.ViewHalfWidth;
            float min = FieldBounds.MinX + halfWidth;
            float max = FieldBounds.MaxX - halfWidth;
            float desiredX = (min <= max) ? Mathf.Clamp(target.position.x, min, max) : (FieldBounds.MinX + FieldBounds.MaxX) * 0.5f;
            // 로비는 한 화면 — 카메라가 필드 가운데에 서 있고 건물 넷이 다 보인다(사용자 2026-10-28, `UI/Screens/LobbyWorld`).
            if (GameState.Current == GameScene.Lobby) desiredX = (FieldBounds.MinX + FieldBounds.MaxX) * 0.5f;

            float feetY = target.position.y - EntitySizeConfig.PlayerRadius;
            bool standing = targetBody == null || Mathf.Abs(targetBody.linearVelocity.y) < 0.05f;
            if (!snapped) followY = DesiredY(feetY);
            followY = NextFollowY(followY, feetY, standing);

            if (!snapped)
            {
                basePos = new Vector3(desiredX, followY, transform.position.z);
                snapped = true;
            }
            else
            {
                // 로비는 시간이 멈춘 채(timeScale 0) 캐릭터만 걷는다(`UI/Screens/LobbyWorld`) — 카메라도 실제 시간으로 따라간다.
                // 사냥 중엔 원본처럼 게임 시간(히트스톱·일시정지에 멈춤).
                float dt = GameState.Current == GameScene.Lobby ? Time.unscaledDeltaTime : Time.deltaTime;
                // 시간이 멈춰 있으면(타이틀·일시정지·결과·히트스톱, dt = 0) 따라가지 않는다. `Mathf.SmoothDamp`는 dt가 0이고 이미 목표에 서 있으면
                // 속도를 0/0으로 계산해 NaN을 만들고, 그 뒤로 카메라 위치가 영영 NaN이 된다(2026-11-04 씬 부팅 테스트가 잡음).
                if (dt > 0f)
                {
                    basePos.x = Mathf.SmoothDamp(basePos.x, desiredX, ref velocityX, smoothTime, Mathf.Infinity, dt);
                    basePos.y = Mathf.SmoothDamp(basePos.y, followY, ref velocityY, StageArtConfig.CameraSmoothTimeY, Mathf.Infinity, dt);
                }
            }

            // 원본 `updateShake(rawDt)` — 히트스톱 중에도 흔들린다(실제 시간).
            float m = HitFeel.TickShake(Time.unscaledDeltaTime) * ShakePxToUnits * HitFeel.ShakeStrength; // 세기 1.5배(사용자 2026-10-28)
            LastShakeOffset = m > 0f ? new Vector3(Random.Range(-m, m), Random.Range(-m, m), 0f) : Vector3.zero;
            transform.position = basePos + LastShakeOffset;
        }

        /// <summary>카메라 중심 높이의 아래·위 한계 — 땅에 설 때가 아래 끝, 가장 높은 발판에 설 때가 위 끝.</summary>
        public static float MinY => FieldBounds.GroundY + StageArtConfig.CameraYOffset;

        public static float MaxY
        {
            get
            {
                float top = FieldBounds.GroundY;
                var p = FieldLayout.Platforms;
                for (int i = 0; i < p.GetLength(0); i++) top = Mathf.Max(top, FieldLayout.PlatformTopY(i));
                return top + StageArtConfig.CameraYOffset;
            }
        }

        /// <summary>그 발 높이에 서 있을 때 카메라 중심 높이.</summary>
        public static float DesiredY(float feetY) => Mathf.Clamp(feetY + StageArtConfig.CameraYOffset, MinY, MaxY);

        /// <summary>
        /// 다음 목표 높이. 서 있으면 그 높이로, 공중이면 머리가 화면 위 여유(<see cref="StageArtConfig.CameraAirTopMargin"/>)를
        /// 넘거나 발이 화면 아래 여유 밖으로 떨어질 때만 그만큼 옮긴다.
        /// </summary>
        public static float NextFollowY(float followY, float feetY, bool standing)
        {
            if (standing) return DesiredY(feetY);

            float head = feetY + EntitySizeConfig.PlayerHeight;
            float top = followY + StageArtConfig.ViewHalfHeight - StageArtConfig.CameraAirTopMargin;
            float bottom = followY - StageArtConfig.ViewHalfHeight + StageArtConfig.CameraAirBottomMargin;
            if (head > top) followY += head - top;
            else if (feetY < bottom) followY -= bottom - feetY;
            return Mathf.Clamp(followY, MinY, MaxY);
        }
    }
}
