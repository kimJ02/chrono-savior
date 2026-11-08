using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 살수(코드 이름 섬영 `Blade*`) 스킬 그림 — 그림만이다(판정·수치는 <see cref="BladeCombat"/>, 기본공격 베기는 <see cref="BladeSlashVfx"/>).
    /// 2026-11-08 VFX 스프린트, 팀원 문서 '이펙트와 연출':
    /// - **집중**: "플랫폼이랑 적이 잘 보이게 배경이 어두워지는 효과 + 하트비트" — 배경(-100)과 땅(-5) 사이(<see cref="ShadeSortingOrder"/>)에 검은 반투명 판을 깔고,
    ///   머리 위에 기가팩 `scifi_heartbeat`(키 <see cref="VfxKeys.AssassinHeartbeat"/>)를 되풀이한다.
    /// - **궤적**: 원본 `drawBladeTrail`(project_test.html:5670) 그대로 — 지나간 길을 따라 넓은 반투명 하늘색 선 + 가는 밝은 선. 예전 Unity엔 궤적 그림이 없었다.
    ///   **파열**(4층 궤적 파열)되면 궤적이 한 번 하얗게 번쩍이고, 기가팩 스플래터(`assassin/splatter`)를 길을 따라 일정 간격으로 뿌린다.
    /// - **칼날폭풍**: Slash Vol 2의 회전베기(`assassin/spin`)를 되풀이하고, 지나간 길에 기가팩 연기(`assassin/smoke`)를 남긴다 — 연기는 사라질 때 투명해진다.
    /// 선은 머티리얼이 필요해 씬 연결 칸(<see cref="lineMaterial"/>)으로 받는다. 씬의 Player에 놓인다. 라이선스 그림이 없으면(팩을 안 가져온 클론) 그 부분만 안 보인다.
    /// </summary>
    [RequireComponent(typeof(BladeCombat))]
    public class BladeVfx : MonoBehaviour
    {
        // ── 집중 ──
        /// <summary>배경을 덮는 정렬(배경 -100 위, 땅·발판 -5 아래 — 그래서 땅·발판·적·캐릭터는 그대로 밝다).</summary>
        public const int ShadeSortingOrder = -50;
        public const float ShadeAlpha = 0.55f, ShadeFadeTime = 0.2f;
        /// <summary>어두운 판 크기(유닛) — 화면(7.5×4.2)보다 넉넉히, 캐릭터를 따라다닌다.</summary>
        const float ShadeSize = 40f;
        /// <summary>하트비트 그림 위치(몸 중심에서 위로)·재생 속도.</summary>
        public const float HeartbeatHeight = 0.55f, HeartbeatFps = 15f;

        // ── 궤적(원본 drawBladeTrail) ──
        /// <summary>원본 넓은 선 26px(×3층 반경 배수)·가는 선 3.4px — 도트 단위로 맞춰 넓은 선 32칸·가는 선 4칸(도트 2개, 2026-11-09). 색과 투명도.</summary>
        public const float TrailWideWidth = 0.25f, TrailThinWidth = 4f / 128f;
        static readonly Color TrailWideColor = new Color(143 / 255f, 228 / 255f, 1f, 0.22f), TrailThinColor = new Color(215 / 255f, 245 / 255f, 1f, 0.85f);
        /// <summary>원본: 궤적 시간이 끝난 뒤(파열 전) 남은 선은 0.75배로 흐리게.</summary>
        public const float TrailEndedFade = 0.75f;
        /// <summary>파열 섬광 시간(초)·스플래터 간격(유닛)·배율.</summary>
        public const float RipFlashTime = 0.18f, SplatterSpacing = 0.5f;
        public const int SplatterScale = 2;

        // ── 칼날폭풍 ──
        public const float SpinFps = 20f, SmokeSpacing = 0.35f, SmokeFps = 15f, SmokeFade = 0.4f;
        public const int SmokeScale = 2;
        /// <summary>연기는 땅(-5) 바로 위, 필드 효과·캐릭터 뒤(CLAUDE.md 예약표).</summary>
        public const int SmokeSortingOrder = -4;
        /// <summary>폭발·베기 그림 정렬(캐릭터 앞 — 폭발 4와 같은 줄).</summary>
        public const int EffectSortingOrder = 4;

        [Tooltip("궤적 선 머티리얼(Assets/Materials/VfxUnlit — 꼭짓점 색을 그대로 칠하는 스프라이트 셰이더). 외주 효과로 바꿀 땐 이 칸만.")]
        [SerializeField] Material lineMaterial;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public Material LineMaterial { get => lineMaterial; set => lineMaterial = value; }

        BladeCombat blade;
        SpriteRenderer shade;
        VfxPlayer heartbeat, spin;
        LineRenderer trailWide, trailThin;
        float shadeAlpha, ripFlash;
        Vector2 lastSmoke;
        bool wasStorming;
        static Sprite whiteSprite;

        /// <summary>테스트용.</summary>
        public float ShadeAmount => shadeAlpha;
        public bool HeartbeatVisible => heartbeat != null && heartbeat.gameObject.activeSelf;
        public bool SpinVisible => spin != null && spin.gameObject.activeSelf;
        public int TrailPointCount => trailWide != null && trailWide.enabled ? trailWide.positionCount : 0;

        void Awake() => blade = GetComponent<BladeCombat>();

        void OnEnable()
        {
            if (blade == null) blade = GetComponent<BladeCombat>();
            if (blade != null) blade.TrailRipped += OnTrailRipped;
        }

        void OnDisable()
        {
            if (blade != null) blade.TrailRipped -= OnTrailRipped;
            HideAll();
        }

        void LateUpdate() => Refresh(Time.deltaTime); // BladeCombat.Update 뒤에 — 일시정지·히트스톱 땐 dt 0

        /// <summary>그림을 지금 상태에 맞춘다(테스트가 직접 부른다).</summary>
        public void Refresh(float dt)
        {
            if (blade == null) blade = GetComponent<BladeCombat>();
            bool active = blade != null && blade.enabled && GameState.IsRunning;
            UpdateFocus(active && blade.IsFocusing, dt);
            UpdateTrail(active, dt);
            UpdateStorm(active && blade.IsStorming);
        }

        void HideAll()
        {
            shadeAlpha = 0f;
            if (shade != null) shade.gameObject.SetActive(false);
            if (heartbeat != null) heartbeat.gameObject.SetActive(false);
            if (spin != null) spin.gameObject.SetActive(false);
            if (trailWide != null) { trailWide.enabled = false; trailThin.enabled = false; }
        }

        // ── 집중: 배경 어둡게 + 하트비트 ──
        void UpdateFocus(bool focusing, float dt)
        {
            shadeAlpha = Mathf.MoveTowards(shadeAlpha, focusing ? ShadeAlpha : 0f, ShadeAlpha * dt / ShadeFadeTime);
            if (shadeAlpha > 0f || focusing)
            {
                if (shade == null)
                {
                    var go = new GameObject("FocusShade", typeof(SpriteRenderer)); // 캐릭터 배율과 무관하게 월드 오브젝트로
                    RunTransient.Mark(go);
                    shade = go.GetComponent<SpriteRenderer>();
                    shade.sprite = WhiteSprite();
                    shade.sortingOrder = ShadeSortingOrder;
                    go.transform.localScale = new Vector3(ShadeSize / shade.sprite.bounds.size.x, ShadeSize / shade.sprite.bounds.size.y, 1f);
                }
                shade.gameObject.SetActive(true);
                shade.transform.position = transform.position;
                shade.color = new Color(0f, 0f, 0f, shadeAlpha);
            }
            else if (shade != null) shade.gameObject.SetActive(false);

            if (focusing)
            {
                if (heartbeat == null) heartbeat = NewLoop("FocusHeartbeat", VfxKeys.AssassinHeartbeat, HeartbeatFps, DotScale.Value);
                if (heartbeat != null)
                {
                    if (!heartbeat.gameObject.activeSelf) { heartbeat.gameObject.SetActive(true); heartbeat.Play(VfxFrames.Get(VfxKeys.AssassinHeartbeat), HeartbeatFps, true, 0f, Color.white); }
                    heartbeat.transform.position = transform.position + new Vector3(0f, HeartbeatHeight, 0f);
                }
            }
            else if (heartbeat != null) heartbeat.gameObject.SetActive(false);
        }

        // ── 궤적(원본 drawBladeTrail) + 파열 섬광 ──
        void UpdateTrail(bool active, float dt)
        {
            if (lineMaterial == null) return; // 씬엔 늘 있다(씬 구성 검증이 본다)
            var pts = active ? blade.TrailPoints : null;
            ripFlash = Mathf.Max(0f, ripFlash - dt);
            bool draw = pts != null && pts.Count >= 2;
            if (!draw && ripFlash <= 0f)
            {
                if (trailWide != null) { trailWide.enabled = false; trailThin.enabled = false; }
                return;
            }
            EnsureLines();
            if (draw) SetLinePoints(pts);
            float fade = blade.TrailLive ? 1f : TrailEndedFade;
            float wide = TrailWideWidth * blade.TrailWidthScale;
            if (ripFlash > 0f)
            {
                // 파열 섬광 — 마지막 궤적이 하얗게 번쩍였다 사라진다
                float k = ripFlash / RipFlashTime;
                trailWide.startColor = trailWide.endColor = new Color(1f, 1f, 1f, 0.6f * k);
                trailThin.startColor = trailThin.endColor = new Color(1f, 1f, 1f, k);
                trailWide.widthMultiplier = wide * (1f + 0.6f * k);
            }
            else
            {
                trailWide.startColor = trailWide.endColor = new Color(TrailWideColor.r, TrailWideColor.g, TrailWideColor.b, TrailWideColor.a * fade);
                trailThin.startColor = trailThin.endColor = new Color(TrailThinColor.r, TrailThinColor.g, TrailThinColor.b, TrailThinColor.a * fade);
                trailWide.widthMultiplier = wide;
            }
            trailThin.widthMultiplier = TrailThinWidth;
            trailWide.enabled = trailThin.enabled = true;
        }

        void EnsureLines()
        {
            if (trailWide != null) return;
            trailWide = NewLine("BladeTrailWide", EffectSortingOrder - 1); // 캐릭터 앞, 폭발 아래(원본도 궤적을 캐릭터 뒤가 아니라 위에 그린다)
            trailThin = NewLine("BladeTrailThin", EffectSortingOrder - 1);
            trailThin.sortingOrder = EffectSortingOrder - 1;
        }

        LineRenderer NewLine(string name, int order)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            RunTransient.Mark(go);
            var lr = go.GetComponent<LineRenderer>();
            lr.sharedMaterial = lineMaterial;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4; // 원본 lineCap round
            lr.numCornerVertices = 2; // 원본 lineJoin round
            lr.sortingOrder = order;
            lr.alignment = LineAlignment.TransformZ;
            return lr;
        }

        void SetLinePoints(IReadOnlyList<Vector2> pts)
        {
            trailWide.positionCount = trailThin.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                trailWide.SetPosition(i, pts[i]);
                trailThin.SetPosition(i, pts[i]);
            }
        }

        void OnTrailRipped(IReadOnlyList<Vector2> pts)
        {
            if (pts == null || pts.Count == 0) return;
            if (lineMaterial != null && pts.Count >= 2) { EnsureLines(); SetLinePoints(pts); ripFlash = RipFlashTime; }
            // 길을 따라 일정 간격으로 스플래터 — 방향은 그 자리의 진행 방향
            float carry = SplatterSpacing; // 첫 점에도 하나
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 prev = i > 0 ? pts[i - 1] : pts[i];
                carry += Vector2.Distance(prev, pts[i]);
                if (carry < SplatterSpacing) continue;
                carry = 0f;
                Vector2 dir = i > 0 ? pts[i] - prev : (pts.Count > 1 ? pts[1] - pts[0] : Vector2.right);
                var v = VfxPlayer.Spawn(VfxKeys.AssassinSplatter, pts[i], SplatterScale, 15f, EffectSortingOrder, false, 0.15f);
                if (v != null && dir.sqrMagnitude > 1e-6f) v.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }

        // ── 칼날폭풍: 회전베기 반복 + 길 따라 연기 ──
        void UpdateStorm(bool storming)
        {
            if (storming)
            {
                if (spin == null) spin = NewLoop("StormSpin", VfxKeys.AssassinSpin, SpinFps, SpinScale());
                if (spin != null)
                {
                    if (!spin.gameObject.activeSelf) { spin.gameObject.SetActive(true); spin.Play(VfxFrames.Get(VfxKeys.AssassinSpin), SpinFps, true, 0f, Color.white); }
                    spin.transform.position = transform.position;
                }
                Vector2 here = transform.position;
                if (!wasStorming) lastSmoke = here;
                if (Vector2.Distance(here, lastSmoke) >= SmokeSpacing)
                {
                    lastSmoke = here;
                    VfxPlayer.Spawn(VfxKeys.AssassinSmoke, here, SmokeScale, SmokeFps, SmokeSortingOrder, false, SmokeFade);
                }
            }
            else if (spin != null) spin.gameObject.SetActive(false);
            wasStorming = storming;
        }

        /// <summary>회전베기 그림 배율 — 공통 도트 배율(96px → 1.5유닛, 판정 지름 2.4와 따로).</summary>
        static int SpinScale() => DotScale.Value;

        /// <summary>캐릭터를 따라다니는 반복 그림 하나(월드 오브젝트 — 캐릭터 배율과 무관한 정수 배율). 그림이 없으면 null.</summary>
        static VfxPlayer NewLoop(string name, string key, float fps, int scale)
        {
            var frames = VfxFrames.Get(key);
            if (frames.Length == 0) return null;
            var go = new GameObject(name, typeof(SpriteRenderer));
            RunTransient.Mark(go);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.GetComponent<SpriteRenderer>().sortingOrder = EffectSortingOrder;
            var p = go.AddComponent<VfxPlayer>();
            p.Play(frames, fps, true, 0f, Color.white);
            go.SetActive(false);
            return p;
        }

        static Sprite WhiteSprite()
        {
            if (whiteSprite == null)
                whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 128f);
            return whiteSprite;
        }
    }
}
