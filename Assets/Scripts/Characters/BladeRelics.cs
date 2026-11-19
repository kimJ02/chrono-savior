using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 섬영 전용 유물 5종(2026-11-19 테스트 유물, <see cref="ItemDatabase"/>) — <see cref="BladeCombat"/>이 회전 베기·집중·칼날폭풍에서 부른다.
    /// - 잔영 칼끝(기본공격): 회전 베기 n번마다 앞으로 날아가는 초승달 칼날.
    /// - 집중의 심장(집중): 흡혈이 터질 때마다 칼날 파동(3랭크는 갔다 돌아오는 칼날 2발).
    /// - 질주 찢개(집중 영웅): 집중이 끝나면 달린 길이 찢어지며 베기.
    /// - 폭풍 칼집(폭풍): 회전 베기 처치를 모아 충전, 3번 차면 칼날 부채꼴.
    /// - 무한 검무(폭풍 전설): 칼날폭풍으로 붙잡을 때마다 검령을 모아 칼날폭풍이 끝나면 넓은 부채꼴로.
    /// </summary>
    public static class BladeRelics
    {
        const float WaveSpeed = 13f, WaveLife = 0.55f;
        static int spins, stormKills, stormCharges, spirits, skillKills;
        static bool inSkill;
        static Vector2 focusStart;
        static bool focusTracking;

        public static void ResetForRun() { spins = stormKills = stormCharges = spirits = skillKills = 0; focusTracking = false; inSkill = false; }

        /// <summary>X(집중·칼날폭풍)가 시작됐다 — 섬영 세트 5가 X 중 처치를 센다.</summary>
        public static void OnSkillStart() { inSkill = true; skillKills = 0; }

        /// <summary>
        /// X가 끝났다 — 섬영 세트 3: 끝난 자리에 유령 베기(공격력 60%). 세트 5: X 중 처치 3마리마다 검령 1개, 끝날 때 부채꼴(40%).
        /// </summary>
        public static void OnSkillEnd(Vector2 pos, int facing)
        {
            if (!inSkill) return;
            inSkill = false;
            if (Relics.CharacterSet3)
                RelicFx.Circle(pos + Vector2.up * 0.4f, 1.8f, 0.6f, RelicFx.Gold, VfxKeys.AssassinSpin);
            if (Relics.CharacterSet5 && skillKills >= 3)
                Fan(pos, facing, Mathf.Min(skillKills / 3, 8), 120f, 0.4f, RelicFx.Gold);
            skillKills = 0;
        }

        /// <summary>Z 회전 베기 한 번(맞았든 안 맞았든) — 잔영 칼끝.</summary>
        public static void OnSpin(Vector2 pos, int facing)
        {
            if (Relics.Rank("afterEdge") <= 0) return;
            if (++spins < Relics.VI("afterEdge", 0)) return;
            spins = 0;
            float mult = Relics.V("afterEdge", 1);
            Wave(pos, new Vector2(facing, 0f), mult, RelicFx.Cyan, RelicProjectile.Mode.Straight);
            if (Relics.Awakened("afterEdge")) RelicFx.After(0.2f, () => Wave(pos, new Vector2(facing, 0f), mult * 0.35f, RelicFx.Gold, RelicProjectile.Mode.Straight));
        }

        /// <summary>흡혈이 터졌다 — 집중의 심장.</summary>
        public static void OnLifesteal(Transform player, int facing)
        {
            if (Relics.Rank("focusHeart") <= 0) return;
            int n = Relics.VI("focusHeart", 0);
            float mult = Relics.V("focusHeart", 1) * (Relics.Awakened("focusHeart") ? 1.35f : 1f); // 각성 — 위력 +35%
            bool boomerang = Relics.Rank("focusHeart") >= 3;
            Vector2 pos = player.position;
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = new Vector2(i % 2 == 0 ? facing : -facing, 0f);
                Wave(pos, dir, mult, Relics.Awakened("focusHeart") ? RelicFx.Gold : RelicFx.Cyan,
                     boomerang ? RelicProjectile.Mode.Boomerang : RelicProjectile.Mode.Straight, player, boomerang ? 1.1f : WaveLife);
            }
        }

        /// <summary>회전 베기로 적을 쓰러뜨렸다(Z·칼날폭풍 유지 틱 모두) — 폭풍 칼집.</summary>
        public static void OnSpinKill(Vector2 pos, int facing)
        {
            if (Relics.InRelicDamage) return;
            if (inSkill) skillKills++;
            if (Relics.Rank("stormSheath") <= 0) return;
            if (++stormKills < Relics.VI("stormSheath", 0)) return;
            stormKills = 0;
            if (++stormCharges < 3) return;
            stormCharges = 0;
            int n = Relics.VI("stormSheath", 1);
            float mult = Relics.V("stormSheath", 2);
            Fan(pos, facing, n, 70f, mult, RelicFx.Cyan);
            if (Relics.Awakened("stormSheath")) RelicFx.After(0.2f, () => Fan(pos, facing, n, 70f, mult * 0.35f, RelicFx.Gold));
        }

        /// <summary>집중이 켜지고 꺼질 때 — 질주 찢개(꺼질 때 달린 길을 벤다).</summary>
        public static void OnFocusChanged(bool on, Vector2 pos)
        {
            if (on) { focusStart = pos; focusTracking = true; OnSkillStart(); return; }
            OnSkillEnd(pos, pos.x >= focusStart.x ? 1 : -1);
            if (!focusTracking) return;
            focusTracking = false;
            if (Relics.Rank("dashRipper") <= 0) return;
            if ((pos - focusStart).sqrMagnitude < 1f) return; // 거의 안 달렸으면 없음
            int n = Relics.VI("dashRipper", 0);
            float mult = Relics.V("dashRipper", 1);
            bool gold = Relics.Awakened("dashRipper");
            Vector2 a = focusStart, b = pos;
            for (int i = 0; i < n; i++)
            {
                Vector2 off = new Vector2(0f, (i - (n - 1) * 0.5f) * 0.45f + 0.35f);
                RelicFx.Line(a + off, b + off, 0.6f, mult, RelicFx.Cyan, 0.3f);
                if (gold) RelicFx.After(0.2f, () => RelicFx.Line(a + off, b + off, 0.6f, mult * 0.35f, RelicFx.Gold, 0.3f));
            }
            VfxPlayer.Spawn(VfxKeys.AssassinBolt1, (a + b) * 0.5f, DotScale.Value, 15f, RelicFx.SortingOrder);
        }

        /// <summary>칼날폭풍으로 적을 붙잡았다 — 무한 검무 검령 저장.</summary>
        public static void OnGrab()
        {
            if (Relics.Rank("endlessDance") <= 0) return;
            spirits = Mathf.Min(spirits + Relics.VI("endlessDance", 0), 15);
        }

        /// <summary>칼날폭풍이 끝났다 — 모은 검령을 부채꼴로.</summary>
        public static void OnStormEnd(Vector2 pos, int facing)
        {
            OnSkillEnd(pos, facing);
            if (spirits <= 0) return;
            int n = spirits;
            spirits = 0;
            if (Relics.Rank("endlessDance") <= 0) return;
            float mult = Relics.V("endlessDance", 1);
            Fan(pos, facing, n, 150f, mult, new Color(0.7f, 0.9f, 1f, 0.9f));
            if (Relics.Awakened("endlessDance")) RelicFx.After(0.2f, () => Fan(pos, facing, n, 150f, mult * 0.35f, RelicFx.Gold));
            HitFeel.Shake(5f, 0.2f);
        }

        static void Fan(Vector2 pos, int facing, int n, float spreadDeg, float mult, Color tint)
        {
            Vector2 fwd = new Vector2(facing, 0f);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : i / (float)(n - 1) - 0.5f;
                Vector2 dir = Quaternion.Euler(0f, 0f, t * spreadDeg) * fwd;
                Wave(pos, dir, mult, tint, RelicProjectile.Mode.Straight);
            }
        }

        static void Wave(Vector2 pos, Vector2 dir, float mult, Color tint, RelicProjectile.Mode mode, Transform owner = null, float life = WaveLife) =>
            RelicProjectile.Spawn(pos + new Vector2(0f, 0.35f), dir, WaveSpeed, life, mult, mode, VfxKeys.AssassinCrescent, tint,
                                  0.55f, -1, owner: owner, artScale: 0.5f);
    }
}
