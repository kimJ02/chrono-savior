using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 살수(코드 이름 섬영 `Blade*`) 스킬 그림 — 그림만이다(판정·수치는 <see cref="BladeCombat"/>, 기본공격 베기는 <see cref="BladeSlashVfx"/>).
    /// **2026-11-10 사용자 확정**(지금 모습 캡처 → 다른 게임 사례(Katana Zero 네온 잔상·Megaman Zero 대시·Hollow Knight 사이클론·Hades 마무리 강조) 조사 →
    /// Codex xhigh 논의 → 도트 격자에 맞춘 움직이는 예시 그림에서 고름):
    /// - **집중 'B 잔상 복제'**: 배경만 완전히 검게 페이드(<see cref="ShadeSortingOrder"/> — 땅·발판·적·캐릭터는 밝게, 2026-11-10 사용자 지시로 남색 반투명에서 승격),
    ///   켤 때만 캐릭터 뒤 청록 하트비트 + 1px 고동 링 두 번,
    ///   이동 중 0.1/0.2/0.3초 전 자리에 파란 잔상 3개 + 짧은 속도선. 궤적(판정)은 굵은 관 대신 옅은 1px 점(판정 자리는 보여야 해서 — 예시엔 없던 것, 사용자에게 알림).
    ///   4층 파열 = 지나온 길을 따라 흰 잔상이 차례로 번쩍 + 맞은 적 자리에 파란 충격(<see cref="BladeCombat.TrailRipHit"/>).
    /// - **칼날폭풍 '잔상 연쇄 파열 + 착지 폭발'**: 판정 고리·연기는 뺐다. 회전베기 루프는 틱마다 좌우 반전 + 큰 청록 초승달이 좌우 번갈아(<see cref="BladeCombat.StormSpun"/>),
    ///   바로 뒤 잔상 3개는 틱마다 먼 쪽부터 하얗게 번쩍, 진행 방향(30° 아래) 속도선, 끌려가는 적 잔상. 지나온 길에 <see cref="ChainStep"/>초마다 잔상이 사슬로 남고,
    ///   끝나면(<see cref="BladeCombat.StormEnded"/>) <see cref="ChainHold"/>초 맥동하다 먼 쪽부터 흰 번쩍 → 청록 번개 베기로 터진다.
    ///   2층+ 착지(<see cref="BladeCombat.StormLanded"/>) = 흰 섬광 + 가시 충격 고리 + 청록 폭발 + **바닥 충격파가 착지 판정 반경(2.1)까지** + 파편(Particle System) + 먼지 + 화면 흔들림.
    /// 그림은 판정을 다시 계산하지 않는다 — 판정이 낸 결과(이벤트·공개 상태)만 그린다(Codex). 시간은 게임 시간(일시정지·히트스톱 땐 같이 멈춘다).
    /// 잔상은 몸 모양만 살려 색으로 칠하는 머티리얼(<see cref="ghostMaterial"/>, SpriteFlash), 충격파는 선 머티리얼(<see cref="lineMaterial"/>), 파편은 프리팹(<see cref="debrisPrefab"/>) — 씬 Player 칸.
    /// 라이선스 그림이 없으면(팩을 안 가져온 클론) 그 부분만 안 보인다.
    /// </summary>
    [RequireComponent(typeof(BladeCombat))]
    public class BladeVfx : MonoBehaviour
    {
        // ── 집중 ──
        /// <summary>배경을 덮는 정렬(배경 -100 위, 땅·발판 -5 아래).</summary>
        public const int ShadeSortingOrder = -50;
        /// <summary>
        /// 배경 어둡기·색 — 2026-11-10 사용자 지시로 남색 반투명(0.34) 대신 **완전 검은 바탕**으로 올림
        /// (페이드아웃으로 검게, 집중이 끝날 때 즘에 다시 페이드인). 전환 시간은 그대로 <see cref="ShadeFadeTime"/>이
        /// 맡는다(아래 <see cref="UpdateFocus"/>의 MoveTowards가 focusing 여부만 보고 자동으로 켜고/끈다).
        /// </summary>
        public const float ShadeAlpha = 0.97f, ShadeFadeTime = 0.2f;
        static readonly Color ShadeColor = Color.black;
        const float ShadeSize = 40f;
        /// <summary>하트비트 — 켤 때만 이만큼(초) 캐릭터 뒤에서, 고동 링은 0초·0.25초 두 번.</summary>
        public const float HeartbeatTime = 0.55f, HeartbeatFps = 20f, PulseLife = 0.22f;
        /// <summary>집중 잔상 — 몇 초 전 자리, 색.</summary>
        static readonly float[] FocusGhostBack = { 0.1f, 0.2f, 0.3f };
        static readonly Color[] FocusGhostColor = { new Color(100 / 255f, 200 / 255f, 1f, 150 / 255f), new Color(100 / 255f, 200 / 255f, 1f, 105 / 255f), new Color(100 / 255f, 200 / 255f, 1f, 60 / 255f) };
        /// <summary>이보다 느리면(0.3초 동안 이만큼도 안 움직이면) 잔상·속도선을 안 그린다(유닛).</summary>
        const float GhostMinTravel = 0.35f;
        static readonly Color TrailDotColor = new Color(120 / 255f, 225 / 255f, 1f, 0.35f);
        /// <summary>파열 — 흰 잔상 간격(유닛)·차례 간격(초)·수명(초).</summary>
        public const float RipGhostSpacing = 0.7f, RipGhostStagger = 0.035f, RipGhostLife = 0.2f;

        // ── 칼날폭풍 ──
        public const float SpinFps = 20f, CrescentFps = 30f, BoltFps = 26f;
        /// <summary>바로 뒤 잔상 — 몇 초 전 자리(2·4·6프레임), 색(밝은 청록·파랑·어두운 파랑).</summary>
        static readonly float[] StormGhostBack = { 2 / 30f, 4 / 30f, 6 / 30f };
        static readonly Color C_W = new Color(244 / 255f, 1f, 1f), C_LC = new Color(84 / 255f, 233 / 255f, 1f), C_C = new Color(34 / 255f, 200 / 255f, 1f),
            C_B = new Color(63 / 255f, 131 / 255f, 1f), C_D = new Color(23 / 255f, 77 / 255f, 138 / 255f);
        /// <summary>잔상 사슬 간격(초)·끝난 뒤 남아 있는 시간(초)·먼 쪽부터 터지는 간격(초)·흰 번쩍 시간(초).</summary>
        public const float ChainStep = 0.05f, ChainHold = 0.35f, ChainStagger = 0.03f, ChainWhite = 2 / 30f;
        /// <summary>초승달이 몸 중심에서 좌우로 비켜나는 거리(그림 px).</summary>
        const float CrescentOffsetPx = 10f;
        /// <summary>속도선 — 칼날폭풍 6줄·집중 3줄, 그림 px 길이 후보, 2프레임마다 다시 뽑는다.</summary>
        const int StormSpeedLines = 6, FocusSpeedLines = 3;
        static readonly int[] SpeedLineLengths = { 18, 28, 40 };
        const float SpeedLineRefresh = 2 / 30f;
        /// <summary>착지 — 흰 섬광 시간, 충격파가 판정 반경까지 퍼지는 시간·사라지는 시간, 흔들림(원본 px)·시간.</summary>
        public const float LandFlashTime = 2 / 30f, ShockGrowTime = 0.22f, ShockLife = 0.3f, LandShakePx = 12f, LandShakeTime = 0.16f, ChainShakePx = 5f;
        public const int EffectSortingOrder = 4, LineSortingOrder = 3, FlashSortingOrder = 5;

        [Tooltip("착지 충격파 선 머티리얼(Assets/Materials/VfxUnlit). 외주 효과로 바꿀 땐 이 칸만.")]
        [SerializeField] Material lineMaterial;
        [Tooltip("잔상 머티리얼(Assets/Materials/SpriteFlash — 몸 모양만 살리고 색으로 칠한다).")]
        [SerializeField] Material ghostMaterial;
        [Tooltip("착지 파편 Particle System 프리팹(Assets/Prefabs/Combat/BladeLandDebris).")]
        [SerializeField] ParticleSystem debrisPrefab;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public Material LineMaterial { get => lineMaterial; set => lineMaterial = value; }
        public Material GhostMaterial { get => ghostMaterial; set => ghostMaterial = value; }
        public ParticleSystem DebrisPrefab { get => debrisPrefab; set => debrisPrefab = value; }

        BladeCombat blade;
        SpriteRenderer body;
        float clock; // 게임 시간
        // 위치 기록(잔상용)
        struct Sample { public float t; public Vector2 pos; public Sprite sprite; public bool flip; }
        readonly List<Sample> history = new List<Sample>();

        // 집중
        SpriteRenderer shade;
        float shadeAlpha;
        VfxPlayer heartbeat;
        float heartbeatT = -1f;
        bool wasFocusing;
        readonly SpriteRenderer[] closeGhosts = new SpriteRenderer[3];
        readonly List<SpriteRenderer> trailDots = new List<SpriteRenderer>();
        struct Pulse { public SpriteRenderer sr; public float t; public Vector2 at; }
        readonly List<Pulse> pulses = new List<Pulse>();
        struct Flash { public SpriteRenderer sr; public float t, delay, life; public Vector2 at; public Sprite sprite; public bool flip; public Color color; }
        readonly List<Flash> flashes = new List<Flash>();

        // 칼날폭풍
        VfxPlayer spin;
        bool wasStorming;
        float closeFlashT = 9f, chainNext, chainEndT = -1f, speedLineT;
        int lastTick;
        struct Link { public SpriteRenderer sr; public float born; public Vector2 at; public Sprite sprite; public bool flip; public bool done; }
        readonly List<Link> chain = new List<Link>();
        readonly List<SpriteRenderer> speedLines = new List<SpriteRenderer>();
        float enemyGhostT;
        LineRenderer shock;
        SpriteRenderer landFlash;
        float landT = -1f;
        Vector2 landAt;
        ParticleSystem debris;
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        /// <summary>테스트용.</summary>
        public float ShadeAmount => shadeAlpha;
        public bool HeartbeatVisible => heartbeat != null && heartbeat.gameObject.activeSelf;
        public bool SpinVisible => spin != null && spin.gameObject.activeSelf;
        public int TrailPointCount { get { int n = 0; foreach (var d in trailDots) if (d != null && d.gameObject.activeSelf) n++; return n; } }
        public int CloseGhostCount { get { int n = 0; foreach (var g in closeGhosts) if (g != null && g.gameObject.activeSelf) n++; return n; } }
        public int ChainCount { get { int n = 0; foreach (var l in chain) if (!l.done) n++; return n; } }
        public bool ShockVisible => shock != null && shock.enabled;
        public float ShockRadius { get; private set; }

        void Awake()
        {
            blade = GetComponent<BladeCombat>();
            body = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            if (blade == null) blade = GetComponent<BladeCombat>();
            if (blade == null) return;
            blade.TrailRipped += OnTrailRipped;
            blade.TrailRipHit += OnTrailRipHit;
            blade.StormSpun += OnStormSpun;
            blade.StormLanded += OnStormLanded;
            blade.StormEnded += OnStormEnded;
        }

        void OnDisable()
        {
            if (blade != null)
            {
                blade.TrailRipped -= OnTrailRipped;
                blade.TrailRipHit -= OnTrailRipHit;
                blade.StormSpun -= OnStormSpun;
                blade.StormLanded -= OnStormLanded;
                blade.StormEnded -= OnStormEnded;
            }
            HideAll();
        }

        void LateUpdate() => Refresh(Time.deltaTime); // BladeCombat.Update 뒤에 — 일시정지·히트스톱 땐 dt 0

        /// <summary>그림을 지금 상태에 맞춘다(테스트가 직접 부른다).</summary>
        public void Refresh(float dt)
        {
            if (blade == null) blade = GetComponent<BladeCombat>();
            if (body == null) body = GetComponent<SpriteRenderer>();
            bool active = blade != null && blade.enabled && GameState.IsRunning;
            if (!active) { HideAll(); return; }
            clock += dt;
            Record();
            bool focusing = blade.IsFocusing, storming = blade.IsStorming;
            UpdateFocus(focusing, dt);
            UpdateTrailDots();
            UpdateCloseGhosts(focusing, storming);
            UpdateSpeedLines(focusing, storming, dt);
            UpdateStorm(storming, dt);
            UpdateChain();
            UpdateLanding(dt);
            UpdateFlashes(dt);
            UpdatePulses(dt);
        }

        // ───────── 공통: 위치 기록·잔상 ─────────
        void Record()
        {
            history.Add(new Sample { t = clock, pos = transform.position, sprite = body != null ? body.sprite : null, flip = body != null && body.flipX });
            while (history.Count > 2 && clock - history[1].t > 0.45f) history.RemoveAt(0);
        }

        bool SampleBack(float back, out Sample s)
        {
            s = default;
            if (history.Count == 0) return false;
            float want = clock - back;
            for (int i = history.Count - 1; i >= 0; i--)
                if (history[i].t <= want) { s = history[i]; return true; }
            return false;
        }

        SpriteRenderer Take(string name, int order)
        {
            SpriteRenderer sr = null;
            while (pool.Count > 0 && sr == null) sr = pool.Pop();
            if (sr == null)
            {
                var go = new GameObject(name, typeof(SpriteRenderer));
                RunTransient.Mark(go);
                sr = go.GetComponent<SpriteRenderer>();
            }
            sr.gameObject.name = name;
            sr.gameObject.SetActive(true);
            sr.sortingOrder = order;
            sr.flipX = false;
            sr.transform.rotation = Quaternion.identity;
            return sr;
        }

        void Give(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.gameObject.SetActive(false);
            pool.Push(sr);
        }

        /// <summary>몸 모양 잔상 하나를 그 자리·그 그림·그 색으로(머티리얼이 없으면 그냥 색을 곱한다).</summary>
        void PlaceGhost(SpriteRenderer sr, Vector2 at, Sprite sprite, bool flip, Color color)
        {
            sr.sprite = sprite;
            sr.flipX = flip;
            if (ghostMaterial != null) sr.sharedMaterial = ghostMaterial;
            sr.color = color;
            sr.transform.position = at;
            sr.transform.localScale = transform.lossyScale;
            if (body != null) { sr.sortingLayerID = body.sortingLayerID; sr.sortingOrder = body.sortingOrder - 1; }
        }

        // ───────── 집중 ─────────
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
                    shade.sprite = VfxDots.DotSprite();
                    shade.sortingOrder = ShadeSortingOrder;
                    go.transform.localScale = new Vector3(ShadeSize / shade.sprite.bounds.size.x, ShadeSize / shade.sprite.bounds.size.y, 1f);
                }
                shade.gameObject.SetActive(true);
                shade.transform.position = transform.position;
                shade.color = new Color(ShadeColor.r, ShadeColor.g, ShadeColor.b, shadeAlpha);
            }
            else if (shade != null) shade.gameObject.SetActive(false);

            // 켤 때만 — 캐릭터 뒤 하트비트 + 고동 링 두 번
            if (focusing && !wasFocusing)
            {
                heartbeatT = 0f;
                AddPulse(0f); AddPulse(-0.25f);
            }
            wasFocusing = focusing;
            if (heartbeatT >= 0f)
            {
                heartbeatT += dt;
                if (heartbeat == null) heartbeat = NewLoop("FocusHeartbeat", VfxKeys.AssassinHeartbeat, HeartbeatFps, DotScale.Value);
                if (heartbeat != null)
                {
                    bool on = heartbeatT < HeartbeatTime && focusing;
                    if (on && !heartbeat.gameObject.activeSelf) { heartbeat.gameObject.SetActive(true); heartbeat.Play(VfxFrames.Get(VfxKeys.AssassinHeartbeat), HeartbeatFps, true, 0f, Color.white); }
                    if (!on) heartbeat.gameObject.SetActive(false);
                    heartbeat.transform.position = transform.position;
                    var hsr = heartbeat.GetComponent<SpriteRenderer>();
                    if (body != null) hsr.sortingOrder = body.sortingOrder - 1;
                }
                if (heartbeatT >= HeartbeatTime) heartbeatT = -1f;
            }
            else if (heartbeat != null) heartbeat.gameObject.SetActive(false);
        }

        void AddPulse(float startT) => pulses.Add(new Pulse { sr = null, t = startT, at = transform.position });

        void UpdatePulses(float dt)
        {
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                var p = pulses[i];
                p.t += dt;
                if (p.t >= PulseLife) { Give(p.sr); pulses.RemoveAt(i); continue; }
                if (p.t >= 0f)
                {
                    if (p.sr == null) p.sr = Take("FocusPulse", EffectSortingOrder);
                    float r = DotScale.Units(8f + p.t * 90f) / 2f * 2f; // 그림 8px에서 0.22초에 ~28px
                    p.sr.sprite = DotShapes.Ring(Mathf.Round(r / DotScale.Units(2f)) * DotScale.Units(2f)); // 2px 단위로(캐시 크기 제한)
                    p.sr.sharedMaterial = lineMaterial;
                    p.sr.transform.localScale = Vector3.one;
                    p.sr.transform.position = transform.position;
                    p.sr.color = new Color(C_LC.r, C_LC.g, C_LC.b, 0.9f * (1f - p.t / PulseLife));
                }
                pulses[i] = p;
            }
        }

        /// <summary>궤적(판정 점) — 옅은 1px 점만. 판정 자리는 보여야 해서 남겼다(예시엔 없던 것).</summary>
        void UpdateTrailDots()
        {
            var pts = blade.TrailPoints;
            int n = pts != null ? pts.Count : 0;
            trailDots.RemoveAll(d => d == null); // 런이 끝나 치워진 것(RunTransient) — 다음 런에 다시 만든다(Codex 리뷰)
            while (trailDots.Count < n)
            {
                var go = new GameObject("TrailDot", typeof(SpriteRenderer));
                RunTransient.Mark(go);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = VfxDots.DotSprite();
                sr.sortingOrder = LineSortingOrder;
                go.transform.localScale = new Vector3(0.5f, 0.5f, 1f); // 그림 1px
                trailDots.Add(sr);
            }
            float fade = blade.TrailLive ? 1f : 0.6f;
            for (int i = 0; i < trailDots.Count; i++)
            {
                var d = trailDots[i];
                if (d == null) continue;
                bool on = i < n;
                if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
                if (!on) continue;
                d.transform.position = GunnerFieldVfx.Snap(pts[i]);
                d.color = new Color(TrailDotColor.r, TrailDotColor.g, TrailDotColor.b, TrailDotColor.a * fade);
            }
        }

        void OnTrailRipped(IReadOnlyList<Vector2> pts)
        {
            if (pts == null || pts.Count == 0) return;
            // 지나온 길을 따라 흰 잔상이 차례로 번쩍(먼 쪽 = 궤적 시작부터)
            float carry = RipGhostSpacing;
            int k = 0;
            Sprite sprite = body != null ? body.sprite : null;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) carry += Vector2.Distance(pts[i - 1], pts[i]);
                if (carry < RipGhostSpacing) continue;
                carry = 0f;
                bool flip = i > 0 ? pts[i].x < pts[i - 1].x : (body != null && body.flipX);
                flashes.Add(new Flash { delay = k * RipGhostStagger, life = RipGhostLife, at = pts[i], sprite = sprite, flip = flip, color = C_W });
                k++;
            }
        }

        void OnTrailRipHit(Vector2 at) => VfxPlayer.Spawn(VfxKeys.AssassinRipHit, at, DotScale.Value, 30f, EffectSortingOrder);

        void UpdateFlashes(float dt)
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                f.t += dt;
                float lt = f.t - f.delay;
                if (lt >= f.life) { Give(f.sr); flashes.RemoveAt(i); continue; }
                if (lt >= 0f && f.sprite != null)
                {
                    if (f.sr == null) f.sr = Take("RipGhost", 0);
                    PlaceGhost(f.sr, f.at, f.sprite, f.flip, new Color(f.color.r, f.color.g, f.color.b, 0.86f * (1f - lt / f.life)));
                }
                else if (lt >= 0f && f.sr != null) // 이미 놓인 잔상(끌려가는 적) — 옅어지기만
                    f.sr.color = new Color(f.color.r, f.color.g, f.color.b, 0.43f * (1f - lt / f.life));
                flashes[i] = f;
            }
        }

        // ───────── 잔상 3개(집중·칼날폭풍 공통) ─────────
        void UpdateCloseGhosts(bool focusing, bool storming)
        {
            bool show = (focusing || storming) && SampleBack(0.3f, out var far) && Vector2.Distance(far.pos, transform.position) > GhostMinTravel;
            if (storming && !show) show = true;
            float[] backs = storming ? StormGhostBack : FocusGhostBack;
            for (int j = 0; j < 3; j++)
            {
                if (!show || !SampleBack(backs[j], out var s) || s.sprite == null)
                {
                    if (closeGhosts[j] != null) closeGhosts[j].gameObject.SetActive(false);
                    continue;
                }
                if (closeGhosts[j] == null) closeGhosts[j] = Take("BladeGhost", 0);
                closeGhosts[j].gameObject.SetActive(true);
                Color col;
                if (storming)
                {
                    col = j == 0 ? new Color(C_LC.r, C_LC.g, C_LC.b, 200 / 255f) : j == 1 ? new Color(C_B.r, C_B.g, C_B.b, 150 / 255f) : new Color(C_D.r, C_D.g, C_D.b, 110 / 255f);
                    // 틱마다 먼 쪽부터 하얗게(3프레임)
                    int frame = Mathf.FloorToInt(closeFlashT * 30f);
                    if (frame >= 0 && frame < 3 && frame == 2 - j) col = C_W;
                }
                else col = FocusGhostColor[j];
                PlaceGhost(closeGhosts[j], s.pos, s.sprite, s.flip, col);
            }
        }

        // ───────── 속도선 ─────────
        void UpdateSpeedLines(bool focusing, bool storming, float dt)
        {
            int want = 0;
            Vector2 dir = Vector2.right;
            if (storming) { want = StormSpeedLines; dir = blade.StormDirection; }
            else if (focusing && SampleBack(0.1f, out var s) && Vector2.Distance(s.pos, transform.position) > GhostMinTravel * 0.33f)
            {
                want = FocusSpeedLines; dir = ((Vector2)transform.position - s.pos).normalized;
            }
            speedLineT -= dt;
            bool reroll = speedLineT <= 0f;
            if (reroll) speedLineT = SpeedLineRefresh;
            speedLines.RemoveAll(l => l == null); // 런이 끝나 치워진 것
            while (speedLines.Count < want) { var sr = Take("SpeedLine", LineSortingOrder); sr.sprite = VfxDots.DotSprite(); speedLines.Add(sr); }
            float px = DotScale.Units(1f);
            for (int i = 0; i < speedLines.Count; i++)
            {
                var sr = speedLines[i];
                if (sr == null) continue;
                bool on = i < want;
                if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
                if (!on || !reroll) continue;
                // 몸 뒤쪽(진행 반대)에서, 진행 방향으로 기운 1px 선
                int len = SpeedLineLengths[Random.Range(0, SpeedLineLengths.Length)];
                Vector2 n = new Vector2(-dir.y, dir.x);
                Vector2 center = (Vector2)transform.position - dir * (px * (16 + Random.Range(0, 24) + len * 0.5f)) + n * (px * Random.Range(-22, 17));
                sr.transform.position = GunnerFieldVfx.Snap(center);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                float unit = sr.sprite.bounds.size.x;
                sr.transform.localScale = new Vector3(len * px / unit, px / unit, 1f);
                sr.sharedMaterial = lineMaterial;
                sr.color = new Color(C_W.r, C_W.g, C_W.b, storming ? 0.82f : 0.63f);
            }
        }

        // ───────── 칼날폭풍 ─────────
        void UpdateStorm(bool storming, float dt)
        {
            closeFlashT += dt;
            if (storming)
            {
                if (!wasStorming)
                {
                    ClearChain(); // 이전 사슬이 아직 남아 있으면 바로 정리(재진입)
                    chainNext = clock;
                    chainEndT = -1f;
                }
                if (spin == null) spin = NewLoop("StormSpin", VfxKeys.AssassinSpin, SpinFps, DotScale.Value);
                if (spin != null)
                {
                    if (!spin.gameObject.activeSelf) { spin.gameObject.SetActive(true); spin.Play(VfxFrames.Get(VfxKeys.AssassinSpin), SpinFps, true, 0f, Color.white); }
                    spin.transform.position = transform.position;
                    spin.GetComponent<SpriteRenderer>().flipX = lastTick % 2 == 1;
                }
                // 지나온 길에 잔상 사슬
                if (clock >= chainNext && body != null && body.sprite != null)
                {
                    chainNext = clock + ChainStep;
                    chain.Add(new Link { sr = null, born = clock, at = transform.position, sprite = body.sprite, flip = body.flipX });
                }
                // 끌려가는 적 잔상(2프레임마다, 짧게)
                enemyGhostT -= dt;
                if (enemyGhostT <= 0f)
                {
                    enemyGhostT = 2 / 30f;
                    foreach (var c in blade.StormGrabbed)
                    {
                        var esr = c.GetComponent<SpriteRenderer>();
                        if (esr == null || esr.sprite == null) continue;
                        var g = Take("GrabGhost", esr.sortingOrder - 1);
                        g.sprite = esr.sprite; g.flipX = esr.flipX;
                        if (ghostMaterial != null) g.sharedMaterial = ghostMaterial;
                        g.sortingLayerID = esr.sortingLayerID;
                        g.transform.position = esr.transform.position;
                        g.transform.localScale = esr.transform.lossyScale;
                        g.color = new Color(C_C.r, C_C.g, C_C.b, 110 / 255f);
                        flashes.Add(new Flash { sr = g, delay = 0f, life = 4 / 30f, at = esr.transform.position, sprite = null, color = C_C });
                    }
                }
            }
            else if (spin != null) spin.gameObject.SetActive(false);
            wasStorming = storming;
        }

        void OnStormSpun(Vector2 at, int tick)
        {
            lastTick = tick;
            closeFlashT = 0f;
            bool right = tick % 2 == 0;
            var p = VfxPlayer.Spawn(VfxKeys.AssassinCrescent, at + new Vector2(DotScale.Units(right ? CrescentOffsetPx : -CrescentOffsetPx), 0f), DotScale.Value, CrescentFps, EffectSortingOrder);
            if (p != null) p.GetComponent<SpriteRenderer>().flipX = !right;
        }

        void OnStormEnded() => chainEndT = clock;

        void UpdateChain()
        {
            for (int i = 0; i < chain.Count; i++)
            {
                var l = chain[i];
                if (l.done) continue;
                float age = clock - l.born;
                if (l.sr == null) l.sr = Take("ChainGhost", 0);
                Color col = age < 0.1f ? new Color(C_LC.r, C_LC.g, C_LC.b, 200 / 255f) : age < 0.25f ? new Color(C_B.r, C_B.g, C_B.b, 150 / 255f) : new Color(C_D.r, C_D.g, C_D.b, 110 / 255f);
                if (chainEndT >= 0f)
                {
                    float sinceEnd = clock - chainEndT;
                    float boom = sinceEnd - ChainHold - i * ChainStagger;
                    if (boom < 0f)
                    {
                        if (Mathf.FloorToInt(sinceEnd * 30f) % 6 < 3) col = new Color(C_B.r, C_B.g, C_B.b, 150 / 255f); // 남아 있는 동안 맥동
                    }
                    else if (boom < ChainWhite) col = C_W;
                    else
                    {
                        // 터진다 — 청록 번개 베기 + 파란 반짝임
                        VfxPlayer.Spawn(i % 2 == 0 ? VfxKeys.AssassinBolt2 : VfxKeys.AssassinBolt1, l.at, DotScale.Value, BoltFps, EffectSortingOrder);
                        VfxPlayer.Spawn(VfxKeys.AssassinBurstSpark, l.at, DotScale.Value, 30f, EffectSortingOrder);
                        if (i == 0) HitFeel.Shake(ChainShakePx, 0.08f);
                        Give(l.sr); l.sr = null; l.done = true;
                        chain[i] = l;
                        continue;
                    }
                }
                PlaceGhost(l.sr, l.at, l.sprite, l.flip, col);
                chain[i] = l;
            }
            if (chain.Count > 0 && chain.TrueForAll(l => l.done)) chain.Clear();
        }

        void ClearChain()
        {
            foreach (var l in chain) Give(l.sr);
            chain.Clear();
            chainEndT = -1f;
        }

        // ───────── 착지 폭발 ─────────
        void OnStormLanded(Vector2 feet)
        {
            landT = 0f;
            landAt = feet;
            VfxPlayer.Spawn(VfxKeys.AssassinLandRing, feet + new Vector2(0f, DotScale.Units(14f)), DotScale.Value, 26f, EffectSortingOrder);
            VfxPlayer.Spawn(VfxKeys.AssassinLandBurst, feet + new Vector2(0f, DotScale.Units(10f)), DotScale.Value, 26f, EffectSortingOrder);
            var d1 = VfxPlayer.Spawn(VfxKeys.AssassinLandDust, feet + new Vector2(-DotScale.Units(18f), DotScale.Units(4f)), DotScale.Value, 30f, EffectSortingOrder);
            var d2 = VfxPlayer.Spawn(VfxKeys.AssassinLandDust, feet + new Vector2(DotScale.Units(18f), DotScale.Units(4f)), DotScale.Value, 30f, EffectSortingOrder);
            if (d1 != null) d1.GetComponent<SpriteRenderer>().flipX = true;
            if (debrisPrefab != null)
            {
                if (debris == null) { debris = Instantiate(debrisPrefab); RunTransient.Mark(debris.gameObject); }
                debris.transform.position = feet + new Vector2(0f, DotScale.Units(4f));
                debris.Play(true);
            }
            HitFeel.Shake(LandShakePx, LandShakeTime);
        }

        void UpdateLanding(float dt)
        {
            if (landT < 0f) return;
            landT += dt;
            // 흰 섬광(착지점 주변만)
            if (landFlash == null) { landFlash = Take("LandFlash", FlashSortingOrder); }
            landFlash.gameObject.SetActive(landT < LandFlashTime);
            if (landT < LandFlashTime)
            {
                landFlash.sprite = DotShapes.Ellipse(DotScale.Units(22f), DotScale.Units(15f));
                landFlash.sharedMaterial = lineMaterial;
                landFlash.transform.localScale = Vector3.one;
                landFlash.transform.position = landAt + new Vector2(0f, DotScale.Units(8f));
                landFlash.color = new Color(C_W.r, C_W.g, C_W.b, 0.85f);
            }
            // 바닥 충격파 — 착지 판정 반경까지 퍼지는 납작한 고리
            if (lineMaterial != null)
            {
                if (shock == null)
                {
                    var go = new GameObject("LandShock", typeof(LineRenderer));
                    RunTransient.Mark(go);
                    shock = go.GetComponent<LineRenderer>();
                    shock.sharedMaterial = lineMaterial;
                    shock.useWorldSpace = true;
                    shock.loop = true;
                    shock.positionCount = 64;
                    shock.widthMultiplier = DotScale.Units(1f);
                    shock.numCapVertices = shock.numCornerVertices = 0;
                    shock.alignment = LineAlignment.TransformZ;
                    shock.sortingOrder = LineSortingOrder;
                }
                bool on = landT < ShockLife;
                shock.enabled = on;
                if (on)
                {
                    float k = Mathf.Clamp01(landT / ShockGrowTime);
                    float rx = Mathf.Lerp(DotScale.Units(10f), BladeCombat.StormLandRadiusUnits, 1f - (1f - k) * (1f - k));
                    float ry = rx * 0.12f;
                    ShockRadius = rx;
                    for (int i = 0; i < 64; i++)
                    {
                        float a = i / 64f * Mathf.PI * 2f;
                        shock.SetPosition(i, GunnerFieldVfx.Snap(landAt + new Vector2(Mathf.Cos(a) * rx, -DotScale.Units(1f) + Mathf.Sin(a) * ry)));
                    }
                    float fade = 1f - landT / ShockLife;
                    shock.startColor = shock.endColor = new Color(C_LC.r, C_LC.g, C_LC.b, fade);
                }
            }
            if (landT >= Mathf.Max(ShockLife, LandFlashTime)) landT = -1f;
        }

        void HideAll()
        {
            shadeAlpha = 0f;
            if (shade != null) shade.gameObject.SetActive(false);
            if (heartbeat != null) heartbeat.gameObject.SetActive(false);
            heartbeatT = -1f;
            if (spin != null) spin.gameObject.SetActive(false);
            foreach (var g in closeGhosts) if (g != null) g.gameObject.SetActive(false);
            foreach (var d in trailDots) if (d != null) d.gameObject.SetActive(false);
            foreach (var s in speedLines) if (s != null) s.gameObject.SetActive(false);
            foreach (var p in pulses) Give(p.sr);
            pulses.Clear();
            foreach (var f in flashes) Give(f.sr);
            flashes.Clear();
            ClearChain();
            if (shock != null) shock.enabled = false;
            if (landFlash != null) landFlash.gameObject.SetActive(false);
            landT = -1f;
            history.Clear();
            wasFocusing = wasStorming = false;
        }

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
    }
}
