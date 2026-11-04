using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>아이템 등급. 원본 `ITEM_GRADES`(project_test.html:749).</summary>
    public enum ItemGrade { Common, Rare, Epic, Legend }

    /// <summary>
    /// 아이템 종류. 원본 `kind`(`:757`):
    /// <c>Stat</c>은 정해진 스탯 축을 올리고, <c>Special</c>은 고유 효과다.
    /// </summary>
    public enum ItemKind { Stat, Special }

    /// <summary>
    /// 아이템이 올리는 스탯 축. 원본 `stat` 키를 그대로 옮겼다.
    /// `as`는 C# 예약어라 <see cref="AtkSpeed"/>로 바꿨다(다른 곳의 `UpgradeStat`과 같은 이유).
    /// </summary>
    public enum ItemStat { None, Atk, Hp, Gold, Exp, Ms, AtkSpeed, Crit, Mob, Rate, CritDmg, Dmg, Cd, Dr }

    /// <summary>아이템 정의 한 줄. 원본 `ITEMS[id]`.</summary>
    public class ItemDef
    {
        public string id;
        public string name;
        public string icon;
        public ItemGrade grade;
        public ItemKind kind;
        public ItemStat stat = ItemStat.None;
        /// <summary>중첩 1개당 세기. 원본 `per`.</summary>
        public float per;
        /// <summary>참이면 **곱연산**(중첩마다 per를 곱한다). 원본 `mul: true`.</summary>
        public bool multiplicative;
        public string description;
    }

    /// <summary>
    /// 아이템 35종 — 원본 `ITEMS`(project_test.html:758~803) 그대로.
    ///
    /// **이 게임의 유일한 영구 성장 축**이다(원본 주석 `:746`). 시간의 파편로 가챠를 돌려 얻고,
    /// 회귀해도 사라지지 않는다.
    ///
    /// 등급이 높을수록 개당 효과가 크지만 **중복 상한이 낮다**(일반 10 → 전설 3).
    /// 원본 주석 그대로 "저등급도 끝까지 모으면 쓸모가 있고, 고등급은 '빨리 도달하는' 가치를 갖는다" —
    /// 상한을 없애거나 균일하게 만들면 이 구조가 통째로 사라진다.
    /// </summary>
    public static class ItemDatabase
    {
        // 원본 ITEM_GRADES(:749) — weight는 가챠 등급 가중치, cap은 중복 상한.
        public static int Weight(ItemGrade g) => g switch
        {
            ItemGrade.Common => 55,
            ItemGrade.Rare => 30,
            ItemGrade.Epic => 12,
            ItemGrade.Legend => 3,
            _ => 0,
        };

        public static int Cap(ItemGrade g) => g switch
        {
            ItemGrade.Common => 10,
            ItemGrade.Rare => 6,
            ItemGrade.Epic => 4,
            ItemGrade.Legend => 3,
            _ => 0,
        };

        public static string GradeName(ItemGrade g) => g switch
        {
            ItemGrade.Common => "일반",
            ItemGrade.Rare => "희귀",
            ItemGrade.Epic => "영웅",
            ItemGrade.Legend => "전설",
            _ => "?",
        };

        public static Color GradeColor(ItemGrade g) => g switch
        {
            ItemGrade.Common => new Color(0.62f, 0.71f, 0.63f),
            ItemGrade.Rare => new Color(0.44f, 0.78f, 1f),
            ItemGrade.Epic => new Color(0.79f, 0.54f, 1f),
            ItemGrade.Legend => new Color(1f, 0.77f, 0.30f),
            _ => Color.white,
        };

        static ItemDef Stat(string id, string nm, string icon, ItemGrade g, ItemStat stat, float per, string desc, bool mul = false) =>
            new ItemDef { id = id, name = nm, icon = icon, grade = g, kind = ItemKind.Stat, stat = stat, per = per, multiplicative = mul, description = desc };

        static ItemDef Special(string id, string nm, string icon, ItemGrade g, float per, string desc) =>
            new ItemDef { id = id, name = nm, icon = icon, grade = g, kind = ItemKind.Special, per = per, description = desc };

        /// <summary>원본 `ITEMS` 정의 순서 그대로(스탯 13종 → 특수 22종).</summary>
        public static readonly ItemDef[] All =
        {
            // ---- 스탯 아이템: 축 하나당 정확히 하나 ----
            Stat("atk",     "오니의 송곳니", "🦷", ItemGrade.Common, ItemStat.Atk,     6f,    "공격력 +6"),
            Stat("hp",      "산군의 심장",   "🫀", ItemGrade.Common, ItemStat.Hp,      60f,   "최대 체력 +60"),
            Stat("gold",    "황금 향로",     "🏮", ItemGrade.Common, ItemStat.Gold,    1.15f, "골드 획득 ×1.15", mul: true),
            Stat("exp",     "수행의 굴레",   "📿", ItemGrade.Common, ItemStat.Exp,     1.15f, "경험치 획득 ×1.15", mul: true),
            Stat("ms",      "축지의 짚신",   "👟", ItemGrade.Rare,   ItemStat.Ms,      0.06f, "이동속도 +6%"),
            Stat("as",      "야차의 손목띠", "🎗", ItemGrade.Rare,   ItemStat.AtkSpeed, 0.06f, "공격속도 +6%"),
            Stat("crit",    "매의 눈동자",   "👁", ItemGrade.Rare,   ItemStat.Crit,    0.03f, "치명타 확률 +3%"),
            Stat("mob",     "요기 응집",     "🌀", ItemGrade.Rare,   ItemStat.Mob,     1f,    "필드 최대 몹 +1"),
            Stat("rate",    "황천의 문",     "⛩", ItemGrade.Rare,   ItemStat.Rate,    0.10f, "스폰 속도 +10%"),
            Stat("critDmg", "처형인의 각인", "💢", ItemGrade.Epic,   ItemStat.CritDmg, 0.35f, "치명타 피해 +0.35배"),
            Stat("dmg",     "전생의 투지",   "🔥", ItemGrade.Epic,   ItemStat.Dmg,     1.12f, "모든 피해 ×1.12", mul: true),
            Stat("cd",      "시간의 조각",   "⏳", ItemGrade.Epic,   ItemStat.Cd,      0.06f, "쿨타임 -6%"),
            Stat("dr",      "불괴의 갑주",   "🛡", ItemGrade.Epic,   ItemStat.Dr,      0.05f, "받는 피해 -5%"),

            // ---- 특수 아이템: 고유 효과 ----
            Special("regen",       "재생의 구슬",   "💧", ItemGrade.Common, 0.005f, "초당 최대 체력 0.5% 회복"),
            Special("eliteLure",   "몬스터 유인향", "🕯", ItemGrade.Common, 0.5f,   "엘리트 출현률 +50%"),
            Special("killSpeed",   "사신의 발걸음", "💨", ItemGrade.Common, 0.12f,  "처치 시 3초간 이동속도 +12%"),
            Special("knockback",   "파쇄의 망치",   "🔨", ItemGrade.Common, 0.5f,   "넉백 +50%"),
            Special("shrineBless", "성소의 은총",   "🎐", ItemGrade.Common, 1.0f,   "성소 가호 지속 +100%"),
            Special("combo",       "연격의 부적",   "🎴", ItemGrade.Rare,   1.0f,   "콤보 피해 배수 +100%"),
            Special("ambush",      "기습의 송곳니", "🗡", ItemGrade.Rare,   0.4f,   "체력이 가득 찬 적에게 피해 +40%"),
            Special("giantSlayer", "거인 사냥꾼",   "🏹", ItemGrade.Rare,   0.2f,   "엘리트·보스에게 피해 +20%"),
            Special("crowd",       "군집 학살자",   "🌪", ItemGrade.Rare,   0.15f,  "근처 적 3마리 이상이면 피해 +15%"),
            Special("plague",      "역병의 고리",   "☠", ItemGrade.Rare,   1f,     "처치 시 주변에 화상·출혈 1중첩 전이"),
            Special("iframe",      "불굴의 껍질",   "🐚", ItemGrade.Rare,   0.25f,  "피격 후 무적시간 +0.25초"),
            Special("killRage",    "광란의 북",     "🥁", ItemGrade.Rare,   0.06f,  "처치 시 3초간 공격속도 +6% (5중첩)"),
            Special("headstart",   "시작의 유산",   "🎁", ItemGrade.Rare,   2f,     "환생 시 골드 강화 2레벨을 갖고 시작"),
            Special("guard",       "영혼의 그릇",   "🏺", ItemGrade.Rare,   1f,     "사냥마다 피격 1회를 무효화"),
            Special("lifesteal",   "흡혈의 인장",   "🩸", ItemGrade.Epic,   0.04f,  "준 피해의 4%를 회복"),
            Special("execute",     "처형의 낫",     "⚰", ItemGrade.Epic,   0.3f,   "체력 25% 이하 적에게 피해 +30%"),
            Special("vuln",        "균열의 각인",   "🕸", ItemGrade.Epic,   0.08f,  "피해를 준 적이 취약해진다 (받는 피해 +8%)"),
            Special("lastStand",   "배수의 진",     "⚔", ItemGrade.Epic,   0.25f,  "체력 30% 이하일 때 피해 +25%"),
            Special("deathBlast",  "연쇄 폭심",     "💥", ItemGrade.Legend, 0.9f,   "처치 시 주변이 폭발한다 (피해 0.9배)"),
            Special("revive",      "최후의 발악",   "🕯", ItemGrade.Legend, 1f,     "쓰러져도 사냥당 1회 부활한다 (체력 40%)"),
            Special("airDmg",      "천공의 지배",   "🪶", ItemGrade.Legend, 0.25f,  "공중에 있을 때 피해 +25%"),
            Special("keepSpec",    "각인의 봉인",   "🔒", ItemGrade.Legend, 1f,     "회귀해도 무기 전문화가 유지된다"),
        };

        static Dictionary<string, ItemDef> byId;

        public static ItemDef Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, ItemDef>(All.Length);
                foreach (var d in All) byId[d.id] = d;
            }
            return id != null && byId.TryGetValue(id, out var def) ? def : null;
        }

        public static int CapOf(string id)
        {
            var d = Get(id);
            return d != null ? Cap(d.grade) : 0;
        }
    }
}
