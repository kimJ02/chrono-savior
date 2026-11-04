using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 플레이어 진행 상태 — 원본 `meta`(project_test.html:1129 `defaultMeta()`)의 일부를 옮긴 것.
    /// **세이브 데이터다, SO가 아니다**(CLAUDE.md "세이브 데이터 vs 설정 데이터" 절 참고) — 플레이어마다
    /// 다르고 런타임에 계속 바뀌므로 순수 직렬화 가능 클래스로 둔다.
    ///
    /// 스프린트 2("성장곡선 검증", `docs/sprints/03-growth-curve-worksplit.md`)의 트랙 A/B 공유 계약 — 필드
    /// 이름·타입을 임의로 바꾸면 두 트랙이 동시에 깨진다. 트랙 A(처치 보상)는 <see cref="AddGold"/>/
    /// <see cref="AddExp"/>만 호출하고 `gold`/`exp` 필드를 직접 증가시키지 않는다.
    /// </summary>
    [System.Serializable]
    public class PlayerProfile
    {
        /// <summary>
        /// 레벨·경험치·쓴 SP는 **캐릭터마다 따로** 간다 — 원본 `meta.charProgress[캐릭터] = { level, exp, spUsed }`
        /// (project_test.html:1124 `makeCharProgress`, `charProg()` `:1249`). 인덱스는 <see cref="CharacterId"/> 순서.
        /// 골드·골드 강화·시간의 파편·아이템·지역 진행·업적은 원본처럼 **모든 캐릭터가 같이 쓴다**(아래 필드들).
        /// 원본처럼 4칸을 다 만들어 둔다(드루이드는 기획에서 빠졌지만 칸은 남긴다 — 순서가 곧 인덱스다).
        /// </summary>
        public List<CharacterProgress> charProgress = NewCharProgress();

        /// <summary>
        /// **지금 고른 캐릭터**의 레벨·경험치 — 원본 `curLevel()`·`curExp()`(`:1255`~`:1256`).
        ///
        /// 예전엔 필드였다. 이 이름을 트랙 A/B·UI·테스트가 전부 쓰는 공유 계약이라, 이름은 그대로 두고
        /// 캐릭터별 칸(<see cref="Progress"/>)을 가리키게만 바꿨다 — 그래서 소문자 프로퍼티다. 다른 캐릭터의 값은
        /// <see cref="ProgressOf"/>로 읽는다. 세이브(`JsonUtility`)는 프로퍼티를 안 쓰고 <see cref="charProgress"/>만 쓴다.
        /// </summary>
        public int level { get => Progress.level; set => Progress.level = value; }
        public long exp { get => Progress.exp; set => Progress.exp = value; }

        public int gold = 0;

        /// <summary>
        /// 지금 고른 캐릭터. 원본 `meta.character`(project_test.html:1134, 기본값 'mage').
        /// 파생 스탯의 캐릭터 배수(<see cref="CharacterStats.StatMultiplier"/>)가 이 값을 본다.
        /// <see cref="level"/>·<see cref="exp"/>·<see cref="spUsed"/>도 이 캐릭터의 칸을 가리킨다(원본 `charProg()`).
        /// </summary>
        public CharacterId character = CharacterId.Mage;

        /// <summary>
        /// **지금 고른 캐릭터**가 스킬트리(전문화)에 쓴 SP — 원본 `charProg().spUsed`(project_test.html:7023).
        /// 캐릭터마다 따로다(<see cref="charProgress"/>). 예전엔 필드였고 이름은 그대로다(<see cref="level"/> 참고).
        /// </summary>
        public int spUsed { get => Progress.spUsed; set => Progress.spUsed = value; }
        public MageBranch mageBranch = MageBranch.None;
        public int mageTier = 0;

        /// <summary>
        /// 메카닉 빌드 — 원본 `meta.skills.gunner = {branch, tier}`(project_test.html:1139).
        /// SP는 메카닉 자기 몫이다(<see cref="charProgress"/> — 로비에서 메카닉을 고른 채로 배운다).
        /// </summary>
        public GunnerBranch gunnerBranch = GunnerBranch.None;
        public int gunnerTier = 0;

        /// <summary>
        /// 섬영(블레이드) 전문화 — 원본 `meta.skills.blade = {branch, tier}`(project_test.html:1140).
        ///
        /// ⚠️ **팀원 추가(2026-09-28), 팀장 확인 필요**: `docs/worksplit.md` 7절·9절, `HANDOFF.md`는
        /// "캐릭터별 SP/branch/tier 일반화는 단계 2에서 메카닉 스킬트리와 같이 처리한다"고 명시하고
        /// `Core/` 전체를 팀원에게 "읽기만" 허용한다 — 이 필드들은 그 문서상 순서를 벗어난 예외다.
        /// 사용자 확인(2026-09-28: "내가 PlayerProfile에 섬영 전용 필드 추가")으로 최소 침습적으로
        /// (마법사 필드는 전혀 안 건드리고) 추가만 했다. 팀장이 실제 일반화(캐릭터별 `PlayerProfile`
        /// 분리 등) 작업을 할 때 이 필드들은 그 구조로 흡수/정리될 걸 전제로 한다 — 지금은 마법사와
        /// 같은 패턴(전용 필드 + 전용 SP 풀)을 그대로 복제한 임시 형태.
        ///
        /// SP는 **섬영 자신의 성장 칸**(`ProgressOf(CharacterId.Blade)`)의 레벨·쓴 SP로 센다 — 원본 `charProg('blade')`.
        /// (예전엔 레벨은 공유하고 SP만 전용 풀이었다 — 2026-10-14 병합 때 캐릭터별 성장 칸으로 옮겼다.)
        ///
        /// ✅ 2026-10-14 병합: 마법사·메카닉·섬영 모두 캐릭터별 성장 칸(레벨·경험치·쓴 SP)을 쓴다(`docs/worksplit.md` 7절).
        /// </summary>
        public BladeBranch bladeBranch = BladeBranch.None;
        public int bladeTier = 0;
        /// <summary>섬영이 쓴 SP — 섬영 성장 칸의 `spUsed`(예전 전용 필드, 2026-10-14 병합 때 이름 그대로 프로퍼티로).</summary>
        public int bladeSpUsed { get => ProgressOf(CharacterId.Blade).spUsed; set => ProgressOf(CharacterId.Blade).spUsed = value; }

        public UpgradeLevels upgrades = new UpgradeLevels();

        /// <summary>시간의 파편 — 가챠 재화. 원본 `meta.shards`(project_test.html:1147). **회귀해도 안 없어진다.**</summary>
        public int shards = 0;

        /// <summary>
        /// 보유 아이템. 원본 `meta.items`(`:1146`) — **회귀해도 사라지지 않는 유일한 성장 축**이다.
        /// </summary>
        public ItemInventory items = new ItemInventory();

        /// <summary>누적 통계. **회귀해도 안 지워진다** — 업적이 "이번 생"이 아니라 "지금까지"를 본다.</summary>
        public PlayerStats stats = new PlayerStats();

        /// <summary>달성한 업적 id. 원본 `meta.achieved`(project_test.html:1150).</summary>
        public List<string> achieved = new List<string>();
        /// <summary>지금까지 회귀한 횟수. 원본 `meta.regressions` — 회귀 장벽 계산의 기준이다.</summary>
        public int regressions = 0;

        /// <summary>
        /// 멸망까지 남은 기간(일) — 사용자 결정(2026-10-24): **7일로 시작해 스테이지에 들어갈 때마다 하루 준다**(원본에 없는 규칙,
        /// 팀 기획 "n일차" — 잡몹만 천천히 잡고 나와 강화하는 느린 공략을 막는 장치). 시간 회귀하면 다시 7일.
        /// 0일이면 스테이지에 못 들어가고 시간 회귀만 남는다(0일 규칙은 아직 안 정해져 우선 이렇게 — 사용자 확인 전).
        /// 옛 세이브엔 이 칸이 없어서 불러오면 기본값 7이 된다(`SaveService.Load`는 기본값 위에 덮어쓴다).
        /// </summary>
        public int doomDays = DoomDaysStart;
        public const int DoomDaysStart = 7;
        public bool CanEnterStage => doomDays > 0;
        public void SpendDay() { if (doomDays > 0) doomDays--; }

        /// <summary>
        /// 지역별 진행도 — 원본 `meta.regions[r] = { kills, bossUnlocked, bossCleared }`(project_test.html:1131).
        /// 인덱스는 0부터라 `[0]`이 1지역이다.
        ///
        /// 셋이 사슬처럼 이어진다: **누적 처치 100 → 보스 해금 → 보스 격파 → 다음 지역 개방**.
        /// 하나라도 빠지면 진행이 막히거나(해금 안 됨) 압력이 사라진다(전부 개방).
        /// </summary>
        public int[] regionKills = new int[RegionConfig.Count];
        public bool[] regionBossUnlocked = new bool[RegionConfig.Count];
        public bool[] regionBossCleared = new bool[RegionConfig.Count];

        /// <summary>캐릭터 칸 수 — <see cref="CharacterId"/> 값 개수(원본 `CHARACTERS` 4종).</summary>
        static readonly int CharacterCount = System.Enum.GetValues(typeof(CharacterId)).Length;

        /// <summary>원본 `makeCharProgress()`(`:1124`) — 캐릭터마다 1레벨·경험치 0·SP 0.</summary>
        static List<CharacterProgress> NewCharProgress()
        {
            var list = new List<CharacterProgress>(CharacterCount);
            for (int i = 0; i < CharacterCount; i++) list.Add(new CharacterProgress());
            return list;
        }

        /// <summary>
        /// 그 캐릭터의 성장 칸 — 원본 `charProg(char)`(`:1249`). 없는 캐릭터면 마법사 칸(원본
        /// `CHARACTERS[char] ? char : 'mage'`), 칸이 모자라거나 비었으면 새로 채운다(원본
        /// `if (!meta.charProgress[key]) meta.charProgress[key] = { level: 1, exp: 0, spUsed: 0 }`).
        /// </summary>
        public CharacterProgress ProgressOf(CharacterId id)
        {
            if (charProgress == null) charProgress = new List<CharacterProgress>(CharacterCount);
            while (charProgress.Count < CharacterCount) charProgress.Add(new CharacterProgress());
            int i = (int)id;
            if (i < 0 || i >= CharacterCount) i = (int)CharacterId.Mage;
            return charProgress[i] ??= new CharacterProgress();
        }

        /// <summary>지금 고른 캐릭터의 성장 칸 — 원본 `charProg()`.</summary>
        public CharacterProgress Progress => ProgressOf(character);

        static bool InRange(int region) => region >= 1 && region <= RegionConfig.Count;

        /// <summary>원본 `regionOpen(r) = r === 1 || regions[r-1].bossCleared`(project_test.html:6434).</summary>
        public bool IsRegionOpen(int region)
        {
            if (region <= 1) return true;
            if (!InRange(region)) return false;
            return regionBossCleared[region - 2]; // 이전 지역
        }

        public int RegionKills(int region) => InRange(region) ? regionKills[region - 1] : 0;
        public bool IsBossUnlocked(int region) => InRange(region) && regionBossUnlocked[region - 1];
        public bool IsBossCleared(int region) => InRange(region) && regionBossCleared[region - 1];

        /// <summary>
        /// 이 지역에서 1마리 잡았다. 원본 `:1866`~`:1869`:
        /// <code>
        /// if (R.kills &lt; regionKillTarget) { R.kills++; if (R.kills >= target) R.bossUnlocked = true; }
        /// </code>
        /// **목표에 도달하면 더 안 센다** — 진행 게이지가 100/100에서 멈춘다.
        /// </summary>
        /// <returns>이번 처치로 보스가 새로 해금됐으면 true.</returns>
        public bool RegisterRegionKill(int region)
        {
            if (!InRange(region)) return false;
            int i = region - 1;
            if (regionKills[i] >= RunState.RegionKillTarget) return false;

            regionKills[i]++;
            if (regionKills[i] >= RunState.RegionKillTarget && !regionBossUnlocked[i])
            {
                regionBossUnlocked[i] = true;
                return true;
            }
            return false;
        }

        /// <summary>보스를 잡았다 — 다음 지역이 열린다. 원본 `onBossKilled`(project_test.html:4282).</summary>
        public void MarkBossCleared(int region)
        {
            if (InRange(region)) regionBossCleared[region - 1] = true;
        }

        /// <summary>
        /// 지금 회귀하면 받을 시간의 파편 — 원본 `rpPreview()`(project_test.html:6505).
        /// **이번 생에 정복한 지역만** 센다. 원본 주석 그대로 "갈아넣은 시간이 아니라
        /// '어디까지 뚫었나'가 보상이다" — 시간을 오래 쓴다고 늘지 않는다.
        /// </summary>
        public int ShardPreview()
        {
            int sum = 0;
            for (int r = 1; r <= RegionConfig.Count; r++)
                if (IsBossCleared(r)) sum += RegressionConfig.ShardsOfRegion(r);
            return sum;
        }

        /// <summary>이번 생에 정복한 지역 수. 원본 `clearedRegions()`(`:6496`).</summary>
        public int ClearedRegionCount()
        {
            int n = 0;
            for (int r = 1; r <= RegionConfig.Count; r++) if (IsBossCleared(r)) n++;
            return n;
        }

        /// <summary>
        /// 회귀한다 — 원본 `doRebirth()`(project_test.html:6509).
        ///
        /// **초기화**: **모든 캐릭터의** 레벨·경험치·쓴 SP(원본 `resetAllCharLevels()`·`resetAllCharSp()` `:6519`·`:6524`),
        /// 골드·골드 강화·전문화 빌드·지역 진행.
        /// **유지**: 시간의 파편·회귀 횟수·아이템. '각인의 봉인'이 있으면 빌드(갈래·층)도 — 단 **쓴 SP는 봉인이 있어도
        /// 0이 된다**: 원본이 `resetAllCharSp()`를 봉인 여부와 상관없이 부른다(확인창 문구는 "전문화 유지"라 헷갈리지만
        /// 코드가 기준이다). 그래서 남긴 빌드는 SP를 안 치른 셈이 된다.
        ///
        /// 정복한 지역이 하나도 없으면 아무 일도 안 한다(원본 `if (gain &lt; 1) return`) —
        /// 얻을 게 없는데 진행만 날리는 걸 막는 안전장치다.
        /// </summary>
        /// <returns>얻은 시간의 파편. 0이면 시간 회귀가 일어나지 않았다.</returns>
        public int DoRegression()
        {
            int gain = ShardPreview();
            if (gain < 1) return 0;

            // '각인의 봉인'이 있으면 되돌려줄 값을 미리 잡아둔다(아래에서 복구).
            var keptBranch = mageBranch;
            int keptTier = mageTier;
            var keptGunnerBranch = gunnerBranch;
            int keptGunnerTier = gunnerTier;
            var keptBladeBranch = bladeBranch;
            int keptBladeTier = bladeTier;

            shards += gain;
            regressions++;
            doomDays = DoomDaysStart; // 새 회차 — 멸망까지 다시 7일
            stats.shardsEarned += gain;

            // 원본 `resetAllCharLevels()`(:6519) — **모든 캐릭터**의 레벨·경험치·쓴 SP를 비운다.
            charProgress = NewCharProgress();
            gold = 0;
            upgrades = new UpgradeLevels();
            // 원본 doRebirth(:6509~) — **모든 무기**의 빌드를 비운다(`for (const w in meta.skills)`).
            mageBranch = MageBranch.None;
            mageTier = 0;
            gunnerBranch = GunnerBranch.None;
            gunnerTier = 0;
            bladeBranch = BladeBranch.None;
            bladeTier = 0;

            regionKills = new int[RegionConfig.Count];
            regionBossUnlocked = new bool[RegionConfig.Count];
            regionBossCleared = new bool[RegionConfig.Count];

            // 시작의 유산 — 골드 강화를 맨바닥이 아니라 몇 레벨 쥐고 시작한다(원본 `headstart` :6524).
            int head = Mathf.RoundToInt(items.Pow("headstart"));
            if (head > 0)
            {
                upgrades.atk = upgrades.hp = upgrades.ms = upgrades.atkSpeed = upgrades.crit = head;
            }

            // 각인의 봉인 — 있으면 빌드(갈래·층)가 유지된다(원본 `keepSpec` :6527). 쓴 SP는 위에서 0이 된 그대로다
            // (원본 `resetAllCharSp()` :6524가 봉인과 상관없이 돈다 — 위 요약 주석 참고).
            if (items.Count("keepSpec") > 0)
            {
                mageBranch = keptBranch;
                mageTier = keptTier;
                gunnerBranch = keptGunnerBranch;
                gunnerTier = keptGunnerTier;
                bladeBranch = keptBladeBranch;
                bladeTier = keptBladeTier;
            }

            return gain;
        }

        /// <summary>레벨이 실제로 오를 때(한 번 이상) 1회 발생. `Characters/PlayerHealth`가 구독해서
        /// 최대체력 재계산 + 풀피 회복을 한다(원본 `project_test.html:1850`, 레벨업 때만 일어나고
        /// 골드 강화 구매 자체로는 즉시 반영되지 않는다 — 원본과 동일한 비직관적 동작).</summary>
        // 이벤트는 직렬화 대상이 아니다 — 뒤에 숨은 델리게이트 필드가 private이라 `JsonUtility`가
        // 애초에 건드리지 않는다(`[NonSerialized]`는 이벤트 선언에 못 붙는다, CS0592).
        public event System.Action LeveledUp;

        /// <summary>트랙 A(처치 보상)가 호출. 즉시 누적만 하면 됨(레벨 개념 없음).</summary>
        public void AddGold(int amount)
        {
            gold += amount;
            // 누적 획득 골드는 쓴 만큼 줄지 않는다(업적 '축재'의 기준). 원본 `meta.stats.goldEarned`.
            if (amount > 0) stats.goldEarned += amount;
        }

        /// <summary>
        /// 트랙 A(처치 보상)가 호출. 원본 `gainExpMeta()`(project_test.html:1440) 그대로 —
        /// 초과분을 이월하며 한 번의 호출로 여러 레벨이 오를 수 있다.
        /// </summary>
        public void AddExp(long amount)
        {
            var cp = Progress; // 지금 고른 캐릭터의 몫 — 원본 `const cp = charProg()`(:1441)
            // `long` 끝에서 넘치면 음수가 돼 아래 반복이 끝나지 않는다 — 최댓값에서 멈춘다.
            cp.exp = amount > 0 && cp.exp > long.MaxValue - amount ? long.MaxValue : cp.exp + amount;
            bool leveled = false;
            while (cp.exp >= RequiredExp(cp.level))
            {
                cp.exp -= RequiredExp(cp.level);
                cp.level++;
                leveled = true;
            }
            if (leveled) LeveledUp?.Invoke();
        }

        /// <summary>
        /// 원본 `CONFIG.expCurve(l) = floor(700 × 1.8^(l-1))`(project_test.html:706) — **원본(JS)처럼 double로 계산하고
        /// `long`으로 돌려준다**(2026-10-06). 예전엔 `float` 계산·`int` 결과였는데, 27레벨(30억)에서 `int`를 넘어 음수가
        /// 되면 <see cref="AddExp"/>의 반복이 끝나지 않아 게임이 멈췄다(SP +1로 마법사 32레벨 → 대붕괴 처치 순간).
        /// `float`는 3레벨이 2267(원본 2268)이었고 15레벨부터 수~수천씩 어긋났다.
        /// `long`도 넘는 65레벨부터는 `long` 최댓값에서 멈춘다 — 원본은 계속 커지지만 구슬이 필요량의 25%라
        /// 레벨업 속도는 같다. 1레벨 미만(깨진 세이브)은 1레벨로 친다 — 필요량이 0이면 위 반복이 끝나지 않는다.
        /// </summary>
        public static long RequiredExp(int level)
        {
            double need = System.Math.Floor(700.0 * System.Math.Pow(1.8, System.Math.Max(level, 1) - 1));
            return need >= long.MaxValue ? long.MaxValue : (long)need;
        }

        public int GetUpgradeLevel(UpgradeStat stat) => stat switch
        {
            UpgradeStat.Atk => upgrades.atk,
            UpgradeStat.Hp => upgrades.hp,
            UpgradeStat.Ms => upgrades.ms,
            UpgradeStat.AtkSpeed => upgrades.atkSpeed,
            UpgradeStat.Crit => upgrades.crit,
            _ => 0,
        };

        /// <summary>
        /// 골드가 충분하면 강화 1단계 구매(원본 `buyGoldUpgrade`, project_test.html:7042의 1회분).
        /// 비용은 <see cref="GoldUpgradeConfig.Cost"/>(현재 단계 기준, 원본 `upCost` :1268). 성공 시 true.
        /// </summary>
        public bool TryBuyUpgrade(UpgradeStat stat)
        {
            int level0 = GetUpgradeLevel(stat);
            int cost = GoldUpgradeConfig.Cost(stat, level0);
            if (gold < cost) return false;
            gold -= cost;
            SetUpgradeLevel(stat, level0 + 1);
            return true;
        }

        void SetUpgradeLevel(UpgradeStat stat, int newLevel)
        {
            switch (stat)
            {
                case UpgradeStat.Atk: upgrades.atk = newLevel; break;
                case UpgradeStat.Hp: upgrades.hp = newLevel; break;
                case UpgradeStat.Ms: upgrades.ms = newLevel; break;
                case UpgradeStat.AtkSpeed: upgrades.atkSpeed = newLevel; break;
                case UpgradeStat.Crit: upgrades.crit = newLevel; break;
            }
        }

        /// <summary>원본 `spTotal()`(project_test.html:1258) = 레벨-1. 1레벨엔 SP가 0이다. 지금 고른 캐릭터 기준.</summary>
        public int SpTotal => level - 1;
        /// <summary>원본 `spAvail()`(project_test.html:1259).</summary>
        public int SpAvailable => SpTotal - spUsed;

        /// <summary>
        /// 마법사 빌드 티어당 SP 비용. 원본 `SPEC.bow.move/conv.tiers[].cost`(project_test.html:900-913) —
        /// 두 갈래 모두 1,2,3,4,5로 동일하다.
        /// </summary>
        static readonly int[] MageTierCost = { 1, 2, 3, 4, 5 };

        /// <summary>
        /// 다음 티어를 습득한다(원본 `learnSkill`, project_test.html:7011). 갈래는 처음 배울 때 고정되고
        /// (`branch !== null && branch !== 선택` 이면 거부), 반드시 순서대로만(1→2→3→4→5) 배울 수 있다.
        /// </summary>
        public bool TryLearnMageTier(MageBranch branch)
        {
            if (branch == MageBranch.None) return false;
            if (mageBranch != MageBranch.None && mageBranch != branch) return false;
            int nextTier = mageTier + 1;
            if (nextTier > MageTierCost.Length) return false;
            int cost = MageTierCost[nextTier - 1];
            if (SpAvailable < cost) return false;
            mageBranch = branch;
            mageTier = nextTier;
            spUsed += cost;
            return true;
        }

        /// <summary>
        /// 메카닉 빌드 티어당 SP 비용. 원본 `SPEC.gunner.move/conv.tiers[].cost`(project_test.html:918-930) —
        /// 두 갈래 모두 1,2,3,4,5로 마법사와 같다.
        /// </summary>
        static readonly int[] GunnerTierCost = { 1, 2, 3, 4, 5 };

        /// <summary>
        /// 메카닉 다음 티어 습득 — 원본 `learnSkill('gunner', branch, tier)`(project_test.html:7011). 규칙은 마법사와 같다:
        /// 갈래는 처음 배울 때 고정, 1층부터 순서대로만, SP가 모자라면 실패.
        /// </summary>
        public bool TryLearnGunnerTier(GunnerBranch branch)
        {
            if (branch == GunnerBranch.None) return false;
            if (gunnerBranch != GunnerBranch.None && gunnerBranch != branch) return false;
            int nextTier = gunnerTier + 1;
            if (nextTier > GunnerTierCost.Length) return false;
            int cost = GunnerTierCost[nextTier - 1];
            if (SpAvailable < cost) return false;
            gunnerBranch = branch;
            gunnerTier = nextTier;
            spUsed += cost;
            return true;
        }

        /// <summary>원본 `spAvail()`(:1259) — **섬영 칸**의 레벨·쓴 SP로 센다(지금 고른 캐릭터와 상관없이).</summary>
        public int BladeSpAvailable
        {
            get { var cp = ProgressOf(CharacterId.Blade); return cp.level - 1 - cp.spUsed; }
        }

        /// <summary>
        /// 섬영 빌드 티어당 SP 비용. 원본 `SPEC.blade.move/conv.tiers[].cost`(project_test.html:936-949) —
        /// 마법사와 마찬가지로 두 갈래 모두 1,2,3,4,5로 동일하다.
        /// </summary>
        static readonly int[] BladeTierCost = { 1, 2, 3, 4, 5 };

        /// <summary>
        /// 섬영 다음 티어를 습득한다 — <see cref="TryLearnMageTier"/>와 동일한 규칙(갈래 최초 고정,
        /// 1→2→3→4→5 순서만, SP 부족하면 거부)을 섬영 전용 필드·SP 풀에 대해 그대로 적용한다.
        /// </summary>
        public bool TryLearnBladeTier(BladeBranch branch)
        {
            if (branch == BladeBranch.None) return false;
            if (bladeBranch != BladeBranch.None && bladeBranch != branch) return false;
            int nextTier = bladeTier + 1;
            if (nextTier > BladeTierCost.Length) return false;
            int cost = BladeTierCost[nextTier - 1];
            if (BladeSpAvailable < cost) return false;
            bladeBranch = branch;
            bladeTier = nextTier;
            bladeSpUsed += cost;
            return true;
        }
    }

    /// <summary>
    /// 캐릭터 하나의 성장 — 원본 `meta.charProgress[캐릭터] = { level: 1, exp: 0, spUsed: 0 }`(project_test.html:1126).
    /// 골드·강화처럼 같이 쓰는 것은 여기 넣지 않는다(원본도 `meta` 바로 아래에 둔다).
    /// </summary>
    [System.Serializable]
    public class CharacterProgress
    {
        public int level = 1;
        public long exp = 0; // 원본은 JS 실수 — 27레벨부터 필요량이 `int`를 넘는다(<see cref="PlayerProfile.RequiredExp"/>)
        public int spUsed = 0;
    }

    /// <summary>원본 마법사 빌드 갈래. move=폭발 계열, conv=중력 계열(project_test.html:900-913).</summary>
    public enum MageBranch { None, Explosion, Gravity }

    /// <summary>원본 메카닉 빌드 갈래. move=캐릭터 레이저 빌드, conv=설치기 빌드(project_test.html:915-932).</summary>
    public enum GunnerBranch { None, Laser, Installer }

    /// <summary>원본 섬영 빌드 갈래. move=집중 계열, conv=칼날폭풍 계열(project_test.html:936-949).</summary>
    public enum BladeBranch { None, Focus, Storm }

    /// <summary>
    /// 골드 강화 5종의 현재 단계. 원본 `CONFIG.upgrades`(project_test.html:731)와 이름을 맞췄으나
    /// `as`(공격속도)는 C# 예약어라 `atkSpeed`로 대체했다 — 트랙 A/B 둘 다 이 이름을 쓸 것.
    /// </summary>
    [System.Serializable]
    public class UpgradeLevels
    {
        public int atk;
        public int hp;
        public int ms;
        public int atkSpeed;
        public int crit;
    }
}
