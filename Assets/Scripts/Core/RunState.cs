using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>원본 `run.mode`(project_test.html:1471) — 일반 사냥 / 보스 스테이지.</summary>
    public enum RunMode { Normal, Boss }

    /// <summary>
    /// 런이 끝난 이유. 원본 `endRun(reason)`(project_test.html:4354)이 받는 문자열 3종 그대로.
    /// 결과 화면 제목이 이걸로 갈린다.
    /// </summary>
    public enum RunEndReason
    {
        /// <summary>쓰러졌다. 원본 `endRun('dead')`(:1927).</summary>
        Dead,
        /// <summary>제한시간 종료. 원본 `endRun('timeout')`(:4413).</summary>
        Timeout,
        /// <summary>보스 격파. 원본 `endRun('bossdead')`(:4289) — 보스가 아직 없어 미사용.</summary>
        BossDead,
    }

    /// <summary>
    /// 지금 진행 중인 한 판(런)의 상태. 원본 전역 `run` 오브젝트(project_test.html:1470)에서
    /// **런 사이클에 필요한 부분만** 옮긴 것이다.
    ///
    /// 원본 `run`에 있지만 여기 없는 것과 그 이유:
    /// - `waveTimer`/`minionTimer`/`shrineTimer` → 스폰 주체(`Systems/EnemySpawner`, 팀원 소유)가 각자 들고 있는다.
    /// - `mapW` → 우리는 `Core/FieldBounds`가 이미 소유한다.
    /// - `chainN`/`chainT`(연쇄 처치), 아이템 런 한정 상태(`guardLeft` 등) → 해당 시스템을 만들 때 추가한다
    ///   (지금 없는 기능을 위해 미리 필드를 만들지 않는다).
    ///
    /// **팀원 세션은 이 클래스를 읽기만 한다**(`docs/worksplit.md` 공유 계약). 특히 <see cref="Region"/>으로
    /// 몹 종류(`rollSpawnType`, :3933)와 지역 스케일을 결정한다.
    /// </summary>
    public static class RunState
    {
        // 원본 CONFIG.run(project_test.html:695)
        public const float NormalTime = 90f;
        public const float BossTime = 180f;
        /// <summary>지역 토벌 목표. 원본 `CONFIG.regionKillTarget`(:699).</summary>
        public const int RegionKillTarget = 100;

        /// <summary>현재 지역 1~9. 원본 `run.region`.</summary>
        public static int Region { get; private set; } = 1;
        public static RunMode Mode { get; private set; } = RunMode.Normal;

        /// <summary>남은 시간(초). 0이 되면 원본은 `endRun('timeout')`(:4413).</summary>
        public static float TimeLeft { get; private set; }
        /// <summary>이번 원정에 흐른 시간(보스 필드로 넘어가도 이어서 잰다) — Figma 원정 기록창 "기록 시간"(2026-10-24).</summary>
        public static float Elapsed { get; private set; }
        /// <summary>마지막으로 플레이어를 쓰러뜨린 것 — Figma 전투 패배창 "붕괴 원인"(UI가 보스인지 본다). 없으면 null.</summary>
        public static GameObject DeathSource { get; private set; }

        public static void NoteDeath(GameObject source) => DeathSource = source;

        /// <summary>이번 런 처치 수. 원본 `run.kills` — **보스는 세지 않는다**(:1857).</summary>
        public static int Kills { get; private set; }

        /// <summary>살기 스택. 원본 `run.fury`(:1825) — 보스 포함 **모든** 처치마다 1씩. 피해 +0.8%/공속 +0.4%씩 붙는다(:702).</summary>
        public static int Fury { get; private set; }

        /// <summary>이번 런에서 번 골드/경험치(결과 화면 표시용). 원본 `run.goldEarned`/`run.expEarned`.</summary>
        public static int GoldEarned { get; private set; }
        public static long ExpEarned { get; private set; } // 27레벨부터 구슬 하나가 `int`를 넘는다(`PlayerProfile.RequiredExp`)

        /// <summary>
        /// **이번 원정**의 최고 콤보 — 결과 화면 '원정 통계'(Figma가 추가한 통계 — 원본 결과 화면엔 콤보 표시가 없다). <see cref="Begin"/>이 지우고
        /// 보스 필드로 넘어가도 이어진다. 사냥 준비가 실패하면 <see cref="Restore"/>가 지난 원정 값을 되돌린다(2026-11-06 4단계 — 예전엔 `CombatModifiers`에 있어
        /// 준비 실패 때 지워졌다). 평생 최고는 프로필 `stats.maxCombo`(`PlayerProfile.RecordCombo`).
        /// </summary>
        public static int MaxCombo { get; private set; }

        /// <summary>콤보가 올랐다(`CombatModifiers.AddCombo`) — 이번 원정 최고를 갱신한다.</summary>
        public static void NoteCombo(int combo) { if (combo > MaxCombo) MaxCombo = combo; }

        /// <summary>런이 끝났는지. 원본 `run.over` — true면 갱신도 추가 피해도 멈춘다(:1881, :7169).</summary>
        public static bool Over { get; private set; }

        /// <summary>원본 `startRun`(:4293)의 런 상태 초기화 부분.</summary>
        public static void Begin(int region, RunMode mode)
        {
            Region = region;
            Mode = mode;
            TimeLeft = mode == RunMode.Normal ? NormalTime : BossTime;
            Kills = 0;
            Elapsed = 0f;
            DeathSource = null;
            Fury = 0;
            GoldEarned = 0;
            ExpEarned = 0;
            MaxCombo = 0;
            Over = false;
        }

        /// <summary>
        /// 런 도중 보스 필드로 넘어간다 — **사용자 지시(2026-09-16)로 추가된 전환이다.**
        /// 원본에는 없다(원본은 보스전이 로비에서 시작하는 별개의 런이다, `:4293`).
        ///
        /// **런은 끊기지 않는다.** 처치 수·살기·번 골드/경험치를 그대로 들고 간다 — 일반 필드에서
        /// 쌓은 살기가 보스전에 그대로 얹히는 게 이 구조의 핵심 보상이다. 바뀌는 것은
        /// 모드와 제한시간뿐이고, 시간은 <see cref="BossTime"/>으로 **새로 시작한다**
        /// (남은 시간을 물려받으면 일반 필드에서 시간을 다 쓴 경우 보스를 볼 수조차 없다).
        /// </summary>
        /// <returns>실제로 전환됐으면 true. 이미 보스 필드거나 끝난 런이면 false.</returns>
        public static bool EnterBossField()
        {
            if (Over || Mode == RunMode.Boss) return false;
            Mode = RunMode.Boss;
            TimeLeft = BossTime;
            return true;
        }

        /// <summary>원본 `updateRun`의 `run.timeLeft -= dt`(:4412). 0에 닿으면 true를 돌려준다(호출자가 timeout 처리).</summary>
        public static bool Tick(float dt)
        {
            if (Over) return false;
            TimeLeft = Mathf.Max(0f, TimeLeft - dt);
            Elapsed += dt;
            return TimeLeft <= 0f;
        }

        public static void MarkOver() => Over = true;

        /// <summary>
        /// 처치 집계. 원본은 `run.fury++`를 보스 포함 전부(:1825), `run.kills++`는 보스 제외(:1857)로
        /// 서로 다르게 센다 — 그대로 옮겼다.
        /// </summary>
        public static void RegisterKill(bool isBoss)
        {
            Fury++;
            if (!isBoss) Kills++;
        }

        /// <summary>결과 화면에 쓸 런 누적 보상. 원본 `run.goldEarned`/`run.expEarned` 누산(:1816, :1820).</summary>
        public static void RegisterReward(int gold, long exp)
        {
            GoldEarned += gold;
            ExpEarned += exp;
        }

        /// <summary>테스트 격리용(`ProfileService.Reset`과 같은 이유).</summary>
        public static void Reset() => Begin(1, RunMode.Normal);

        /// <summary>런 상태 전체의 사본 — 사냥에 들어가다 실패하면 들어가기 전(예: 결과 화면의 지난 런)으로 되돌릴 때(`GameFlow.StartRun`, 2026-11-05 씬 분할 2차).</summary>
        public readonly struct Snapshot
        {
            internal readonly int region, kills, fury, gold, maxCombo;
            internal readonly RunMode mode;
            internal readonly float timeLeft, elapsed;
            internal readonly GameObject deathSource;
            internal readonly long exp;
            internal readonly bool over;

            internal Snapshot(int region, RunMode mode, float timeLeft, float elapsed, GameObject deathSource,
                              int kills, int fury, int gold, long exp, int maxCombo, bool over)
            {
                this.region = region; this.mode = mode; this.timeLeft = timeLeft; this.elapsed = elapsed; this.deathSource = deathSource;
                this.kills = kills; this.fury = fury; this.gold = gold; this.exp = exp; this.maxCombo = maxCombo; this.over = over;
            }
        }

        public static Snapshot Capture() =>
            new Snapshot(Region, Mode, TimeLeft, Elapsed, DeathSource, Kills, Fury, GoldEarned, ExpEarned, MaxCombo, Over);

        public static void Restore(Snapshot s)
        {
            Region = s.region; Mode = s.mode; TimeLeft = s.timeLeft; Elapsed = s.elapsed; DeathSource = s.deathSource;
            Kills = s.kills; Fury = s.fury; GoldEarned = s.gold; ExpEarned = s.exp; MaxCombo = s.maxCombo; Over = s.over;
        }
    }
}
