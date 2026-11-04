using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 스킬(빔·드론·설치기·장판)이 같이 쓰는 "맞힐 수 있는 적" 조회.
    ///
    /// 원본은 전역 `enemies` 배열을 돌며 `e.dead || e.spawnInvuln > 0`인 적을 거르고, 위치는 **몸통 중심**
    /// (`e.x`, `e.y - e.h/2`), 크기는 **폭** `e.w`를 쓴다. Unity엔 그런 전역 배열이 없어서 Physics2D 원 조회로
    /// 후보를 모은 뒤 같은 조건으로 거른다 — `Characters`는 `Enemies`를 참조할 수 없으므로(asmdef 계층 규칙)
    /// 태그 `Enemy`와 `Core`의 `IDamageable`·`ISpawnProtectable`만 쓴다.
    /// </summary>
    public static class GunnerTargets
    {
        public struct Target
        {
            public Collider2D collider;
            public IDamageable damageable;
            /// <summary>몸통 중심 — 원본 `(e.x, e.y - e.h/2)`.</summary>
            public Vector2 center;
            /// <summary>폭 — 원본 `e.w`.</summary>
            public float width;
        }

        /// <summary>
        /// 조회 반경에 더하는 여유. 판정은 "몸통 중심이 범위 안"으로 따로 하므로, 콜라이더가 조회 원에
        /// 걸치기만 하면 되는 Physics2D 조회 쪽은 넉넉하게 잡는다(보스·엘리트처럼 큰 적 포함).
        /// </summary>
        public const float QueryMargin = 1.5f;

        /// <summary>
        /// <paramref name="center"/>에서 <paramref name="radius"/> 안에 콜라이더가 걸치는 유효한 적 전부.
        /// 같은 적이 콜라이더를 여러 개 갖고 있어도 한 번만 들어간다. 호출할 때마다 새 목록을 돌려준다.
        /// </summary>
        public static List<Target> Query(Vector2 center, float radius)
        {
            var list = new List<Target>();
            var seen = new HashSet<IDamageable>();
            foreach (var col in Physics2D.OverlapCircleAll(center, radius))
            {
                if (!TryMake(col, out var t)) continue;
                if (!seen.Add(t.damageable)) continue;
                list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// 이 콜라이더가 지금 맞힐 수 있는 적이면 정보를 채워 true. 원본의 `e.dead || e.spawnInvuln > 0` 스킵과 같다.
        /// </summary>
        public static bool TryMake(Collider2D col, out Target target)
        {
            target = default;
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) return false;
            if (!col.CompareTag("Enemy")) return false;
            var dmg = col.GetComponent<IDamageable>();
            if (dmg == null || dmg.IsDead) return false;
            var protectable = col.GetComponent<ISpawnProtectable>();
            if (protectable != null && protectable.IsSpawnProtected) return false;

            var b = col.bounds;
            target = new Target { collider = col, damageable = dmg, center = b.center, width = b.size.x };
            return true;
        }
    }
}
