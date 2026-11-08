using UnityEngine;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 중력장(블랙홀)이 남아 있는 동안 보이는 암흑 구체 — 그림만이다(끌어당기는 힘·피해는 <see cref="GravityWellZone"/>).
    /// 2026-11-07 VFX 스프린트, 팀원 문서: "작은 dark_matter가 투사체, 착탄 때 큰 것을 투사체 크기로 바꿔 끼우고 중첩만큼 확대".
    /// Projectiles Pack 4 `pj4_dark_matter_large_violet`(키 <see cref="VfxKeys.MageGravityOrbLarge"/>, 96px = 0.75유닛 ≈ 완충 중력탄 그림)을 되풀이한다.
    ///
    /// - 크기: 그림 배율 = **중첩 수**(1~5, 정수 — 도트 배율 정수). 착탄 때 1배(투사체 크기)로 나타나 중첩이 늘면 부드럽게 커진다.
    ///   끌어당기는 판정 반경(1.3~3.3)과는 따로다 — 이건 구체의 '핵' 크기(Codex 합의). 최대 3.75유닛으로 판정 지름보다 늘 작다.
    /// - 투명도: 착탄 즉시 보이고(폭발 그림이 그 위에 터진다), 중력장이 끝나기 <see cref="FadeOutTime"/>초 전부터 흐려진다.
    /// - 회전은 시키지 않는다(2026-10-25 사용자). 예전 그림(착탄 폭발의 가운데 프레임 반복 + 빨려드는 톱니 크기 변화)은 이걸로 바꿨다.
    /// 중력장 오브젝트의 자식이라 같이 사라진다. 그림이 없으면(팩을 안 가져온 클론) 붙지 않는다.
    /// </summary>
    public class GravityVortexVfx : MonoBehaviour
    {
        public const float Fps = 15f;
        public const float FadeOutTime = 0.5f;
        /// <summary>중첩이 늘 때 커지는 속도(배율/초) — 한 단계(0.75유닛)를 약 0.2초에.</summary>
        public const float GrowSpeed = 5f;
        /// <summary>필드 효과 — 캐릭터·적(0) 뒤, 불길(-2) 뒤(CLAUDE.md 예약표).</summary>
        public const int SortingOrder = -3;

        GravityWellZone well;
        VfxPlayer player;
        VfxDots pull;
        float scale = 1f;
        /// <summary>합쳐질 때 부푸는 정도(배율 +) — 그 뒤 <see cref="GrowSpeed"/>로 제 크기로 돌아온다.</summary>
        public const float PulseScale = 1f;
        static readonly Color PullColor = new Color(0.85f, 0.7f, 1f, 0.9f), SparkleColor = new Color(0.9f, 0.75f, 1f);

        public static GravityVortexVfx Attach(GravityWellZone well)
        {
            var frames = VfxFrames.Get(VfxKeys.MageGravityOrbLarge);
            if (frames.Length == 0) return null;
            var go = new GameObject("GravityVortex", typeof(SpriteRenderer));
            go.transform.SetParent(well.transform, false);
            go.GetComponent<SpriteRenderer>().sortingOrder = SortingOrder;
            var v = go.AddComponent<GravityVortexVfx>();
            v.well = well;
            v.player = go.AddComponent<VfxPlayer>();
            v.player.Play(frames, Fps, true, 0f, Color.white);
            // 끌어당김이 보이게 — 바깥에서 가운데로 빨려 드는 보라 점(그림 없이, 2026-11-09 타격감). 구체와 달리 끌어당기는 범위 둘레에서 생긴다.
            v.pull = VfxDots.Attach(well.transform, VfxDots.Motion.Inward, 12, PullColor, well.Radius, 0.7f, 0f, SortingOrder + 1);
            v.Apply(0f);
            return v;
        }

        /// <summary>중첩 수에 맞는 그림 배율(정수, 1~5).</summary>
        public static int ScaleFor(int stack) => Mathf.Clamp(stack, 1, 5);

        /// <summary>남은 시간에 따른 투명도 — 끝나기 <see cref="FadeOutTime"/>초 전부터 0으로.</summary>
        public static float AlphaAt(float remaining) => Mathf.Clamp01(remaining / FadeOutTime);

        /// <summary>합쳐질 때 — 한 번 부풀었다 제 크기로(쿵), 보라 반짝임이 사방으로.</summary>
        public void Pulse()
        {
            scale = ScaleFor(well != null ? well.Stack : 1) + PulseScale;
            VfxDots.Attach(transform.parent, VfxDots.Motion.Burst, 14, SparkleColor, 0f, 0.45f, 3f, SortingOrder + 8);
        }

        /// <summary>지금 그림 배율(테스트용 — 중첩이 늘면 목표까지 커지는 중일 수 있다).</summary>
        public float Scale => scale;

        void Update() => Apply(Time.deltaTime); // 히트스톱(timeScale 0) 동안엔 같이 멈춘다

        void Apply(float dt)
        {
            if (well == null) return;
            scale = Mathf.MoveTowards(scale, ScaleFor(well.Stack), GrowSpeed * dt);
            transform.localScale = new Vector3(scale, scale, 1f);
            player.Alpha = AlphaAt(well.Remaining);
            if (pull != null)
            {
                pull.SetRadius(well.Radius);
                pull.Emitting = well.Remaining > FadeOutTime;
            }
        }
    }
}
