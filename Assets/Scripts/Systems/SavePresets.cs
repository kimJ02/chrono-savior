using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 세이브 슬롯 3개의 시나리오(2026-11-19, 교수님 지시 — 다음 주 수요일 플레이테스트. 처음부터 끝까지 다 해 보기 어려우니
    /// 게임의 초반 / 조금 성장한 상태 / 유물 + 능력치 풀 상태를 바로 해 볼 수 있게).
    /// 1. 새 출발 — 아무것도 없는 처음 상태. 들어가면 캐릭터부터 고른다.
    /// 2. 성장 — 중력 법사, 레벨 16, 중력 5층(스킬만 다 찍음), 골드 강화·유물 없음, 챕터 1 클리어(다음 2-1).
    /// 3. 중력 법사 풀세팅 — 레벨 25, 중력 5층, 골드 강화 각 15, 유물 전부 각성(스폰 유물 각성 = 몬스터 2배) + 슬롯 전용 몬스터 2배 더, 챕터 1~2 클리어(다음 3-1 — 잠겨 있어 1~2챕터를 다시 돈다).
    /// 수치는 팀이 플레이해 보고 바꾸면 된다(이 파일만 고치면 다음 초기화부터 반영).
    /// </summary>
    public static class SavePresets
    {
        public static string Name(int slot) => slot switch
        {
            1 => "새 출발",
            2 => "성장",
            3 => "중력 법사 풀세팅",
            _ => "?",
        };

        public static string Blurb(int slot) => slot switch
        {
            1 => "처음부터 — 캐릭터를 고르고 시작",
            2 => "스킬 5층까지 — 유물·강화 없음",
            3 => "유물 전부 각성 + 능력치 최대 + 몬스터 4배",
            _ => "",
        };

        public static PlayerProfile Build(int slot)
        {
            var p = new PlayerProfile();
            switch (slot)
            {
                case 1:
                    p.needsCharacterSelect = true;
                    break;

                case 2:
                    // 2026-11-19 사용자 — "예전에 5차까지 찍었을 때만큼은 강하게, 유물·능력치 강화 없이"(GPT와 정함):
                    // 스킬만 5층(SP 15 = Lv.16), 골드·강화·유물 0. 슬롯 3과 차이 = 강화 + 유물.
                    p.character = CharacterId.Mage;
                    p.level = 16;                                     // SP 15
                    for (int i = 0; i < 5; i++) p.TryLearnMageTier(MageBranch.Gravity); // 1+2+3+4+5 = SP 15
                    Conquer(p, 1); // 챕터 1 클리어 → 다음은 2-1(사용자 2026-11-19)
                    break;

                case 3:
                    p.character = CharacterId.Mage;
                    p.level = 25;                                     // SP 24
                    for (int i = 0; i < 5; i++) p.TryLearnMageTier(MageBranch.Gravity); // 1+2+3+4+5 = SP 15
                    SetUpgrades(p, 15);
                    p.gold = 50000;
                    p.shards = 200;
                    foreach (var d in ItemDatabase.All)
                        while (p.items.Count(d.id) < ItemDatabase.Cap(d.grade)) p.items.Add(d.id);
                    Conquer(p, 2);
                    p.spawnScale = 2f; // 몬스터 수 2배 더(사용자 2026-11-19) — 유물 각성 2배와 곱해져 원래의 약 4배
                    break;
            }
            return p;
        }

        /// <summary>직업 테스트 전용 슬롯 번호 — 슬롯 1~3(교수님 시나리오)을 덮지 않게 따로 쓴다(slot_9.json).</summary>
        public const int ClassTestSlot = 9;

        /// <summary>
        /// 직업별 최종 빌드 + 유물 전부 각성(2026-11-22 사용자 "각 직업마다 최종 빌드 + 유물 버전 플레이 테스트") — 슬롯 3과 같은 조건
        /// (레벨 25, 강화 15씩, 챕터 1~2 정복, 몬스터 배율 2)에 그 캐릭터·갈래를 끝까지 찍는다.
        /// </summary>
        public static PlayerProfile BuildClassTest(CharacterId who, int branch)
        {
            var p = new PlayerProfile();
            p.character = who;
            p.level = 30; // 어느 갈래든 끝 층까지 찍을 SP가 넉넉하게
            for (int i = 0; i < 10; i++)
            {
                bool ok = who switch
                {
                    CharacterId.Mage => p.TryLearnMageTier(branch == 0 ? MageBranch.Explosion : MageBranch.Gravity),
                    CharacterId.Gunner => p.TryLearnGunnerTier(branch == 0 ? GunnerBranch.Laser : GunnerBranch.Installer),
                    CharacterId.Blade => p.TryLearnBladeTier(branch == 0 ? BladeBranch.Focus : BladeBranch.Storm),
                    _ => false,
                };
                if (!ok) break;
            }
            SetUpgrades(p, 15);
            p.gold = 50000;
            p.shards = 200;
            foreach (var d in ItemDatabase.All)
                while (p.items.Count(d.id) < ItemDatabase.Cap(d.grade)) p.items.Add(d.id);
            Conquer(p, 2);
            p.spawnScale = 2f;
            return p;
        }

        static void SetUpgrades(PlayerProfile p, int n)
        {
            p.upgrades.atk = p.upgrades.hp = p.upgrades.ms = p.upgrades.atkSpeed = p.upgrades.crit = n;
        }

        /// <summary>챕터 1~n을 모두 깬 상태(스테이지 1~5 클리어 + 챕터 보스 정복, 2026-11-19 챕터 구조).</summary>
        static void Conquer(PlayerProfile p, int chapters)
        {
            for (int c = 1; c <= chapters && c <= ChapterConfig.ChapterCount; c++)
            {
                for (int st = 1; st <= ChapterConfig.StagesPerChapter; st++) p.MarkStageCleared(c, st);
                p.MarkBossCleared(c);
            }
            // 프리셋은 "지난 생에 깬 진행"이다 — 이번 생 보스 처치로 남기면 보스를 안 잡고 회귀해도 파편이 나온다(2026-11-20)
            p.ForgetThisLifeBossKills();
        }
    }
}
