using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 가챠(기원) — 원본 `rollItem`/`rollFrom`/`doGacha`(project_test.html:6541~:6588).
    ///
    /// ## 등급을 먼저 뽑고 그 안에서 균등하게 고른다
    /// 원본 주석 그대로 "아이템을 등급에 몇 개 넣든 표기 확률이 그대로 유지된다 — 등급이 곧 확률
    /// 조절 손잡이"(`:6551`). 아이템별로 가중치를 주면 등급에 아이템을 추가할 때마다 표기 확률이
    /// 조용히 틀어진다.
    ///
    /// ## 상한에 찬 아이템은 후보에서 빠진다
    /// 원본 `:6544` — "다 찬 뒤에도 계속 나와서 허탕 치는 일을 막는다".
    /// 후보가 남은 등급끼리만 가중 추첨하므로, **빈 등급의 확률은 남은 등급들이 비례로 나눠 갖는다.**
    ///
    /// ## 천장
    /// 영웅+가 20회 연속 안 나오면 다음 뽑기는 영웅 이상 확정(`PITY_AT`, `:6540`).
    /// </summary>
    public static class GachaService
    {
        /// <summary>1회 비용(시간의 파편). 원본 `GACHA_COST = 10`.</summary>
        public const int Cost = 10;
        /// <summary>이만큼 연속으로 영웅+가 안 나오면 확정. 원본 `PITY_AT = 20`.</summary>
        public const int PityAt = 20;

        /// <summary>
        /// 후보 목록 — 상한에 안 찬 것들. <paramref name="minGrade"/>가 있으면 그 이상만.
        /// </summary>
        public static List<ItemDef> Pool(ItemInventory inv, ItemGrade? minGrade = null)
        {
            var pool = new List<ItemDef>();
            foreach (var def in ItemDatabase.All)
            {
                if (inv.IsFull(def.id)) continue;
                if (minGrade.HasValue && def.grade < minGrade.Value) continue;
                pool.Add(def);
            }
            return pool;
        }

        /// <summary>
        /// 한 개 굴린다. 더 줄 게 없으면(전부 상한) null.
        /// 천장이 찼으면 영웅 이상에서만 뽑되, **영웅+가 다 찼으면 전체 후보로 되돌아간다**
        /// (원본 `if (!pool.length) return rollFrom(gachaPool(null))`).
        /// </summary>
        public static ItemDef Roll(ItemInventory inv)
        {
            bool forced = inv.pity >= PityAt;
            var pool = Pool(inv, forced ? ItemGrade.Epic : (ItemGrade?)null);
            if (pool.Count == 0) pool = Pool(inv);
            return RollFrom(pool);
        }

        /// <summary>후보가 남은 등급만 모아 가중 추첨하고, 그 등급 안에서는 균등하게 고른다.</summary>
        public static ItemDef RollFrom(List<ItemDef> pool)
        {
            if (pool == null || pool.Count == 0) return null;

            var byGrade = new Dictionary<ItemGrade, List<ItemDef>>();
            foreach (var d in pool)
            {
                if (!byGrade.TryGetValue(d.grade, out var list))
                    byGrade[d.grade] = list = new List<ItemDef>();
                list.Add(d);
            }

            int total = 0;
            foreach (var kv in byGrade) total += ItemDatabase.Weight(kv.Key);
            if (total <= 0) return pool[Random.Range(0, pool.Count)];

            float r = Random.value * total;
            foreach (var kv in byGrade)
            {
                r -= ItemDatabase.Weight(kv.Key);
                if (r <= 0f) return kv.Value[Random.Range(0, kv.Value.Count)];
            }

            // 부동소수 오차로 아무 등급도 안 걸렸을 때의 대비(원본도 마지막 등급으로 떨어뜨린다).
            foreach (var kv in byGrade) return kv.Value[Random.Range(0, kv.Value.Count)];
            return null;
        }

        /// <summary>
        /// n회 뽑는다. 포인트가 모자라거나 더 줄 게 없으면 거기서 멈춘다(원본 `doGacha` `:6574`).
        /// </summary>
        /// <returns>뽑힌 아이템들(순서대로). 비어 있으면 아무것도 못 뽑은 것.</returns>
        public static List<ItemDef> Pull(PlayerProfile profile, int n)
        {
            var got = new List<ItemDef>();
            var inv = profile.items;

            for (int i = 0; i < n; i++)
            {
                if (profile.shards < Cost) break;
                var def = Roll(inv);
                if (def == null) break; // 전 아이템 상한 — 더 줄 게 없다

                profile.shards -= Cost;
                inv.Add(def.id);
                // 천장은 **영웅 이상이 나오면 초기화**된다(원본 `:6583`).
                inv.pity = (def.grade == ItemGrade.Epic || def.grade == ItemGrade.Legend) ? 0 : inv.pity + 1;
                profile.stats.pulls++;
                got.Add(def);
            }
            if (got.Count > 0) Achievements.CheckNew(profile);
            return got;
        }
    }
}
