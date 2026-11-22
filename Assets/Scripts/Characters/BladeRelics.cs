using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 섬영 전용 유물 6종 — **2026-11-22 재설계**(사용자 "유물이 직업의 재미를 살리지 않는다, 섬영은 이펙트도 구리다" — GPT와 정함).
    /// 시그니처(집중 = 질주 궤적·파열, 칼날폭풍 = 유지 회전베기·착지·연행 낙하)를 키우고, Z(회전 베기) 자체엔 피해를 더하지 않는다.
    /// - 잔영 칼끝(기본공격): X(집중·칼날폭풍) 쿨타임 감소.
    /// - 집중의 심장(집중): 궤적 피해·반경 + 집중 중 흡혈 회전베기 때 초승달.
    /// - 질주 찢개(집중 영웅): 집중이 끝나면 궤적을 바로 찢고, 찢기 피해·반경 증가.
    /// - 잔영 추격(집중 전설, 새로): 궤적·파열로 처치하면 초승달 추격.
    /// - 폭풍 칼집(폭풍): 칼날폭풍 처치(유지 틱·착지·낙하)를 모아 초승달 부채꼴 — Z 처치는 안 센다.
    /// - 무한 검무(폭풍 전설): 붙잡을 때마다 검령, 칼날폭풍이 끝나면 넓은 부채꼴.
    /// 세트 3: 집중 중 궤적 반경 ×1.25·피해 ×1.5 / 칼날폭풍 유지 피해 ×1.5·착지 반경 ×1.25.
    /// 세트 5: 집중 끝 궤적 파열 피해 ×1.5 / 칼날폭풍 끝에 그동안 처치 수만큼 초승달(최대 15).
    /// 그림은 섬영 킷과 같은 결(초승달·번개 베기, 흰색) — 예전 금색 고리·선은 쓰지 않는다.
    /// </summary>
    public static class BladeRelics
    {
        const float WaveSpeed = 13f, WaveLife = 0.55f;
        static readonly Color Tint = Color.white;
        static readonly Color EchoTint = new Color(0.8f, 0.95f, 1f, 0.7f);
        static int stormKills, spirits, skillKills;
        static bool inSkill;
        static float nextPursuit;

        public static void ResetForRun() { stormKills = spirits = skillKills = 0; inSkill = false; nextPursuit = 0f; }

        // ── 시그니처 수치(BladeCombat이 읽는다, 꺼져 있으면 1·0) ──
        public static float SkillCdMult => 1f - Relics.V("afterEdge", 0);
        public static float TrailDamageMult(bool focusing) =>
            (Relics.Rank("focusHeart") > 0 ? Relics.V("focusHeart", 0) : 1f) * (focusing && Relics.CharacterSet3 ? 1.5f : 1f);
        public static float TrailRadiusBonus => Relics.V("focusHeart", 1);
        public static float TrailRadiusMult(bool focusing) => focusing && Relics.CharacterSet3 ? 1.25f : 1f;
        public static float RipDamageMult =>
            (Relics.Rank("dashRipper") > 0 ? Relics.V("dashRipper", 0) : 1f) * (Relics.CharacterSet5 ? 1.5f : 1f);
        public static float RipRadiusMult => Relics.Rank("dashRipper") > 0 ? Relics.V("dashRipper", 1) : 1f;
        /// <summary>집중이 끝나는 순간 궤적을 바로 찢는가(질주 찢개·세트 5) — 층과 상관없이 찢기 피해가 난다.</summary>
        public static bool RipOnFocusEnd => Relics.Rank("dashRipper") > 0 || Relics.CharacterSet5;
        public static float StormSpinMult => Relics.CharacterSet3 ? 1.5f : 1f;
        public static float StormLandRadiusMult => Relics.CharacterSet3 ? 1.25f : 1f;

        /// <summary>X(집중·칼날폭풍)가 시작됐다 — 세트 5가 X 중 처치를 센다.</summary>
        public static void OnSkillStart() { inSkill = true; skillKills = 0; }

        /// <summary>집중 중 흡혈 회전베기 — 집중의 심장: 앞뒤로 초승달.</summary>
        public static void OnFocusLifesteal(Transform player, int facing)
        {
            if (Relics.InRelicDamage || player == null || Relics.Rank("focusHeart") <= 0) return;
            Relics.Proc("focusHeart");
            int n = Relics.VI("focusHeart", 2);
            float mult = Relics.V("focusHeart", 3);
            Vector2 pos = player.position;
            for (int i = 0; i < n; i++)
                Wave(pos, new Vector2(i % 2 == 0 ? facing : -facing, 0f), mult, Tint);
            if (Relics.Awakened("focusHeart")) RelicFx.After(0.2f, () => Wave(pos, new Vector2(facing, 0f), mult * 0.35f, EchoTint));
        }

        /// <summary>궤적·파열로 적을 쓰러뜨렸다 — 잔영 추격(같은 순간 여러 마리여도 0.12초에 한 번).</summary>
        public static void OnTrailKill(Vector2 pos, int facing)
        {
            if (Relics.InRelicDamage) return;
            if (inSkill) skillKills++;
            if (Relics.Rank("echoPursuit") <= 0 || Time.time < nextPursuit) return;
            nextPursuit = Time.time + 0.12f;
            Relics.Proc("echoPursuit");
            int n = Relics.VI("echoPursuit", 0);
            float mult = Relics.V("echoPursuit", 1);
            Fan(pos, facing, n, 360f - 360f / Mathf.Max(1, n), mult, Tint); // 사방으로
            VfxPlayer.Spawn(VfxKeys.AssassinBurstSpark, pos, DotScale.Value, 15f, RelicFx.SortingOrder);
        }

        /// <summary>칼날폭풍(유지 틱·착지·연행 낙하)으로 쓰러뜨렸다 — 폭풍 칼집.</summary>
        public static void OnStormKill(Vector2 pos, int facing)
        {
            if (Relics.InRelicDamage) return;
            if (inSkill) skillKills++;
            if (Relics.Rank("stormSheath") <= 0) return;
            if (++stormKills < Relics.VI("stormSheath", 0)) return;
            stormKills = 0;
            Relics.Proc("stormSheath");
            int n = Relics.VI("stormSheath", 1);
            float mult = Relics.V("stormSheath", 2);
            Fan(pos, facing, n, 80f, mult, Tint);
            VfxPlayer.Spawn(VfxKeys.AssassinBolt1, pos + Vector2.up * 0.4f, DotScale.Value, 15f, RelicFx.SortingOrder);
            if (Relics.Awakened("stormSheath")) RelicFx.After(0.2f, () => Fan(pos, facing, n, 80f, mult * 0.35f, EchoTint));
        }

        /// <summary>집중이 끝났다 — 세트 5 카운트 정리(찢기는 BladeCombat이 <see cref="RipOnFocusEnd"/>를 보고 한다).</summary>
        public static void OnFocusEnd() { inSkill = false; skillKills = 0; }

        /// <summary>칼날폭풍으로 적을 붙잡았다 — 무한 검무 검령 저장.</summary>
        public static void OnGrab()
        {
            if (Relics.Rank("endlessDance") <= 0) return;
            spirits = Mathf.Min(spirits + Relics.VI("endlessDance", 0), 15);
        }

        /// <summary>칼날폭풍이 끝났다 — 무한 검무(검령 부채꼴) + 세트 5(그동안 처치 수만큼 초승달, 최대 15).</summary>
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
            if (inSkill && Relics.CharacterSet5 && skillKills > 0)
            {
                Fan(pos, -facing, Mathf.Min(skillKills, 15), 200f, 0.4f, Tint);
                VfxPlayer.Spawn(VfxKeys.AssassinBolt1, pos + Vector2.up * 0.4f, DotScale.Value, 15f, RelicFx.SortingOrder);
            }
            inSkill = false;
            skillKills = 0;
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
