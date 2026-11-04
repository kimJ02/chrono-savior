using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 골드 강화가 반영된 파생 스탯. 원본 `statAtk`/`statMaxHp`/`statMs`/`statAs`/`statCrit`
    /// (project_test.html:1284~1288)에서 아이템 항(전부 0, 아이템 시스템이 아직 없음)만 뺀 형태다.
    /// 캐릭터 배수(`charStatMult()`)는 반영돼 있다.
    ///
    /// 기본값은 원본 `CONFIG.player`(:605)·`CONFIG.bow.dmg`(:609)를 그대로 옮긴 것.
    /// `BaseCritChance`가 `Combat.DamageCalculator.BaseCritChance`와 값이 겹치는데(둘 다 0.10) —
    /// `Core`는 `Combat`을 참조할 수 없어서(asmdef 계층, Core는 참조 대상 없음) 부득이 중복
    /// 정의했다. SO 전환(스프린트 4)에서 데이터 하나로 합칠 것 — 값을 바꿀 땐 두 곳 다 같이.
    /// </summary>
    public static class PlayerStatCalculator
    {
        public const float BaseAtk = 10f;          // CONFIG.player.baseAtk
        public const float BaseMaxHp = 100f;       // CONFIG.player.baseHp
        public const float BaseCritChance = 0.10f; // CONFIG.player.baseCrit
        public const float BowDamageMult = 0.9f;   // CONFIG.bow.dmg(:609) — 마법사 무기 배수

        // 원본 statAtk/statMaxHp는 완성된 스탯 전체에 캐릭터 배수를 곱한다(`charStatMult()`, :1284~1285).
        // ms/as/crit(:1286~1288)엔 곱하지 않는다 — 원본 그대로이니 "일관성"을 이유로 바꾸지 말 것.
        public static float ComputeAtk(PlayerProfile p) =>
            (BaseAtk + p.upgrades.atk * 3f + p.items.Add(ItemStat.Atk)) * CharacterStats.StatMultiplier(p.character);

        public static float ComputeMaxHp(PlayerProfile p) =>
            Mathf.Round((BaseMaxHp + p.upgrades.hp * 25f + p.items.Add(ItemStat.Hp)) * CharacterStats.StatMultiplier(p.character));

        public static float ComputeMoveSpeedMultiplier(PlayerProfile p) =>
            1f + p.upgrades.ms * 0.04f + p.items.Add(ItemStat.Ms);

        /// <summary>
        /// 원본 `statAs()`(project_test.html:1287):
        /// `(1 + upgrades.as * 0.04 + itemAdd) * (1 + run.fury * 0.004) * (1 + killRage)`.
        ///
        /// **살기(fury)가 스탯 자체에 들어 있다** — 키트가 따로 곱하는 게 아니다. 여기 두면
        /// 이미 `statAs`를 쓰는 모든 공격(마법사·메카닉, 앞으로 섬영·드루이드)이 자동으로 받는다.
        /// 아이템 항(`itemAdd('as')`)까지 들어 있다. `killRage`(처치 시 일시 공속)는 런 한정 버프라 아직 없다.
        /// </summary>
        public static float ComputeAttackSpeedMultiplier(PlayerProfile p) =>
            (1f + p.upgrades.atkSpeed * 0.04f + p.items.Add(ItemStat.AtkSpeed)) * CombatModifiers.AttackSpeedMultiplier;

        public static float ComputeCritChance(PlayerProfile p) =>
            BaseCritChance + p.upgrades.crit * 0.02f + p.items.Add(ItemStat.Crit);

        /// <summary>원본 `critMultOf() = CONFIG.player.critMult + itemAdd('critDmg')`(project_test.html:1289).</summary>
        public static float ComputeCritMultiplier(PlayerProfile p) =>
            BaseCritMultiplier + p.items.Add(ItemStat.CritDmg);

        /// <summary>
        /// 치명타 배수 기준값. 원본 `CONFIG.player.critMult = 1.7`(project_test.html:605).
        /// **여기(Core)가 원본 자리다** — `Combat.DamageCalculator`가 이 값을 읽어 쓴다.
        /// Core는 Combat을 참조할 수 없으므로 반대 방향이면 컴파일이 안 된다.
        /// </summary>
        public const float BaseCritMultiplier = 1.7f;

        /// <summary>원본 `cdMult() = max(0.25, 1 - itemAdd('cd'))`(`:1290`) — 쿨타임 감소(하한 25%).</summary>
        public static float ComputeCooldownMultiplier(PlayerProfile p) =>
            Mathf.Max(0.25f, 1f - p.items.Add(ItemStat.Cd));

        /// <summary>원본 `drMult() = max(0.2, 1 - itemAdd('dr'))`(`:1291`) — 받는 피해 감소(하한 20%).</summary>
        public static float ComputeDamageTakenMultiplier(PlayerProfile p) =>
            Mathf.Max(0.2f, 1f - p.items.Add(ItemStat.Dr));
    }
}
