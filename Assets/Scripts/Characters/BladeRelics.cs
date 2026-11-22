using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 섬영 전용 유물 6종 — **2026-11-22 재설계 2차**(사용자 "섬영은 X가 아니라 기본공격에 특별한 효과" — GPT와 정함).
    /// 전부 Z 회전베기에 붙는다. 갈래 유물은 "그 갈래에서 Z가 어떻게 달라지나".
    /// - 잔영 칼끝(기본공격): 회전베기에 맞은 적을 베기 중심으로 끌어당긴다(보스·통제 면역 제외).
    /// - 집중의 심장(집중): 집중 중 Z 흡혈 때 앞뒤로 초승달.
    /// - 질주 찢개(집중 영웅): 집중 중 Z를 n번 맞히면 남은 궤적을 바로 찢는다(궤적을 다시 깔며 계속 찢는 운영).
    /// - 잔영 추격(집중 전설): 집중 중 Z·궤적으로 처치하면 유도 초승달(한 번 휘두를 때 한 번).
    /// - 폭풍 칼집(폭풍): 폭풍 갈래에서 Z(와 칼날폭풍) 처치 n번마다 부채꼴 초승달.
    /// - 무한 검무(폭풍 전설): 폭풍 갈래에서 Z 적중 n번마다 검령 1개, 칼날폭풍이 끝나면 넓은 부채꼴로.
    /// 세트 3: Z 적중 세 번마다 초승달 하나가 따라 나간다. 세트 5: 집중·칼날폭풍 중 회전베기 적중을 모아 끝날 때 초승달로(최대 15).
    /// 그림은 섬영 킷과 같은 결(초승달·번개 베기, 흰색).
    /// </summary>
    public static class BladeRelics
    {
        const float WaveSpeed = 13f, WaveLife = 0.55f;
        static readonly Color Tint = Color.white;
        static readonly Color EchoTint = new Color(0.8f, 0.95f, 1f, 0.7f);
        static int stormKills, spirits, spiritHits, ripHits, setHits, skillHits;
        static bool inSkill;
        static float nextPursuit;

        public static void ResetForRun() { stormKills = spirits = spiritHits = ripHits = setHits = skillHits = 0; inSkill = false; nextPursuit = 0f; }

        /// <summary>집중·칼날폭풍이 시작됐다 — 세트 5가 회전베기 적중을 모으기 시작.</summary>
        public static void OnSkillStart() { inSkill = true; skillHits = 0; ripHits = 0; } // 질주 찢개는 집중마다 새로 센다

        // ── 잔영 칼끝 — 회전베기 당김 ──

        /// <summary>Z 회전베기에 맞은 적을 중심으로 끌어당긴다(랭크별 세기 0.5/0.75/1).</summary>
        public static void PullOnSpin(Collider2D col, Vector2 center)
        {
            if (col == null || Relics.Rank("afterEdge") <= 0) return;
            if (col.GetComponent<ICrowdControlImmune>() != null) return;
            var g = col.GetComponent<IGravityAffectable>();
            if (g == null) return;
            Vector2 to = center - (Vector2)col.transform.position;
            float d = to.magnitude;
            if (d < 0.15f) return;
            float step = Mathf.Min(d - 0.1f, Relics.V("afterEdge", 0) * 0.45f);
            g.ApplyGravityWellPull(to / d * step * new Vector2(1f, 0.3f));
        }

        // ── 회전베기 한 번이 끝났다 ──

        /// <summary>
        /// Z 회전베기(또는 칼날폭풍 유지 베기)가 적을 맞혔다. 질주 찢개가 궤적을 찢어야 하면 true.
        /// </summary>
        public static bool OnSpinHits(Vector2 pos, int facing, int hitCount, bool isZ, bool focusing, bool stormBranch, bool hasTrail = true)
        {
            if (hitCount <= 0 || Relics.InRelicDamage) return false;
            bool rip = false;

            // 세트 3 — 적중 세 번마다 초승달 하나(Z·칼날폭풍 유지 베기 모두)
            if (Relics.CharacterSet3 && ++setHits >= 3)
            {
                setHits = 0;
                Wave(pos, new Vector2(facing, 0f), 0.35f, Tint);
            }
            // 세트 5 — 집중·칼날폭풍 중 적중을 모은다
            if (inSkill) skillHits += hitCount;

            if (isZ && focusing && hasTrail && Relics.Rank("dashRipper") > 0 && ++ripHits >= Relics.VI("dashRipper", 0)) // 찢을 궤적이 있을 때만 센다
            {
                ripHits = 0;
                Relics.Proc("dashRipper");
                rip = true;
            }
            if (isZ && stormBranch && Relics.Rank("endlessDance") > 0 && ++spiritHits >= Relics.VI("endlessDance", 0))
            {
                spiritHits = 0;
                spirits = Mathf.Min(spirits + 1, 15);
            }
            return rip;
        }

        /// <summary>질주 찢개가 부른 찢기의 피해·반경 배수(아니면 1).</summary>
        public static float RelicRipDamageMult => Relics.Rank("dashRipper") > 0 ? Relics.V("dashRipper", 1) : 1f;
        public static float RelicRipRadiusMult => Relics.Rank("dashRipper") > 0 ? Relics.V("dashRipper", 2) : 1f;

        /// <summary>집중 중 Z 흡혈 — 집중의 심장: 앞뒤로 초승달.</summary>
        public static void OnFocusLifesteal(Transform player, int facing)
        {
            if (Relics.InRelicDamage || player == null || Relics.Rank("focusHeart") <= 0) return;
            Relics.Proc("focusHeart");
            int n = Relics.VI("focusHeart", 0);
            float mult = Relics.V("focusHeart", 1);
            Vector2 pos = player.position;
            for (int i = 0; i < n; i++)
                Wave(pos, new Vector2(i % 2 == 0 ? facing : -facing, 0f), mult, Tint);
            if (Relics.Awakened("focusHeart")) RelicFx.After(0.2f, () => Wave(pos, new Vector2(facing, 0f), mult * 0.35f, EchoTint));
        }

        /// <summary>집중 중 Z·궤적으로 적을 쓰러뜨렸다 — 잔영 추격(0.25초에 한 번 — 한 번 휘두를·궤적 한 틱에 한 번).</summary>
        public static void OnFocusKill(Vector2 pos, int facing)
        {
            if (Relics.InRelicDamage || Relics.Rank("echoPursuit") <= 0 || Time.time < nextPursuit) return;
            nextPursuit = Time.time + 0.25f;
            Relics.Proc("echoPursuit");
            int n = Relics.VI("echoPursuit", 0);
            float mult = Relics.V("echoPursuit", 1);
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, 360f * i / n) * new Vector2(facing, 0f);
                RelicProjectile.Spawn(pos + new Vector2(0f, 0.35f), dir, WaveSpeed, 0.9f, mult, RelicProjectile.Mode.Homing,
                                      VfxKeys.AssassinCrescent, Tint, 0.55f, -1, artScale: 0.5f);
            }
            VfxPlayer.Spawn(VfxKeys.AssassinBurstSpark, pos, DotScale.Value, 15f, RelicFx.SortingOrder);
        }

        /// <summary>폭풍 갈래에서 Z·칼날폭풍으로 쓰러뜨렸다 — 폭풍 칼집.</summary>
        public static void OnStormKill(Vector2 pos, int facing)
        {
            if (Relics.InRelicDamage || Relics.Rank("stormSheath") <= 0) return;
            if (++stormKills < Relics.VI("stormSheath", 0)) return;
            stormKills = 0;
            Relics.Proc("stormSheath");
            int n = Relics.VI("stormSheath", 1);
            float mult = Relics.V("stormSheath", 2);
            Fan(pos, facing, n, 70f, mult, Tint);
            VfxPlayer.Spawn(VfxKeys.AssassinBolt1, pos + Vector2.up * 0.4f, DotScale.Value, 15f, RelicFx.SortingOrder);
            if (Relics.Awakened("stormSheath")) RelicFx.After(0.2f, () => Fan(pos, facing, n, 70f, mult * 0.35f, EchoTint));
        }

        /// <summary>집중이 끝났다 — 세트 5: 모은 적중만큼 초승달(최대 15).</summary>
        public static void OnFocusEnd(Vector2 pos, int facing) => ReleaseSkillHits(pos, facing);

        /// <summary>칼날폭풍이 끝났다 — 무한 검무(검령 부채꼴) + 세트 5.</summary>
        public static void OnStormEnd(Vector2 pos, int facing)
        {
            if (spirits > 0 && Relics.Rank("endlessDance") > 0)
            {
                int n = spirits;
                Relics.Proc("endlessDance");
                float mult = Relics.V("endlessDance", 1);
                Fan(pos, facing, n, 150f, mult, Tint);
                VfxPlayer.Spawn(VfxKeys.AssassinBolt2, pos + Vector2.up * 0.4f, DotScale.Value, 15f, RelicFx.SortingOrder);
                if (Relics.Awakened("endlessDance")) RelicFx.After(0.2f, () => Fan(pos, facing, n, 150f, mult * 0.35f, EchoTint));
                HitFeel.Shake(5f, 0.2f);
            }
            spirits = 0;
            ReleaseSkillHits(pos, facing);
        }

        /// <summary>지금 모인 검령(테스트용).</summary>
        public static int Spirits => spirits;

        static void ReleaseSkillHits(Vector2 pos, int facing)
        {
            if (inSkill && Relics.CharacterSet5 && skillHits > 0)
            {
                Fan(pos, -facing, Mathf.Min(skillHits, 15), 200f, 0.35f, Tint);
                VfxPlayer.Spawn(VfxKeys.AssassinBolt1, pos + Vector2.up * 0.4f, DotScale.Value, 15f, RelicFx.SortingOrder);
            }
            inSkill = false;
            skillHits = 0;
        }

        static void Fan(Vector2 pos, int facing, int n, float spreadDeg, float mult, Color tint)
        {
            Vector2 fwd = new Vector2(facing, 0f);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : i / (float)(n - 1) - 0.5f;
                Vector2 dir = Quaternion.Euler(0f, 0f, t * spreadDeg) * fwd;
                Wave(pos, dir, mult, tint);
            }
        }

        // 초승달(섬영 칼날 그림, 원래 색) — 관통, 0.55초
        static void Wave(Vector2 pos, Vector2 dir, float mult, Color tint) =>
            RelicProjectile.Spawn(pos + new Vector2(0f, 0.35f), dir, WaveSpeed, WaveLife, mult, RelicProjectile.Mode.Straight,
                                  VfxKeys.AssassinCrescent, tint, 0.55f, -1, artScale: 0.5f);
    }
}
