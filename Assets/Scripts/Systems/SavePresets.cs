using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 세이브 슬롯 3개의 시나리오(2026-11-19, 교수님 지시 — 다음 주 수요일 플레이테스트. 처음부터 끝까지 다 해 보기 어려우니
    /// 게임의 초반 / 조금 성장한 상태 / 유물 + 능력치 풀 상태를 바로 해 볼 수 있게).
    /// 1. 새 출발 — 아무것도 없는 처음 상태. 들어가면 캐릭터부터 고른다.
    /// 2. 성장 — 중력 법사, 레벨 16, 중력 5층(스킬만 다 찍음), 골드 강화·유물 없음, 1~2지역 정복.
    /// 3. 중력 법사 풀세팅 — 레벨 25, 중력 5층, 골드 강화 각 15, 유물 32종 전부 각성(스폰 유물 각성 = 몬스터 2배), 1~2지역 정복.
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
            3 => "유물 전부 각성 + 능력치 최대 + 몬스터 2배",
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
                    Conquer(p, 2);
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
                    break;
            }
            return p;
        }

        static void SetUpgrades(PlayerProfile p, int n)
        {
            p.upgrades.atk = p.upgrades.hp = p.upgrades.ms = p.upgrades.atkSpeed = p.upgrades.crit = n;
        }

        /// <summary>1~n지역을 정복한 상태(토벌 목표 채움 + 보스 열림·처치).</summary>
        static void Conquer(PlayerProfile p, int regions)
        {
            for (int r = 1; r <= regions && r <= RegionConfig.Count; r++)
            {
                p.regionKills[r - 1] = RunState.RegionKillTarget;
                p.regionBossUnlocked[r - 1] = true;
                p.MarkBossCleared(r);
            }
        }
    }
}
