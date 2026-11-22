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

        // ── 시그니처 피해(2026-11-22 사용자 "레이저로 쓸어버려야 재미가 있다" — GPT와 정함) ──
        // 메카닉(레이저 광선·설치 장·설치기)·섬영(집중 궤적·파열·칼날폭풍) 시그니처가 피해를 주는 동안 켠다.
        // 메카닉·섬영은 이게 켜진 처치만 공용 처치 유물(등불·별똥·종언의 종 등) 카운트에 든다 — Z·드론·유물로 화면이 먼저 정리되지 않게.

        /// <summary>지금 시그니처 공격이 피해를 주는 중.</summary>
        public static bool InSignatureDamage { get; private set; }

        /// <summary>시그니처 피해를 주는 동안 감싼다. <c>using (Relics.Signature()) { … }</c></summary>
        public static SignatureScope Signature() => new SignatureScope(true);

        public readonly struct SignatureScope : System.IDisposable
        {
            readonly bool prev;
            public SignatureScope(bool on) { prev = InSignatureDamage; InSignatureDamage = on; }
            public void Dispose() => InSignatureDamage = prev;
        }

        /// <summary>
        /// 이 처치가 공용 처치 유물을 셀 수 있는가(2026-11-22 2차) — 마법사·섬영은 늘(섬영은 Z 회전베기가 주력, 사용자),
        /// 메카닉은 시그니처만(레이저 = 광선·드론 탄, 설치 = 설치기·장판). 설치 갈래 Z 총알 처치는 안 센다.
        /// </summary>
        public static bool KillCountsForCommon(PlayerProfile p)
        {
            if (p == null) return true;
            if (p.character == CharacterId.Gunner && p.gunnerBranch != GunnerBranch.None) return InSignatureDamage;
            return true;
        }

        // ── 레이저가 닿은 적(2026-11-22 사용자 "레이저가 닿기 전에 죽는 적은 없었으면 — 내가 죽인지도 모르겠다") ──
        // 메카닉 레이저 갈래에선 유물 공격(공용·전용 모두)이 **레이저 광선이 한 번이라도 닿은 적만** 고른다. 드론(X)은 그대로(사용자 선택).
        static readonly System.Collections.Generic.HashSet<Collider2D> laserTouched = new System.Collections.Generic.HashSet<Collider2D>();

        /// <summary>지금 유물 공격이 "레이저에 닿은 적만"을 골라야 하는가(메카닉 레이저 갈래).</summary>
        public static bool RequireLaserTouch
        {
            get
            {
                var p = ProfileService.Current;
                return p != null && p.character == CharacterId.Gunner && p.gunnerBranch == GunnerBranch.Laser;
            }
        }

        /// <summary>레이저 광선이 이 적에 닿았다.</summary>
        public static void MarkLaserTouched(Collider2D c)
        {
            if (c == null) return;
            if (laserTouched.Count > 512) laserTouched.RemoveWhere(x => x == null); // 죽어 사라진 것 정리
            laserTouched.Add(c);
        }

        /// <summary>유물 공격이 이 적을 칠 수 있는가 — 레이저 갈래면 레이저가 닿은 적만.</summary>
        public static bool RelicCanHit(Collider2D c) => !RequireLaserTouch || (c != null && laserTouched.Contains(c));

        /// <summary>새 사냥 — 닿음 기록을 비운다.</summary>
        public static void ClearLaserTouched() => laserTouched.Clear();

        // ── 발동 알림(2026-11-19 전투 화면 유물 칸이 반짝이게) ──
        static readonly System.Collections.Generic.Dictionary<string, float> lastProc = new System.Collections.Generic.Dictionary<string, float>();

        /// <summary>유물이 발동했다 — 전투 화면 유물 칸이 잠깐 반짝인다.</summary>
        public static void Proc(string id) => lastProc[id] = Time.unscaledTime;

        /// <summary>마지막 발동 뒤 지난 실제 시간(발동한 적 없으면 큰 값).</summary>
        public static float SinceProc(string id) => lastProc.TryGetValue(id, out var t) ? Time.unscaledTime - t : 999f;

        /// <summary>그 유물이 지금 켜져 있는 랭크(0 = 꺼짐). 보유 수를 상한으로 자른다.</summary>
        public static int Rank(string id) => Rank(ProfileService.Current, id);

        public static int Rank(PlayerProfile p, string id)
        {
            if (p == null || id == null) return 0;
            Ensure(p);
            return ranks.TryGetValue(id, out var r) ? r : 0;
        }

        // ── 랭크·세트·태그 캐시(2026-11-20 최적화) ──
        // 처치·피격·폭발·매 프레임 HUD마다 유물 33종 × 보유 목록을 다시 훑던 것을, 프로필·보유·캐릭터·갈래가 바뀔 때만 다시 센다.
        static PlayerProfile cP;
        static ItemInventory cInv;
        static int cVer = -1;
        static CharacterId cChar;
        static MageBranch cMage;
        static GunnerBranch cGunner;
        static BladeBranch cBlade;
        static int cCharSet;
        static readonly System.Collections.Generic.Dictionary<string, int> ranks = new System.Collections.Generic.Dictionary<string, int>();
        static readonly System.Collections.Generic.Dictionary<string, int> tagCounts = new System.Collections.Generic.Dictionary<string, int>();

        static int stamp;

        /// <summary>켜진 유물·랭크가 바뀔 때마다 오르는 번호 — 화면이 "다시 그릴지" 정하는 데 쓴다(전투 화면 유물 칸).</summary>
        public static int StateVersion
        {
            get
            {
                var p = ProfileService.Current;
                if (p != null) Ensure(p);
                return stamp;
            }
        }

        static void Ensure(PlayerProfile p)
        {
            var inv = p.items;
            int ver = inv != null ? inv.Version : 0;
            if (ReferenceEquals(cP, p) && ReferenceEquals(cInv, inv) && cVer == ver && cChar == p.character
                && cMage == p.mageBranch && cGunner == p.gunnerBranch && cBlade == p.bladeBranch) return;
            cP = p; cInv = inv; cVer = ver; cChar = p.character;
            cMage = p.mageBranch; cGunner = p.gunnerBranch; cBlade = p.bladeBranch;
            stamp++;

            ranks.Clear();
            tagCounts.Clear();
            cCharSet = 0;
            var owner = p.character == CharacterId.Mage ? RelicOwner.Mage
                      : p.character == CharacterId.Gunner ? RelicOwner.Gunner
                      : p.character == CharacterId.Blade ? RelicOwner.Blade : RelicOwner.Common;
            if (inv == null) return;
            foreach (var def in ItemDatabase.All)
            {
                int owned = inv.Count(def.id);
                if (owned <= 0) continue;
                // 세트 — 같은 유물 중복은 1로 센다(켜짐·갈래 무관, 예전 CharacterSetCount/TagCount와 같다)
                if (owner != RelicOwner.Common && def.owner == owner) cCharSet++;
                if (def.tags != null)
                    foreach (var t in def.tags)
                        if (t != null) tagCounts[t] = (tagCounts.TryGetValue(t, out var c) ? c : 0) + 1;
                int n = Mathf.Min(owned, ItemDatabase.Cap(def.grade));
                if (IsActiveFor(p, def)) ranks[def.id] = n;
            }
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

        /// <summary>그 프로필 기준 k번째 수치(<see cref="V"/>의 프로필 지정판).</summary>
        public static float Value(PlayerProfile p, string id, int k)
        {
            int r = Rank(p, id);
            if (r <= 0) return 0f;
            var def = ItemDatabase.Get(id);
            if (k < 0 || k >= def.values.Length) return 0f;
            var arr = def.values[k];
            return arr[Mathf.Clamp(r - 1, 0, arr.Length - 1)];
        }

        // ── 세트(2026-11-19) — 같은 유물 중복은 1로 센다 ──

        /// <summary>지금 캐릭터 전용 유물을 몇 종 가졌는지(갈래 무관) — 캐릭터 세트 3·5개.</summary>
        public static int CharacterSetCount()
        {
            var p = ProfileService.Current;
            if (p == null) return 0;
            Ensure(p);
            return cCharSet;
        }

        /// <summary>그 태그(공용 "연쇄"·"시간")의 유물을 몇 종 가졌는지 — 태그 세트 2·4개.</summary>
        public static int TagCount(string tag)
        {
            var p = ProfileService.Current;
            if (p == null || tag == null) return 0;
            Ensure(p);
            return tagCounts.TryGetValue(tag, out var n) ? n : 0;
        }

        public static bool CharacterSet3 => CharacterSetCount() >= 3;
        public static bool CharacterSet5 => CharacterSetCount() >= 5;

        /// <summary><see cref="V"/>의 정수판(간격·개수).</summary>
        public static int VI(string id, int k) => Mathf.RoundToInt(V(id, k));
    }
}
