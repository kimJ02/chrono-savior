using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>보유 아이템 한 종류. `JsonUtility`가 딕셔너리를 직렬화 못 해서 리스트로 둔다.</summary>
    [System.Serializable]
    public class ItemStack
    {
        public string id;
        public int count;
    }

    /// <summary>
    /// 보유 아이템과 그 효과 집계 — 원본 `meta.items`와 `itemCount`/`itemPow`/`itemAdd`/`itemMul`
    /// (project_test.html:1185~:1206).
    ///
    /// **회귀해도 사라지지 않는다** — 이게 게임의 유일한 영구 성장 축이라(원본 `:746`),
    /// `PlayerProfile.DoRegression()`가 건드리지 않는 몇 안 되는 필드 중 하나다.
    ///
    /// ## 합연산과 곱연산이 갈린다
    /// 같은 "스탯 아이템"이라도 원본은 두 갈래로 집계한다(`:1189`·`:1199`):
    /// - `itemAdd(stat)` — 중첩 × per를 **더한다**(공격력 +6씩)
    /// - `itemMul(stat)` — per를 중첩 횟수만큼 **곱한다**(골드 ×1.15^n)
    /// 곱연산 아이템을 합연산으로 처리하면 후반 배수가 완전히 달라지므로 섞으면 안 된다.
    /// </summary>
    [System.Serializable]
    public class ItemInventory
    {
        public List<ItemStack> stacks = new List<ItemStack>();

        /// <summary>가챠 천장 카운터. 원본 `meta.pity`(`:6540`).</summary>
        public int pity;

        // ── 조회 캐시(2026-11-20 최적화 — 몬스터 4배·유물 전부 각성에서 처치·피격·매 프레임마다 목록을 훑던 것) ──
        // 보유가 바뀌면(Add, 목록 교체·길이 변화) 버전이 오르고, 다음 조회 때 한 번만 다시 센다. 직렬화하지 않는다.
        [System.NonSerialized] int version;
        [System.NonSerialized] List<ItemStack> seenList;
        [System.NonSerialized] int seenCount = -1;
        [System.NonSerialized] int cachedVersion = -1;
        [System.NonSerialized] Dictionary<string, int> countCache;
        [System.NonSerialized] float[] addCache, mulCache;

        /// <summary>보유가 바뀔 때마다 오르는 번호 — 캐시가 낡았는지 보는 데 쓴다(유물 랭크 캐시 등).</summary>
        public int Version
        {
            get
            {
                if (!ReferenceEquals(seenList, stacks) || seenCount != (stacks != null ? stacks.Count : 0))
                {
                    seenList = stacks;
                    seenCount = stacks != null ? stacks.Count : 0;
                    version++;
                }
                return version;
            }
        }

        /// <summary>목록을 직접 고친 뒤 부른다(게임 코드는 <see cref="Add(string)"/>만 쓰면 자동).</summary>
        public void MarkDirty() => version++;

        void EnsureCache()
        {
            int v = Version;
            if (v == cachedVersion && countCache != null) return;
            cachedVersion = v;
            countCache ??= new Dictionary<string, int>();
            countCache.Clear();
            int n = System.Enum.GetValues(typeof(ItemStat)).Length;
            if (addCache == null || addCache.Length != n) { addCache = new float[n]; mulCache = new float[n]; }
            for (int i = 0; i < n; i++) { addCache[i] = 0f; mulCache[i] = 1f; }
            if (stacks == null) return;
            foreach (var s in stacks)
            {
                if (s == null || s.id == null) continue;
                if (!countCache.ContainsKey(s.id)) countCache[s.id] = s.count; // 원본처럼 같은 id가 두 줄이면 앞 줄
                var def = ItemDatabase.Get(s.id);
                if (def == null || def.kind != ItemKind.Stat) continue;
                int k = (int)def.stat;
                if (def.multiplicative) mulCache[k] *= Mathf.Pow(def.per, s.count);
                else addCache[k] += s.count * def.per;
            }
        }

        public int Count(string id)
        {
            if (id == null) return 0;
            EnsureCache();
            return countCache.TryGetValue(id, out var n) ? n : 0;
        }

        /// <summary>중복 상한에 도달했는지. 원본 `itemCount(id) < itemCap(id)`(`:6545`).</summary>
        public bool IsFull(string id) => Count(id) >= ItemDatabase.CapOf(id);

        /// <summary>한 개 추가한다. 상한을 넘기지 않는다.</summary>
        /// <returns>이번에 처음 얻은 아이템이면 true.</returns>
        public bool Add(string id)
        {
            foreach (var s in stacks)
            {
                if (s.id != id) continue;
                if (s.count < ItemDatabase.CapOf(id)) { s.count++; version++; }
                return false;
            }
            stacks.Add(new ItemStack { id = id, count = 1 });
            version++;
            return true;
        }

        /// <summary>
        /// 뽑기로 하나 얻었다 — 아이템을 더하고 천장을 갱신한다. 천장은 **영웅 이상이 나오면 초기화**, 아니면 1 오른다(원본 `:6583`).
        /// </summary>
        public void RecordPull(ItemDef def)
        {
            Add(def.id);
            pity = (def.grade == ItemGrade.Epic || def.grade == ItemGrade.Legend) ? 0 : pity + 1;
        }

        /// <summary>원본 `itemPow(id) = itemCount(id) * per`(`:1187`) — 특수 아이템의 총 세기.</summary>
        public float Pow(string id)
        {
            var def = ItemDatabase.Get(id);
            return def == null ? 0f : Count(id) * def.per;
        }

        /// <summary>원본 `itemAdd(stat)`(`:1189`) — 합연산 스탯 아이템의 총합.</summary>
        public float Add(ItemStat stat)
        {
            EnsureCache();
            return addCache[(int)stat];
        }

        /// <summary>원본 `itemMul(stat)`(`:1199`) — 곱연산 스탯 아이템의 총 배수.</summary>
        public float Mul(ItemStat stat)
        {
            EnsureCache();
            return mulCache[(int)stat];
        }

        /// <summary>보유 수량 기준 현재 효과값(표시용). 원본 `itemCurValue`(`:1208`).</summary>
        public float CurrentValue(string id)
        {
            var def = ItemDatabase.Get(id);
            if (def == null) return 0f;
            int n = Count(id);
            return def.multiplicative ? Mathf.Pow(def.per, n) : n * def.per;
        }
    }
}
