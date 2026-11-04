using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 몬스터들의 성소 — 원본 `spawnShrine()`(project_test.html:3996)과 파괴 처리(`killEnemy`의
    /// 성소 분기, `:1797`).
    ///
    /// **움직이지도 때리지도 않는 구조물**이다(원본 `if (e.shrine) continue;` `:4024`).
    /// 대신 살아 있는 동안 **모든 적이 강해지고**(피해 ×1.3, 이속 ×1.25), 부수면 그 힘이
    /// 15초간 **플레이어**에게 넘어온다 + 경험치 30 · 골드 45.
    ///
    /// 즉 "빨리 부술수록 이득"인 선택지다 — 방치하면 필드 전체가 위험해지고, 부수러 가면
    /// 그동안 다른 적에게 노출된다. 이 긴장이 성소의 존재 이유라, 버프 한쪽만 구현하면 의미가 사라진다.
    ///
    /// 적 목록에 들어가는 게 아니라 <see cref="EnemyHealth"/>를 그대로 쓰고 <see cref="EnemyMove"/>만
    /// 꺼서 만든다 — 원본도 `enemies` 배열에 넣고 이동 분기에서만 건너뛴다.
    ///
    /// **소유권 예외(2026-09-30, 팀원 추가)**: `IGrabExempt` 구현은 섬영 칼날폭풍 4층 "강제 연행"이
    /// 성소를 붙잡지 못하게 막으려고 팀원이 추가했다(원본 `e.shrine` 제외, project_test.html:2694).
    /// 사용자 확인 받음 — `Core/IGrabExempt.cs` 주석과 PROGRESS.md 참고, 팀장 확인 필요.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class Shrine : MonoBehaviour, IGrabExempt
    {
        // 원본 CONFIG.shrine(project_test.html:698)
        /// <summary>기본 체력. 원본 `hp: 70`.</summary>
        public const float BaseHp = 70f;
        /// <summary>레벨당 체력 배수. 원본 `hpGrow: 1.4`.</summary>
        public const float HpGrowPerLevel = 1.4f;
        /// <summary>첫 등장 시각(런 시작 후 초). 원본 `firstAt: 18`.</summary>
        public const float FirstAt = 18f;
        /// <summary>재등장 간격. 원본 `interval: 35`.</summary>
        public const float Interval = 35f;
        /// <summary>파괴 보상 경험치. 원본 `exp: 30`.</summary>
        public const int RewardExp = 30;
        /// <summary>파괴 보상 골드. 원본 `gold: 45`.</summary>
        public const int RewardGold = 45;
        /// <summary>스폰 직후 무적. 원본 `spawnInvuln: 1.0`.</summary>
        public const float SpawnProtect = 1f;

        /// <summary>원본 `hp: round(70 * 1.4^(lv-1))`(`:4001`) — 몹 레벨을 따른다.</summary>
        public static float HpForLevel(int level) => BaseHp * Mathf.Pow(HpGrowPerLevel, level - 1);

        EnemyHealth health;
        ISpawnProtectable spawnProtect;

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
            spawnProtect = GetComponent<ISpawnProtectable>();
            health.Died += HandleDestroyed;
        }

        /// <summary>
        /// 원본은 이 상태를 **매 프레임 다시 계산한다** —
        /// `run.shrineActive = enemies.some(e => !e.dead && e.shrine && e.spawnInvuln <= 0)`(`:4414`).
        /// 핵심은 `spawnInvuln <= 0`이다: **아직 스폰 보호 중인 성소는 적을 강화하지 않는다.**
        /// 세울 때 플래그를 한 번 켜는 방식이면 손도 못 대는 1초 동안 필드 전체가 먼저 강해진다.
        /// </summary>
        void Update()
        {
            bool protectedNow = spawnProtect != null && spawnProtect.IsSpawnProtected;
            CombatModifiers.SetShrineActive(!health.IsDead && !protectedNow);
        }

        void OnDestroy()
        {
            if (health != null) health.Died -= HandleDestroyed;
            // 파괴든 런 종료(RunTransient 정리)든, 사라지면 적 버프도 같이 꺼져야 한다.
            // 원본은 성소가 한 번에 하나뿐이라(`:3997`) 이 단순한 처리로 충분하다.
            CombatModifiers.SetShrineActive(false);
        }

        /// <summary>
        /// 원본 `killEnemy`의 성소 분기(project_test.html:1797~1810). **일반 처치 보상 경로를 타지 않는다** —
        /// 골드/경험치가 고정값이고, 살기·연쇄 처치에도 안 들어간다(원본은 여기서 `return`한다).
        /// </summary>
        void HandleDestroyed(EnemyHealth _)
        {
            ProfileService.Current.stats.shrines++;
            ProfileService.Current.AddExp(RewardExp);
            ProfileService.Current.AddGold(RewardGold);
            RunState.RegisterReward(RewardGold, RewardExp);

            // 부순 힘이 플레이어에게 넘어온다 — 수신은 `CombatModifiers`가 이미 구현해 뒀다.
            CombatEvents.RaiseShrineBuffGranted(CombatModifiers.ShrineBuffDuration);
        }
    }
}
