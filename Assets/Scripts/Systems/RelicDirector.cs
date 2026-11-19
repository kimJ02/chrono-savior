using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Combat;
using ChronoSavior.Enemies;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 처치·사냥 흐름에서 발동하는 유물(2026-11-19 테스트 유물, <see cref="ItemDatabase"/>) — <see cref="CombatDirector"/>가 처치마다 부른다.
    /// 공격 자체를 바꾸는 캐릭터 유물은 그 공격 코드(MageAttack·Gunner…·BladeCombat)에 있다.
    /// 유물 피해로 난 처치는 다른 유물을 발동시키지 않는다(<see cref="Relics.InRelicDamage"/>).
    /// </summary>
    public static class RelicDirector
    {
        /// <summary>새 사냥 시작 — 런마다 세는 값을 되돌린다.</summary>
        public static void ResetForRun()
        {
        }

        /// <summary>적이 쓰러졌다(성소·보스 포함).</summary>
        public static void OnEnemyKilled(GameObject enemy)
        {
            if (enemy == null) return;
            var eh = enemy.GetComponent<EnemyHealth>();
            Vector2 pos = enemy.TryGetComponent(out Collider2D col) ? (Vector2)col.bounds.center : (Vector2)enemy.transform.position;

            // 불씨 장부(마법사 폭발) — 불타던 적이 쓰러지면 시체가 폭발한다. 유물 피해로 쓰러진 것도 터진다(연쇄 폭발이 이 유물의 재미).
            if (eh != null && eh.BurnStacks > 0 && Relics.Rank("emberLedger") > 0)
            {
                float r = Relics.V("emberLedger", 0), mult = Relics.V("emberLedger", 1);
                bool gold = Relics.Awakened("emberLedger");
                RelicFx.After(0.05f, () =>
                {
                    RelicFx.Circle(pos, r, mult, RelicFx.Fire, VfxKeys.MageFireExplosion, onHit: c => c.GetComponent<IElementAfflictable>()?.ApplyBurn(1));
                    if (gold) RelicFx.After(0.2f, () => RelicFx.Circle(pos, r, mult * 0.35f, RelicFx.Gold, VfxKeys.MageFireExplosion));
                });
            }
        }
    }
}
