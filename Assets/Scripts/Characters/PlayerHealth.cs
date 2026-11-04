using System;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 플레이어 체력·무적시간·피격 반동. 원본 `damagePlayer()`(project_test.html:1879)를 옮긴 것이다.
    ///
    /// **적 쪽(`EnemyHealth`)과 비대칭인 점**: 원본은 피해 ±10% 난수를 적에게는 공격자 쪽(`dealDamage`)에서,
    /// 플레이어에게는 피격자 쪽(`damagePlayer` :1904)에서 적용한다. 그 구조를 그대로 따랐다 —
    /// 그래서 여기서 `amount`를 받은 뒤 다시 난수를 굴린다(적을 때릴 때처럼 공격자가 미리 굴려 오지 않는다).
    ///
    /// 아직 빠진 것(`docs/original-parity.md` 참고): 아이템 관련 전부(피해감소 `drMult`, 피격무효 `guard`,
    /// 부활 `revive`, 무적연장 `iframe`), 섬영 전용 회피, 화면 붉은 플래시·흔들림·사운드.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerHealth : MonoBehaviour, IDamageable, IRunResettable
    {
        [Header("스탯 (원본 CONFIG.player — project_test.html:605~606)")]
        [Tooltip("최대 체력. 원본 baseHp 100.")]
        public float maxHp = 100f;
        [Tooltip("피격 후 무적시간(초). 원본 invulnTime 0.9 — 접촉 반복 피해를 막는 건 전적으로 이 값이다.")]
        public float invulnTime = 0.9f;

        [Header("피격 반동 (원본 그대로, 100px=1유닛)")]
        [Tooltip("가해자 반대 방향으로 밀리는 속도. 원본 260px/s ÷100(project_test.html:1908).")]
        public float knockbackSpeedX = 2.6f;
        [Tooltip("위로 튕기는 최소 속도. 원본 `vy = min(vy, -220)`인데 원본 Y+가 아래라 부호를 뒤집었다(:1909).")]
        public float knockbackSpeedY = 2.2f;

        /// <summary>현재 체력.</summary>
        public float CurrentHp { get; private set; }
        public float MaxHp => maxHp;
        public bool IsDead => CurrentHp <= 0f;
        /// <summary>남은 무적시간(초). 0보다 크면 피해를 받지 않는다.</summary>
        public float InvulnRemaining { get; private set; }

        SpriteRenderer sprite; // 무적 깜빡임용(원본 :6195)

        /// <summary>
        /// 사망 시 1회 발생. 같은 오브젝트 안에서 쓰는 국지적 신호다.
        /// **런을 끝내는 경로는 이게 아니라 `Core.CombatEvents.PlayerDied`** — `Systems`(3층)가
        /// `Characters`(2층)를 참조할 수 없어서 `CombatDirector`는 그쪽만 구독한다(원본 :1927).
        /// </summary>
        public event Action Died;

        Rigidbody2D rb;

        void Awake()
        {
            CurrentHp = maxHp;
            rb = GetComponent<Rigidbody2D>();
            sprite = GetComponent<SpriteRenderer>();
            // 원본은 레벨업 때만 최대체력을 다시 계산하고 풀피로 채운다(project_test.html:1850)
            // — 골드 강화(hp)를 사더라도 다음 레벨업 전까지는 즉시 반영되지 않는, 원본 그대로의
            // 비직관적 동작이다(임의로 "매 프레임 갱신"으로 바꾸지 말 것). 구독은 아래 OnEnable에서.
        }

        /// <summary>지금 레벨업을 듣고 있는 프로필. 프로필이 통째로 바뀌면(새 원정) 새 쪽으로 갈아탄다(<see cref="ProfileService.Replaced"/>).</summary>
        PlayerProfile listening;

        void OnEnable()
        {
            ProfileService.Replaced += ListenToCurrentProfile;
            ListenToCurrentProfile();
        }

        // 정적 이벤트·정적 프로필 구독이라 해제 안 하면 파괴된 오브젝트를 향한 호출이 남는다 — EnemyHealth 테스트 오염 때 겪은 것과 같은 함정.
        void OnDisable()
        {
            ProfileService.Replaced -= ListenToCurrentProfile;
            if (listening != null) listening.LeveledUp -= HandleLeveledUp;
            listening = null;
        }

        void ListenToCurrentProfile()
        {
            if (listening == ProfileService.Current) return;
            if (listening != null) listening.LeveledUp -= HandleLeveledUp;
            listening = ProfileService.Current;
            listening.LeveledUp += HandleLeveledUp;
        }

        void HandleLeveledUp()
        {
            maxHp = PlayerStatCalculator.ComputeMaxHp(ProfileService.Current);
            CurrentHp = maxHp;
        }

        void Update()
        {
            if (InvulnRemaining > 0f) InvulnRemaining -= Time.deltaTime;
            if (BlinkRemaining > 0f) BlinkRemaining -= Time.deltaTime;
            UpdateBlink();
            UpdateRegen(Time.deltaTime);
        }

        /// <summary>
        /// 재생의 구슬 — 초당 최대 체력의 일정 비율을 회복한다(원본 `regen`, `updateItemBuffs` :4393).
        /// 최대 체력일 때는 아무 일도 안 한다.
        /// </summary>
        /// <summary>
        /// 원본 무적 깜빡임 — `blink = run && invuln > 0 && floor(time × 14) % 2 == 0`이면 캐릭터를 안 그린다(:6195~:6196).
        /// 시간은 게임 시간(히트스톱·일시정지에 멈춤)이라 원본 `state.time`과 같다.
        /// 원본 `p.invuln`에 해당하는 건 <see cref="BlinkRemaining"/>다 — 섬영의 상시 무적은 안 깜빡인다(<see cref="GrantInvuln"/>).
        /// </summary>
        void UpdateBlink()
        {
            if (sprite == null) return;
            bool blink = GameState.Current == GameScene.Run && BlinkRemaining > 0f && !IsDead
                         && Mathf.FloorToInt(Time.time * 14f) % 2 == 0;
            if (sprite.enabled == blink) sprite.enabled = !blink;
        }

        void UpdateRegen(float dt)
        {
            float per = ProfileService.Current.items.Pow("regen");
            if (per <= 0f || IsDead || CurrentHp >= maxHp) return;

            regenTick += maxHp * per * dt;
            // 1 미만은 모아뒀다가 정수 단위로 준다 — 원본 `regenAcc`(`:4396`~`:4399`)와 같은 방식이다.
            // 우리 체력은 float이라 소수로 회복해도 되지만, 그러면 표시 체력이 매 프레임 흔들리고
            // 원본과 회복 타이밍이 미묘하게 어긋난다.
            if (regenTick < 1f) return;
            int heal = Mathf.FloorToInt(regenTick);
            regenTick -= heal;
            CurrentHp = Mathf.Min(maxHp, CurrentHp + heal);
        }

        /// <summary>
        /// 새 사냥 시작 시 초기화 — 원본 `resetPlayerForRun()`의 체력 부분
        /// (`p.maxHp = statMaxHp(); p.hp = p.maxHp; p.invuln = 0`, project_test.html:1515).
        ///
        /// **최대체력을 여기서 다시 계산하는 게 핵심이다.** 평소엔 레벨업 때만 갱신돼서(`:1850`)
        /// 로비에서 체력 강화를 사도 즉시 반영되지 않는데, 런에 입장할 때 이 재계산이 일어나
        /// 그제서야 적용된다 — 원본 그대로의 동작이라 "매 프레임 갱신"으로 바꾸지 말 것.
        /// </summary>
        public void ResetForRun()
        {
            var profile = ProfileService.Current;
            maxHp = PlayerStatCalculator.ComputeMaxHp(profile);
            CurrentHp = maxHp;
            InvulnRemaining = BlinkRemaining = 0f; // 원본 `p.invuln = 0`

            // 아이템에서 오는 **런 한정** 자원 — 원본 `startRun`이 매 런 다시 채운다(:4310).
            guardLeft = Mathf.RoundToInt(profile.items.Pow("guard"));
            reviveLeft = Mathf.RoundToInt(profile.items.Pow("revive"));
            regenTick = 0f;
        }

        /// <summary>원본 `p.hp = round(p.maxHp * 0.4)`(:1919).</summary>
        public const float ReviveHpRatio = 0.4f;
        /// <summary>원본 `p.invuln = max(p.invuln, 2.0)`(:1920).</summary>
        public const float ReviveInvuln = 2f;

        /// <summary>영혼의 그릇으로 무효화할 수 있는 남은 피격 횟수(런 한정).</summary>
        public int guardLeft;
        /// <summary>최후의 발악으로 남은 부활 횟수(런 한정).</summary>
        public int reviveLeft;
        float regenTick;

        /// <summary>
        /// 남은 무적시간을 최소 이 값까지 늘린다(줄이지는 않는다). 원본 `p.invuln = Math.max(p.invuln, 0.22)`
        /// (project_test.html:2149, 불길 이동 텔레포트 직후 무적)에 대응 — 다른 무적(피격 직후 0.9초 등)이
        /// 이미 더 길게 남아있으면 그대로 둔다.
        ///
        /// **몇 프레임만 덮는 무적은 깜빡이지 않는다**(2026-10-24). 섬영의 방향키 무적·최고 속도 충돌 무적은 원본에선
        /// 타이머가 아니라 맞는 순간의 조건 검사라 캐릭터가 깜빡이지 않는데(:1883·:1889), 우리는 `BladeCombat`이
        /// 매 프레임 2프레임어치씩 이 함수로 갱신한다 — 그대로 깜빡임에 넣으면 방향키를 누르는 내내 깜빡였다
        /// (사용자 "섬영 기본 공격할 때 캐릭터가 깜빡인다").
        /// </summary>
        public void GrantInvuln(float duration)
        {
            InvulnRemaining = Mathf.Max(InvulnRemaining, duration);
            if (duration > SustainedGrantMax) BlinkRemaining = Mathf.Max(BlinkRemaining, duration);
        }

        /// <summary>이 길이 이하로 주는 무적은 "매 프레임 갱신하는 상시 무적"으로 보고 깜빡이지 않는다(20fps 2프레임 = 0.1초).</summary>
        public const float SustainedGrantMax = 0.1f;

        /// <summary>깜빡임이 남은 시간 — 원본 `p.invuln`. 맞은 뒤·부활·순간이동처럼 한 번에 주는 무적만 여기 들어간다.</summary>
        public float BlinkRemaining { get; private set; }

        /// <summary>맞았을 때(영혼의 그릇으로 막은 것 포함)의 무적 — 깜빡임도 같은 길이(원본 `p.invuln = invulnTime + iframe`).</summary>
        void SetHitInvuln(float duration) => InvulnRemaining = BlinkRemaining = duration;

        /// <summary>
        /// 체력을 회복한다(최대체력을 넘지 않게 자른다). 원본엔 이 이름의 공용 함수가 따로 없고,
        /// 회복이 필요한 곳마다 `p.hp = Math.min(p.maxHp, p.hp + heal)`를 그때그때 인라인으로 썼다
        /// (흡혈 `bladeLifesteal` project_test.html:2503, 재생의 구슬 `updateItemBuffs` :4393 등) —
        /// 여기서는 그 한 줄을 공용 메서드로 뽑은 것뿐이라 새 동작을 추가하지 않는다.
        ///
        /// 죽은 상태에서는 회복하지 않는다(원본에도 사망 이후 회복이 의미 있는 경로가 없다).
        ///
        /// **팀장 확인 필요**: `PlayerHealth`는 `docs/worksplit.md`의 팀원 담당 파일 목록에 없는
        /// 공용 파일이다 — 섬영 흡혈(`BladeCombat`)에 필요해서 최소한으로 추가했다(2026-09-28,
        /// 사용자 확인 후 재작성). main 병합 전에 팀장 리뷰가 필요하다.
        /// </summary>
        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead) return;
            CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            // 원본 `if (p.invuln > 0 ...) return;`(project_test.html:1881).
            // 적 쪽에서도 겹침 판정 전에 무적을 확인하지만(:4144), Enemies 도메인은 Characters를 참조할 수
            // 없으므로(asmdef 같은 층) 우리 구조에선 이 검사 하나가 그 역할을 전부 맡는다 — 결과는 동일하다.
            if (IsDead || InvulnRemaining > 0f) return;

            var profile = ProfileService.Current;

            // 영혼의 그릇 — 남은 횟수만큼 피격을 통째로 무효화한다(원본 `guard` :1897).
            if (guardLeft > 0)
            {
                guardLeft--;
                SetHitInvuln(invulnTime + profile.items.Pow("iframe"));
                return;
            }

            // 원본 `dmg = max(1, round(dmg * mult * drMult() * rand(0.9,1.1)))`(:1904).
            // `drMult()`가 '불괴의 갑주'다. 난수를 **여기서** 굴리는 게 원본 구조다(클래스 주석 참고).
            float dr = PlayerStatCalculator.ComputeDamageTakenMultiplier(profile);
            int applied = Mathf.Max(1, Mathf.RoundToInt(amount * dr * UnityEngine.Random.Range(0.9f, 1.1f)));
            CurrentHp -= applied;
            // 불굴의 껍질 — 피격 후 무적시간 연장(원본 `invulnTime + itemPow('iframe')` :1907).
            SetHitInvuln(invulnTime + profile.items.Pow("iframe"));
            HitFeel.PlayerHurt(); // 원본 `state.redFlash = 0.35; shake(7, 0.22)`(:1912~:1913)

            // 원본 `p.vx = sign(p.x - srcX) * 260; p.vy = min(p.vy, -220); p.onGround = false;`(:1908~1910)
            // 주의: 원본에서 이 수평 속도는 **다음 프레임에 이동 입력으로 통째로 덮어써진다**
            // (`p.vx = mx * moveSpeed ...`, :1548 — 피격 중 이동 잠금이 없다). 우리 CharacterMover2D도
            // FixedUpdate마다 vx를 덮어쓰므로 별도 처리 없이 같은 동작이 된다 — 일부러 넉백을 유지시키지 말 것.
            // 반면 수직 속도는 CharacterMover2D가 보존하므로(종단속도 클램프만 함) 튕겨 오르는 건 남는다.
            if (rb != null)
            {
                float dirX = 1f;
                if (source != null)
                {
                    dirX = Mathf.Sign(transform.position.x - source.transform.position.x);
                    if (Mathf.Approximately(dirX, 0f)) dirX = 1f;
                }
                rb.linearVelocity = new Vector2(dirX * knockbackSpeedX, Mathf.Max(rb.linearVelocity.y, knockbackSpeedY));
            }

            if (CurrentHp <= 0f)
            {
                // 최후의 발악 — 쓰러지는 대신 체력 40%로 다시 일어난다(원본 `revive` :1917).
                // **아이템이 있을 때만, 사냥당 정해진 횟수만** — 원본에 R키 같은 수동 부활은 없다.
                if (reviveLeft > 0)
                {
                    reviveLeft--;
                    CurrentHp = Mathf.Round(maxHp * ReviveHpRatio);
                    GrantInvuln(ReviveInvuln);
                    HitFeel.Shake(12f, 0.4f); // 원본 :1922
                    return;
                }

                CurrentHp = 0f;
                RunState.NoteDeath(source); // 전투 패배창 "붕괴 원인"(2026-10-24)
                Died?.Invoke();
                // 원본은 체력이 0이 되는 그 자리에서 바로 런을 끝낸다(`endRun('dead')`, :1927).
                CombatEvents.RaisePlayerDied();
            }
        }
    }
}
