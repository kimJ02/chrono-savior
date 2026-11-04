using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.World
{
    /// <summary>
    /// 일반 필드 우측 끝에 서 있는 **보스 필드로 통하는 포탈.**
    ///
    /// ## 원본과 다른 부분 — 사용자 지시(2026-09-16)
    /// 원본은 로비에서 `[지역]`과 `[👹보스]` 버튼을 나란히 두고 보스전을 **별도의 런**으로 시작한다
    /// (project_test.html:6464 · `startRun(region, 'boss')` :4293). 우리는 그 선택을 없애고
    /// **항상 일반 필드로 입장한 뒤, 필드 안의 이 포탈로 보스 필드에 들어간다.**
    ///
    /// 바꾼 이유는 두 가지다.
    ///   1. 지역마다 버튼이 두 개씩 늘어나면 스테이지가 늘어날 때 로비가 버튼 벽이 된다.
    ///   2. "일반 몹을 밀어내다가 보스로 향하는 문이 열린다"는 흐름이 로비에서 모드를 고르는 것보다
    ///      게임 안에서 읽힌다.
    ///
    /// ## 잠금 조건
    /// <see cref="PlayerProfile.IsBossUnlocked"/> — 그 지역 **누적** 토벌 100마리
    /// (`RunState.RegionKillTarget`). 누적이므로 여러 런에 걸쳐 채워도 되고, 한 번 열리면
    /// 영구히 열려 있다. 상태를 매 프레임 다시 읽으므로 **사냥 중에 100번째를 잡으면 그 자리에서
    /// 열린다** — 로비로 나갔다 올 필요가 없다.
    ///
    /// ## 입장 방식
    /// 겹친 상태에서 **F키**(<see cref="GameInput.InteractDown"/>). 닿기만 해도 들어가는 방식은
    /// 100마리를 채운 뒤 계속 사냥하려는데 밀려서 들어가는 사고가 나고, ↑키는 마법사 중력 계열이
    /// "위로 발사"에 이미 쓰고 있어 충돌한다(사용자 확인 사항).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossPortal : MonoBehaviour
    {
        [Tooltip("이 반지름 안에 플레이어가 들어오면 입장할 수 있다(월드 유닛).")]
        public float interactRadius = 0.9f;

        [Tooltip("잠겨 있을 때 색. 해금되면 아래 색으로 바뀐다.")]
        public Color lockedColor = new Color(0.32f, 0.30f, 0.38f, 0.75f);
        [Tooltip("해금됐을 때 색. 원본 보스 색(#c86aff) 계열.")]
        public Color unlockedColor = new Color(0.78f, 0.42f, 1f);

        /// <summary>해금된 포탈이 숨을 쉬듯 밝아지는 주기(초).</summary>
        const float PulsePeriod = 1.6f;

        [Tooltip("같은 씬(20_Combat)의 플레이어 — 가까이 왔는지 본다.")]
        [SerializeField] Transform player;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다(2026-11-05 2b — 예전엔 Player 태그로 찾았다).</summary>
        public Transform Player { get => player; set => player = value; }

        SpriteRenderer sr;
        float age;

        /// <summary>지금 이 포탈로 들어갈 수 있는지. HUD가 안내를 띄울지 결정할 때 읽는다.</summary>
        public bool IsUnlocked =>
            ProfileService.Current.IsBossUnlocked(RunState.Region);

        /// <summary>플레이어가 상호작용 범위 안에 있는지.</summary>
        public bool PlayerInRange { get; private set; }

        /// <summary>
        /// 지금 화면에 있어야 하는지 — **일반 필드에서 사냥 중일 때만**이다.
        /// 보스 필드로 넘어간 뒤에도 포탈이 남아 있으면 같은 포탈을 또 밟을 수 있다.
        /// </summary>
        public bool ShouldBeVisible =>
            GameState.Current == GameScene.Run && RunState.Mode == RunMode.Normal && !RunState.Over;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        void Update()
        {
            bool visible = ShouldBeVisible;
            if (sr != null && sr.enabled != visible) sr.enabled = visible;

            if (!visible)
            {
                PlayerInRange = false;
                return;
            }

            age += Time.deltaTime;
            UpdateTint();

            PlayerInRange = IsPlayerInRange();
            if (PlayerInRange && IsUnlocked && GameInput.InteractDown)
                RunEvents.RequestBossPortal();
        }

        void UpdateTint()
        {
            if (sr == null) return;

            if (!IsUnlocked)
            {
                sr.color = lockedColor;
                return;
            }

            // 해금된 포탈은 밝기가 오르내린다 — 가만히 있는 회색 덩어리는 "들어갈 수 있다"는
            // 신호가 안 된다. 색 자체는 그대로 두고 밝기만 흔들어 잠김 상태와 확실히 구분한다.
            float t = 0.78f + 0.22f * Mathf.Sin(age * (Mathf.PI * 2f / PulsePeriod));
            sr.color = new Color(unlockedColor.r * t, unlockedColor.g * t, unlockedColor.b * t, 1f);
        }

        /// <summary>
        /// Figma 포탈 그림으로 바꾼다(2026-10-24, `StageVisuals`가 부른다). <see cref="ArtScale"/>배로 땅 위에 세운다.
        /// 입장 범위(<see cref="interactRadius"/>)는 그대로다.
        /// 색은 그림 색 그대로 두고 밝기로만 상태를 보인다 — 예전 흰 원에 쓰던 보라색을 곱하면 파란 소용돌이가 탁해진다.
        /// </summary>
        public void ApplyArt(Sprite art)
        {
            if (art == null) return;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.sprite = art;
            transform.localScale = new Vector3(ArtScale, ArtScale, 1f);
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, FieldBounds.GroundY + art.bounds.size.y * ArtScale / 2f, p.z);
            lockedColor = LockedArtColor;
            unlockedColor = Color.white; // 실제 색은 다음 Update의 UpdateTint가 상태에 맞춰 칠한다
        }

        /// <summary>
        /// 포탈 그림 배율 — **1.5배(48×96px, 사용자 결정 2026-10-24)**. Figma는 1배(캐릭터와 키가 같다)인데 작아서 "좀만 키우자".
        /// 정수배가 아니라 확대해 보면 픽셀 크기가 들쭉날쭉하다(1·1.5·2배 비교 캡처를 보고 고름 — 2배는 선명하지만 크다).
        /// </summary>
        public const float ArtScale = 1.5f;

        /// <summary>Figma 그림을 입혔을 때 잠긴 색 — 어둡고 살짝 비친다.</summary>
        public static readonly Color LockedArtColor = new Color(0.42f, 0.42f, 0.48f, 0.8f);

        bool IsPlayerInRange()
        {
            if (player == null || !player.gameObject.activeInHierarchy) return false;

            // 세로로 긴 문이라 Y는 넉넉히 본다 — 발판에서 뛰어내리며 스쳐도 잡히게.
            Vector2 d = (Vector2)player.position - (Vector2)transform.position;
            return Mathf.Abs(d.x) <= interactRadius && Mathf.Abs(d.y) <= interactRadius * 2f;
        }
    }
}
