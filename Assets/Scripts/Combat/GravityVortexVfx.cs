using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 중력장(블랙홀)이 남아 있는 동안 보이는 암흑 구체 — 그림만이다(끌어당기는 힘·피해는 <see cref="GravityWellZone"/>).
    /// 2026-11-07 VFX 스프린트, 팀원 문서: "작은 dark_matter가 투사체, 착탄 때 큰 것을 투사체 크기로 바꿔 끼우고 중첩만큼 확대".
    /// Projectiles Pack 4 `pj4_dark_matter_large_violet`(키 <see cref="VfxKeys.MageGravityOrbLarge"/>, 96px = 0.75유닛 ≈ 완충 중력탄 그림)을 되풀이한다.
    ///
    /// - 크기: 도트 크기를 하나로 맞추면서(2026-11-09, <see cref="DotScale"/>) 늘리지 않고 **그림을 바꾼다** — 중첩 1~2는 작은 구체(48px → 0.75유닛, 중력탄과 같은 크기),
    ///   3 이상은 큰 구체(96px → 1.5유닛). 합쳐질 땐 판정 고리가 한 번 퍼지고 보라 반짝임이 튄다. 끌어당기는 판정 반경과는 따로다(그 둘레에서 보라 점이 빨려 든다).
    /// - 투명도: 착탄 즉시 보이고(폭발 그림이 그 위에 터진다), 중력장이 끝나기 <see cref="FadeOutTime"/>초 전부터 흐려진다.
    /// - 회전은 시키지 않는다(2026-10-25 사용자). 예전 그림(착탄 폭발의 가운데 프레임 반복 + 빨려드는 톱니 크기 변화)은 이걸로 바꿨다.
    /// 중력장 오브젝트의 자식이라 같이 사라진다. 그림이 없으면(팩을 안 가져온 클론) 붙지 않는다.
    /// </summary>
    public class GravityVortexVfx : MonoBehaviour
    {
        public const float Fps = 15f;
        public const float FadeOutTime = 0.5f;
        /// <summary>필드 효과 — 캐릭터·적(0) 뒤, 불길(-2) 뒤(CLAUDE.md 예약표).</summary>
        public const int SortingOrder = -3;

        GravityWellZone well;
        VfxPlayer player;
        VfxDots pull;
        static readonly Color PullColor = new Color(0.85f, 0.7f, 1f, 0.9f), SparkleColor = new Color(0.9f, 0.75f, 1f);

        public static GravityVortexVfx Attach(GravityWellZone well)
        {
            var frames = VfxFrames.Get(KeyFor(well.Stack));
            if (frames.Length == 0) return null;
            var go = new GameObject("GravityVortex", typeof(SpriteRenderer));
            go.transform.SetParent(well.transform, false);
            go.GetComponent<SpriteRenderer>().sortingOrder = SortingOrder;
            var v = go.AddComponent<GravityVortexVfx>();
            v.well = well;
            v.player = go.AddComponent<VfxPlayer>();
            v.player.Play(frames, Fps, true, 0f, Color.white);
            v.shownKey = KeyFor(well.Stack);
            go.transform.localScale = new Vector3(DotScale.Value, DotScale.Value, 1f);
            // 끌어당김이 보이게 — 바깥에서 가운데로 빨려 드는 보라 점(그림 없이, 2026-11-09 타격감). 구체와 달리 끌어당기는 범위 둘레에서 생긴다.
            v.pull = VfxDots.Attach(well.transform, VfxDots.Motion.Inward, 12, PullColor, well.Radius, 0.7f, 0f, SortingOrder + 1);
            v.Apply(0f);
            return v;
        }

        /// <summary>중첩 수에 맞는 구체 그림 — 1~2 작은 것, 3 이상 큰 것.</summary>
        public static string KeyFor(int stack) => stack >= 3 ? VfxKeys.MageGravityOrbLarge : VfxKeys.MageGravityOrbSmall;

        /// <summary>중첩 수에 맞는 구체 지름(유닛) — 그림 픽셀 × 도트 배율.</summary>
        public static float DiameterFor(int stack) => DotScale.Units(stack >= 3 ? 96f : 48f);

        string shownKey;

        /// <summary>남은 시간에 따른 투명도 — 끝나기 <see cref="FadeOutTime"/>초 전부터 0으로.</summary>
        public static float AlphaAt(float remaining) => Mathf.Clamp01(remaining / FadeOutTime);

        /// <summary>합쳐질 때 — 한 번 부풀었다 제 크기로(쿵), 보라 반짝임이 사방으로.</summary>
        public void Pulse()
        {
            if (well != null) RangeRing.Show(well.transform.position, well.Radius, SparkleColor); // 커진 범위가 한 번 퍼진다
            VfxDots.Attach(transform.parent, VfxDots.Motion.Burst, 14, SparkleColor, 0f, 0.45f, 3f, SortingOrder + 8);
        }

        /// <summary>지금 보이는 구체 그림 키(테스트용).</summary>
        public string ShownKey => shownKey;


        void Update() => Apply(Time.deltaTime); // 히트스톱(timeScale 0) 동안엔 같이 멈춘다

        void Apply(float dt)
        {
            if (well == null) return;
            string key = KeyFor(well.Stack);
            if (key != shownKey && VfxFrames.Has(key)) { shownKey = key; player.Play(VfxFrames.Get(key), Fps, true, 0f, Color.white); } // 중첩이 3을 넘으면 큰 구체로
            player.Alpha = AlphaAt(well.Remaining);
            if (pull != null)
            {
                pull.SetRadius(well.Radius);
                pull.Emitting = well.Remaining > FadeOutTime;
            }
        }
    }
}
