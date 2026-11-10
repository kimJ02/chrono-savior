namespace ChronoSavior.Core
{
    /// <summary>
    /// 게임플레이 효과음 트리거 — 캐릭터 액션(점프·공격·스킬)이 일어났다는 알림만 한다(실제 재생은
    /// `UI/GameplaySfx`). <see cref="CombatEvents"/>와 같은 이유로 여기를 거친다 — 계층 규칙상
    /// `Characters`/`Combat`(1~2층)는 `UI`(4층)를 몰라야 해서, 소리를 트리거하는 쪽은
    /// `AudioSource`를 들고 있지 않고 **"이 일이 일어났다"만 알리고**, 실제 재생(`Resources.Load`·
    /// 음량 설정 읽기)은 UI 층의 구독자가 한다(`CLAUDE.md`의 `EnemySpawnRequestBus`와 같은 패턴).
    ///
    /// `CombatEvents`를 재사용하지 않는 이유: 그건 "적↔플레이어 사이를 넘어가는 신호"로 범위가 좁게
    /// 문서화돼 있어서(`CombatEvents.cs` 머리 주석), 캐릭터 자기 자신의 액션 신호까지 섞으면 범위가
    /// 헷갈린다. 사망음은 그대로 <see cref="CombatEvents.PlayerDied"/>를 재사용한다(이미 있는 신호).
    ///
    /// 2026-11-10 사운드 적용 스프린트 — 사운드팩에 아직 없는 효과(마법사/메카닉 기본공격, 칼날폭풍,
    /// 범용 타격음)도 트리거는 미리 걸어 둔다. `UI/GameplaySfx`가 `Resources.Load`로 못 찾으면
    /// 조용히 넘어가므로 나중에 파일만 추가하면 코드를 더 안 건드려도 바로 들린다.
    /// </summary>
    public static class SfxEvents
    {
        // ── 공통 ──
        public static event System.Action<CharacterId> Jumped;
        public static void RaiseJumped(CharacterId id) => Jumped?.Invoke(id);

        /// <summary>플레이어가 직접 타격을 받았을 때(사망 순간도 <see cref="PlayerHealth.TakeDamage"/>가 이 피격 처리를 먼저 지나므로 같이 난다).</summary>
        public static event System.Action PlayerDamaged;
        public static void RaisePlayerDamaged() => PlayerDamaged?.Invoke();

        public static event System.Action BossRoomEntered;
        public static void RaiseBossRoomEntered() => BossRoomEntered?.Invoke();

        // ── 마법사 ──
        /// <summary>아직 갈래(화염/중력)를 안 고른 상태의 기본공격 — 사운드팩에 아직 전용 음원 없음.</summary>
        public static event System.Action MageBasicAttack;
        public static void RaiseMageBasicAttack() => MageBasicAttack?.Invoke();
        public static event System.Action MageFireAttack;
        public static void RaiseMageFireAttack() => MageFireAttack?.Invoke();
        public static event System.Action MageFireSkill;
        public static void RaiseMageFireSkill() => MageFireSkill?.Invoke();
        public static event System.Action MageGravityAttack;
        public static void RaiseMageGravityAttack() => MageGravityAttack?.Invoke();
        public static event System.Action MageGravitySkill;
        public static void RaiseMageGravitySkill() => MageGravitySkill?.Invoke();

        // ── 메카닉 ──
        /// <summary>레이저 빌드가 아닐 때(또는 0층) 나가는 그냥 총알 — 사운드팩에 아직 전용 음원 없음.</summary>
        public static event System.Action MechanicBasicAttack;
        public static void RaiseMechanicBasicAttack() => MechanicBasicAttack?.Invoke();
        /// <summary>
        /// 레이저 빔 On/Off 상태 변화(눌려 있는 동안 매 프레임이 아니라 **바뀐 순간만** true/false로 한 번) —
        /// 2026-11-10 정정: 레이저는 누르고 있는 동안 계속 들려야 해서(루프), 한 번만 알리던 것에서
        /// 꺼지는 순간까지 함께 넘기도록 bool을 들고 가게 바꿨다. 재생(루프 시작/정지)은 `UI/GameplaySfx`.
        /// </summary>
        public static event System.Action<bool> MechanicLaserActive;
        public static void RaiseMechanicLaserActive(bool active) => MechanicLaserActive?.Invoke(active);
        public static event System.Action MechanicDroneFired;
        public static void RaiseMechanicDroneFired() => MechanicDroneFired?.Invoke();
        /// <summary>설치기를 바닥에 놓는 순간(소리 파일 `mechanic_machine_set`).</summary>
        public static event System.Action MechanicTurretSet;
        public static void RaiseMechanicTurretSet() => MechanicTurretSet?.Invoke();
        /// <summary>
        /// 설치기 가동음(소리 파일 `mechanic_machine_operating`) — 2026-11-10 정정: 놓는 순간 한 번이 아니라
        /// 설치기가 2개 이상 링크되어 장판이 떠 있는 동안 ~4.5초마다 반복(<see cref="Characters.GunnerAttack"/>의
        /// `UpdateField`가 올린다). 놓는 순간의 소리는 그대로 <see cref="MechanicTurretSet"/>(파일 `mechanic_machine_set`).
        /// </summary>
        public static event System.Action MechanicTurretOperating;
        public static void RaiseMechanicTurretOperating() => MechanicTurretOperating?.Invoke();
        /// <summary>메카닉 X 스킬 쿨다운이 다 찼을 때(사운드팩 파일명이 `both_` — 레이저 드론·설치기 공용).</summary>
        public static event System.Action MechanicSkillReady;
        public static void RaiseMechanicSkillReady() => MechanicSkillReady?.Invoke();

        // ── 살수 ──
        /// <summary>
        /// 기본공격(회전베기 Z) — 사운드팩 attack1~3을 **랜덤이 아니라** VFX(<see cref="Characters.BladeSlashVfx"/>가
        /// 베기 그림을 번갈아 쓰는 것)와 같은 번갈이 숫자(1~3)로 맞춰서 재생한다. 2026-11-10 정정 —
        /// 그 번갈이 숫자를 들고 있는 쪽이 `BladeSlashVfx`라 거기서 올린다(그림 팩이 없는 클론도 소리는 난다).
        /// </summary>
        public static event System.Action<int> AssassinBasicAttack;
        public static void RaiseAssassinBasicAttack(int index) => AssassinBasicAttack?.Invoke(index);
        /// <summary>
        /// 집중(시간 느려짐) On/Off — 2026-11-10 정정: 한 번 울리고 끝나는 신호가 아니라, 레이저와 같은
        /// On/Off 전이 신호로 바꿨다. 지속시간이 티어별로 다르고(3~5층 +1초) 이제 고정값으로 추측할 수
        /// 없어서, 재생기가 고정 시간으로 스스로 끄는 대신 실제로 꺼지는 순간을 그대로 받아 맞춘다.
        /// </summary>
        public static event System.Action<bool> AssassinFocusActive;
        public static void RaiseAssassinFocusActive(bool active) => AssassinFocusActive?.Invoke(active);
        /// <summary>칼날폭풍 시작 — 사운드팩에 아직 전용 음원 없음.</summary>
        public static event System.Action AssassinSpinAttack;
        public static void RaiseAssassinSpinAttack() => AssassinSpinAttack?.Invoke();

        /// <summary>테스트 격리용(정적 이벤트라 구독 해제 안 하면 다음 테스트로 새어나간다 — `CombatEvents.Reset`과 같은 이유).</summary>
        public static void Reset()
        {
            Jumped = null;
            PlayerDamaged = null;
            BossRoomEntered = null;
            MageBasicAttack = null;
            MageFireAttack = null;
            MageFireSkill = null;
            MageGravityAttack = null;
            MageGravitySkill = null;
            MechanicBasicAttack = null;
            MechanicLaserActive = null;
            MechanicDroneFired = null;
            MechanicTurretSet = null;
            MechanicTurretOperating = null;
            MechanicSkillReady = null;
            AssassinBasicAttack = null;
            AssassinFocusActive = null;
            AssassinSpinAttack = null;
        }
    }
}
