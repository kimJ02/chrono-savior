using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 유물 조회 — **지금 켜져 있는** 랭크와 그 랭크의 수치(2026-11-19 테스트 유물, <see cref="ItemDatabase"/>).
    ///
    /// - 보유 = 적용(장착 칸 없음). 랭크 = 보유 수(상한까지).
    /// - 캐릭터 전용은 지금 캐릭터가 주인일 때만, 갈래 유물은 그 갈래를 골랐을 때만 켜진다 — 아니면 랭크 0.
    /// - 유물 효과가 낸 피해는 다른 유물을 다시 발동시키지 않는다(<see cref="InRelicDamage"/>, 무한 연쇄 방지).
    /// </summary>
    public static class Relics
    {
        /// <summary>지금 유물 효과가 피해를 주는 중 — 발동 집계(처치 n번마다 등)는 이때 세지 않는다.</summary>
        public static bool InRelicDamage { get; private set; }

        /// <summary>유물 피해를 주는 동안 감싼다. <c>using (Relics.Damaging()) { … }</c></summary>
        public static Scope Damaging() => new Scope(true);

        /// <remarks>구조체라 <c>new Scope()</c>(인자 없음)는 생성자를 건너뛴다 — 꼭 <see cref="Damaging"/>으로 만든다.</remarks>
        public readonly struct Scope : System.IDisposable
        {
            readonly bool prev;
            public Scope(bool on) { prev = InRelicDamage; InRelicDamage = on; }
            public void Dispose() => InRelicDamage = prev;
        }

        /// <summary>그 유물이 지금 켜져 있는 랭크(0 = 꺼짐). 보유 수를 상한으로 자른다.</summary>
        public static int Rank(string id) => Rank(ProfileService.Current, id);

        public static int Rank(PlayerProfile p, string id)
        {
            var def = ItemDatabase.Get(id);
            if (def == null || p == null) return 0;
            int n = Mathf.Min(p.items.Count(id), ItemDatabase.Cap(def.grade));
            if (n <= 0) return 0;
            return IsActiveFor(p, def) ? n : 0;
        }

        /// <summary>그 유물이 이 프로필(캐릭터·갈래)에서 켜지는 종류인지 — 보유 여부와 무관.</summary>
        public static bool IsActiveFor(PlayerProfile p, ItemDef def)
        {
            switch (def.owner)
            {
                case RelicOwner.Common: return true;
                case RelicOwner.Mage: if (p.character != CharacterId.Mage) return false; break;
                case RelicOwner.Gunner: if (p.character != CharacterId.Gunner) return false; break;
                case RelicOwner.Blade: if (p.character != CharacterId.Blade) return false; break;
            }
            switch (def.branch)
            {
                case RelicBranch.Any:
                case RelicBranch.Base: return true;
                case RelicBranch.Explosion: return p.mageBranch == MageBranch.Explosion;
                case RelicBranch.Gravity: return p.mageBranch == MageBranch.Gravity;
                case RelicBranch.Laser: return p.gunnerBranch == GunnerBranch.Laser;
                case RelicBranch.Installer: return p.gunnerBranch == GunnerBranch.Installer;
                case RelicBranch.Focus: return p.bladeBranch == BladeBranch.Focus;
                case RelicBranch.Storm: return p.bladeBranch == BladeBranch.Storm;
            }
            return false;
        }

        /// <summary>최대 랭크(각성)인지.</summary>
        public static bool Awakened(string id)
        {
            var def = ItemDatabase.Get(id);
            return def != null && Rank(id) >= ItemDatabase.Cap(def.grade);
        }

        /// <summary>지금 랭크의 k번째 수치(꺼져 있으면 0).</summary>
        public static float V(string id, int k)
        {
            int r = Rank(id);
            if (r <= 0) return 0f;
            var def = ItemDatabase.Get(id);
            if (k < 0 || k >= def.values.Length) return 0f;
            var arr = def.values[k];
            return arr[Mathf.Clamp(r - 1, 0, arr.Length - 1)];
        }

        /// <summary><see cref="V"/>의 정수판(간격·개수).</summary>
        public static int VI(string id, int k) => Mathf.RoundToInt(V(id, k));
    }
}
