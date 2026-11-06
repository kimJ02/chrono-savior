using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 잔상 — 원본 `player.afterimages`(project_test.html:1494·:3502~3504·:6192~6193). 지나간 자리에 캐릭터 그림을
    /// 푸른 반투명으로 0.25초 남긴다(`alpha = 0.3 × (1 − t / 0.25)`, 색은 원본 유령 그림의 `rgba(135,190,255)` — :4609).
    ///
    /// 남기는 곳(원본): 섬영이 빠를 때(속도 비율 0.55 초과 → 프레임마다 비율 × 0.7 확률, :2876~2877), 칼날폭풍 활공 중
    /// (프레임마다 80%, :2815), 마법사 불길 이동 출발 자리(:2147 — <see cref="MageAttack"/>가 <see cref="Spawn"/>을 부른다).
    /// 2026-10-24 사용자 "섬영 기본 공격할 때 깜빡이는 효과 말고 다른 방식" → 원본에 있던 잔상(우리 게임엔 빠져 있었다).
    ///
    /// 섬영 상태는 팀원 파일(`BladeCombat`)을 안 건드리고 밖에서 읽는다 — 속도 비율은 `BladeCombat.ComputeSpeedRatio`와
    /// 같은 식(물리 속도 ÷ `inertialMaxSpeed × MoveScale`), 칼날폭풍은 "물리 속도를 비우고 위치를 직접 옮기는" 상태로 알아본다
    /// (`BladeCombat.UpdateStorm`).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAfterimages : MonoBehaviour, IRunResettable
    {
        /// <summary>원본 `if (t > 0.25) splice`.</summary>
        public const float Life = 0.25f;
        /// <summary>원본 `alpha: 0.3 * (1 - g.t / 0.25)`.</summary>
        public const float StartAlpha = 0.3f;
        /// <summary>원본 `bladeSpeedRatio() > 0.55`.</summary>
        public const float BladeRatioMin = 0.55f;
        /// <summary>원본 `Math.random() < bladeSpeedRatio() * 0.7`.</summary>
        public const float BladeChancePerRatio = 0.7f;
        /// <summary>원본 칼날폭풍 `Math.random() < 0.8`.</summary>
        public const float StormChance = 0.8f;
        /// <summary>원본 확률은 한 프레임(60fps) 기준 — 프레임이 더 짧으면 그만큼 낮춰 초당 잔상 수를 맞춘다.</summary>
        public const float OriginalFps = 60f;
        /// <summary>원본 유령 그림 색(마법사 유령 로브 `rgba(135,190,255,…)`, :4609). 몸 모양 그대로 이 색으로 칠한다.</summary>
        public static readonly Color GhostColor = new Color(135f / 255f, 190f / 255f, 1f, 1f);

        // 칼날폭풍 알아보기: 물리 속도는 비어 있는데 이만큼(유닛/초) 넘게 옆으로 움직이면 활공 중이다.
        const float StormDetectSpeed = 0.5f;
        // 이것보다 빠른 한 프레임 이동은 속도가 아니라 순간이동(런 시작 배치 등)이다 — 칼날폭풍 최고 속도는 1.4배(:2675).
        const float TeleportSpeedMult = 3f;

        struct Ghost { public SpriteRenderer sr; public float t; }
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        SpriteRenderer body;
        CharacterMover2D mover;
        Rigidbody2D rb;
        Vector3 lastPos;
        [Tooltip("효과 머티리얼(Assets/Materials/SpriteFlash — 그림의 몸 모양(알파)만 살리고 색은 꼭짓점 색으로 칠하는 셰이더). 외주 효과로 바꿀 땐 이 칸만.")]
        [SerializeField] Material ghostMaterial;

        /// <summary>씬 검증과 테스트가 읽고 넣는다(2026-11-06 3.5-e — 예전엔 `Resources`에서 셰이더를 읽었다).</summary>
        public Material GhostMaterial { get => ghostMaterial; set => ghostMaterial = value; }

        /// <summary>지금 남아 있는 잔상 수(테스트용).</summary>
        public int Count => ghosts.Count;

        /// <summary>i번째 잔상 그림(테스트용).</summary>
        public SpriteRenderer GhostRenderer(int i) => ghosts[i].sr;

        void Awake()
        {
            body = GetComponent<SpriteRenderer>();
            mover = GetComponent<CharacterMover2D>();
            rb = GetComponent<Rigidbody2D>();
            lastPos = transform.position;
        }

        void Update()
        {
            float dt = Time.deltaTime; // 히트스톱·일시정지 땐 0 — 원본처럼 잔상도 같이 멈춘다
            // 로비·타이틀로 나가면 남은 잔상을 지운다 — 시간이 멈춘 채(dt 0) 그 자리에 하얗게 굳어 로비 하늘에 줄지어 떠 있었다
            // (2026-10-25 사용자 섬영 플레이 스크린샷). 일시정지·결과 화면은 사냥 화면 위라 그대로 둔다.
            if (ghosts.Count > 0 && LeftTheField) ResetForRun();
            Age(dt);
            if (dt > 0f && GameState.Current == GameScene.Run && ProfileService.Current.character == CharacterId.Blade)
            {
                float chance = BladeChance(dt);
                if (chance > 0f && Random.value < chance) Spawn(transform.position);
            }
            lastPos = transform.position;
        }

        float BladeChance(float dt)
        {
            float max = mover != null ? mover.inertialMaxSpeed * mover.MoveScale : 0f; // bladeMaxSpeed(:2429)
            if (max <= 0f) return 0f;

            float vx = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            bool storm = false;
            if (vx < 0.01f)
            {
                float moved = Mathf.Abs(transform.position.x - lastPos.x) / dt;
                if (moved > max * TeleportSpeedMult) return 0f;
                storm = moved > StormDetectSpeed;
                vx = moved;
            }
            return ChancePerFrame(Mathf.Clamp01(vx / max), storm, dt);
        }

        /// <summary>
        /// 이번 프레임에 섬영이 잔상을 남길 확률 — 원본은 60fps 한 프레임에 `비율 × 0.7`(비율이 0.55 넘을 때만),
        /// 칼날폭풍 활공 중엔 0.8. 프레임 길이(<paramref name="dt"/>)가 다르면 초당 잔상 수가 같도록 바꾼다.
        /// </summary>
        public static float ChancePerFrame(float speedRatio, bool storm, float dt)
        {
            float p60 = storm ? StormChance : (speedRatio > BladeRatioMin ? speedRatio * BladeChancePerRatio : 0f);
            if (p60 <= 0f || dt <= 0f) return 0f;
            p60 = Mathf.Min(p60, 0.999f);
            return 1f - Mathf.Pow(1f - p60, dt * OriginalFps);
        }

        /// <summary>지금 그림 그대로 이 자리에 잔상을 하나 남긴다(원본 `p.afterimages.push({ x, y, f })`).</summary>
        public void Spawn(Vector3 position)
        {
            if (body == null || body.sprite == null || ghostMaterial == null) return; // 머티리얼은 씬엔 늘 있다(씬 구성 검증이 본다)
            var sr = pool.Count > 0 ? pool.Pop() : NewGhostRenderer();
            sr.gameObject.SetActive(true);
            sr.transform.SetPositionAndRotation(position, transform.rotation);
            sr.transform.localScale = transform.lossyScale;
            sr.sprite = body.sprite;
            sr.flipX = body.flipX;
            sr.flipY = body.flipY;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 1; // 원본은 잔상을 먼저 그리고 캐릭터를 그 위에(:6192~6196)
            sr.color = WithAlpha(StartAlpha);
            ghosts.Add(new Ghost { sr = sr, t = 0f });
        }

        void Age(float dt)
        {
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                g.t += dt;
                if (g.t > Life)
                {
                    Release(g.sr);
                    ghosts.RemoveAt(i);
                    continue;
                }
                g.sr.color = WithAlpha(StartAlpha * (1f - g.t / Life));
                ghosts[i] = g;
            }
        }

        static bool LeftTheField => GameState.Current == GameScene.Lobby || GameState.Current == GameScene.Title;

        /// <summary>원본 `resetPlayerForRun`의 `p.afterimages = []`(:1518).</summary>
        public void ResetForRun()
        {
            foreach (var g in ghosts) Release(g.sr);
            ghosts.Clear();
            lastPos = transform.position;
        }

        void OnDestroy()
        {
            // 잔상은 캐릭터를 따라 움직이면 안 돼서 부모 없이 둔다 — 캐릭터가 없어질 때 같이 지운다.
            foreach (var g in ghosts) if (g.sr != null) Destroy(g.sr.gameObject);
            foreach (var sr in pool) if (sr != null) Destroy(sr.gameObject);
            ghosts.Clear();
            pool.Clear();
        }

        void Release(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.gameObject.SetActive(false);
            pool.Push(sr);
        }

        static Color WithAlpha(float a)
        {
            Color c = GhostColor;
            c.a = a;
            return c;
        }

        // 피격 플래시와 같은 머티리얼 — 그림의 몸 모양(알파)만 살리고 색은 꼭짓점 색(유령 색)으로 칠한다.
        SpriteRenderer NewGhostRenderer()
        {
            var sr = new GameObject("Afterimage").AddComponent<SpriteRenderer>();
            sr.sharedMaterial = ghostMaterial;
            return sr;
        }
    }
}
