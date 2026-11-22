using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Combat;
using ChronoSavior.Enemies;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 사냥 흐름에서 발동하는 유물(2026-11-19 테스트 유물, <see cref="ItemDatabase"/>) — <see cref="CombatDirector"/>·<see cref="EnemyRewards"/>가 부른다.
    /// 공격 자체를 바꾸는 캐릭터 유물은 그 공격 코드(MageAttack·GunnerRelics·BladeRelics)에, 피격·사망 유물은 PlayerHealth에 있다.
    ///
    /// - 처치: 등불·월하의 별똥·종이새 떼·종언의 종(n번마다), 정예 사냥종(엘리트), 향로(성소), 유리 서약, 불씨 장부(마법사)
    /// - 연쇄 처치: 편지·연쇄 모래시계·일곱째 날의 왕관
    /// - 콤보: 콤보 종착역 / 입장: 칠일의 모래·문턱의 시계 / 엘리트 첫 피격: 회귀 엽서
    ///
    /// 유물 피해로 난 처치·연쇄는 다른 유물을 다시 발동시키지 않는다(<see cref="Relics.InRelicDamage"/>). 불씨 장부만 예외(연쇄 폭발이 그 유물의 재미).
    /// 각성(랭크 상한)이면 터진 뒤 0.2초에 35% 메아리(<see cref="Echo"/>).
    /// </summary>
    public static class RelicDirector
    {
        public const float AwakenDelay = 0.2f, AwakenPower = 0.35f;

        /// <summary>
        /// 가로로 길게 긋는 선 공격(칠일의 모래·연쇄 모래시계 바닥 파동, 콤보 종착역·일곱째 날의 왕관 화면 베기, 연쇄 세트 4 칼날 줄)을 끈다 —
        /// 2026-11-22 사용자 "횡 범위로 파란·주황 이펙트가 터지는데 없애자" + "받은 VFX 파일을 쓰고, 그림은 검사받고 적용". 이 선은 받은 VFX가 아니라
        /// LineRenderer로 임의로 그린 것이었다. 받은 VFX에서 그림을 골라 사용자 승인을 받으면 다시 켠다(효과째 끔 — 사용자 선택).
        /// </summary>
        public const bool LineEffectsOff = true;

        static int kills, chainKills, lastComboStep;
        static readonly Dictionary<string, int> killCounters = new Dictionary<string, int>();
        static readonly HashSet<GameObject> postcardDone = new HashSet<GameObject>();
        static bool postcardUsedThisStage;

        /// <summary>새 사냥 시작 — 런마다 세는 값을 되돌린다.</summary>
        public static void ResetForRun()
        {
            kills = chainKills = lastComboStep = 0;
            killCounters.Clear();
            postcardDone.Clear();
            postcardUsedThisStage = false;
        }

        static Vector2 PlayerPos => WorldView.Combat != null ? WorldView.Combat.PlayerPosition : Vector2.zero;

        /// <summary>각성이면 0.2초 뒤 35% 위력으로 한 번 더.</summary>
        static void Echo(string id, System.Action<float> fire, float mult)
        {
            Relics.Proc(id);
            fire(mult);
            if (Relics.Awakened(id)) RelicFx.After(AwakenDelay, () => fire(mult * AwakenPower));
        }

        /// <summary>n번마다 — 그 유물의 처치 카운터를 올리고 문턱에 닿으면 true.</summary>
        static bool Every(string id, int k = 0)
        {
            if (Relics.Rank(id) <= 0) return false;
            int every = Mathf.Max(1, Relics.VI(id, k));
            killCounters.TryGetValue(id, out int n);
            n++;
            if (n >= every) { killCounters[id] = 0; return true; }
            killCounters[id] = n;
            return false;
        }

        // ───────────────────────── 입장 ─────────────────────────

        /// <summary>사냥 시작 직후(몹이 나오기 시작할 즈음) — 칠일의 모래·문턱의 시계.</summary>
        public static void OnRunStarted()
        {
            if (Relics.Rank("sevenSands") > 0 || Relics.Rank("thresholdClock") > 0 || Relics.TagCount("시간") >= 2)
                RelicFx.After(2.5f, StageEntryWave);
        }

        static void StageEntryWave()
        {
            Vector2 p = PlayerPos;
            // 시간 세트 2 — 입장 때 첫 무리를 0.5초 늦춘다
            if (Relics.TagCount("시간") >= 2) CombatModifiers.SlowEnemies(0.5f);
            if (Relics.Rank("thresholdClock") > 0)
            {
                CombatModifiers.FreezeEnemies(Relics.V("thresholdClock", 0));
                Echo("thresholdClock", m => RelicFx.Circle(p + Vector2.up * 0.4f, 6f, m, RelicFx.Light, VfxKeys.AssassinLandRing), Relics.V("thresholdClock", 1));
            }
            if (Relics.Rank("sevenSands") > 0)
            {
                float slow = Relics.V("sevenSands", 1);
                if (slow > 0f) CombatModifiers.SlowEnemies(slow);
                if (!LineEffectsOff) Echo("sevenSands", m => GroundSweep(p, 14f, m, new Color(1f, 0.85f, 0.5f, 0.9f)), Relics.V("sevenSands", 0));
            }
        }

        /// <summary>플레이어 높이에서 좌우로 바닥을 쓸어가는 파동(두께 있는 선).</summary>
        static void GroundSweep(Vector2 center, float halfWidth, float mult, Color color)
        {
            Vector2 a = center + new Vector2(-halfWidth, 0.4f), b = center + new Vector2(halfWidth, 0.4f);
            RelicFx.Line(a, b, 1.6f, mult, color, 0.35f);
            VfxPlayer.Spawn(VfxKeys.AssassinLandDust, center, DotScale.Value, 15f, RelicFx.SortingOrder);
            HitFeel.Shake(5f, 0.2f);
        }

        // ───────────────────────── 처치 ─────────────────────────

        /// <summary>적이 쓰러졌다(성소·보스 포함).</summary>
        public static void OnEnemyKilled(GameObject enemy)
        {
            if (enemy == null) return;
            var eh = enemy.GetComponent<EnemyHealth>();
            Vector2 pos = enemy.TryGetComponent(out Collider2D col) ? (Vector2)col.bounds.center : (Vector2)enemy.transform.position;

            // 불씨 장부(마법사 폭발) — 불타던 적이 쓰러지면 시체가 폭발한다. 유물 피해로 쓰러진 것도 터진다(연쇄 폭발이 이 유물의 재미).
            if (eh != null && eh.BurnStacks > 0 && Relics.Rank("emberLedger") > 0)
            {
                float r = Relics.V("emberLedger", 0);
                RelicFx.After(0.05f, () => Echo("emberLedger",
                    m => RelicFx.Circle(pos, r, m, RelicFx.Fire, VfxKeys.MageFireExplosion, onHit: c => c.GetComponent<IElementAfflictable>()?.ApplyBurn(1)),
                    Relics.V("emberLedger", 1)));
            }

            if (Relics.InRelicDamage) return; // 유물이 낸 처치는 아래 유물을 다시 발동시키지 않는다
            // 메카닉·섬영은 시그니처(레이저·설치·집중·칼날폭풍)로 잡은 것만 센다 — Z가 공용 유물로 화면을 먼저 정리하지 않게(2026-11-22, GPT와 정함)
            if (!Relics.KillCountsForCommon(ProfileService.Current)) return;

            bool elite = enemy.GetComponent<EnemyElite>() != null;
            kills++;

            CombatModifiers.GrantGlass(); // 유리 서약 — 처치 시 4초 강화

            // (향로 — 성소 파괴 시 발동 — 는 2026-11-22 성소를 끄면서 능력치 유물 "멸망 전의 잔불"로 바뀌었다)

            // 정예 사냥종 — 엘리트 처치 충격파 + 살아남은 적 취약
            if (elite && Relics.Rank("eliteBell") > 0)
            {
                float r = Relics.V("eliteBell", 0), vuln = Relics.V("eliteBell", 1);
                Echo("eliteBell", m => RelicFx.Circle(pos, r, m, RelicFx.Light, VfxKeys.AssassinLandRing,
                    onHit: c => c.GetComponent<IVulnerable>()?.ApplyVulnerable(vuln)), Relics.V("eliteBell", 2));
            }

            // 등불 — 처치 n번마다 가까운 무리에 빛 파동
            if (Every("lantern"))
            {
                Vector2 at = RelicFx.Densest(PlayerPos, 7f, 1.8f) ?? pos;
                Echo("lantern", m => RelicFx.Circle(at, 2.2f, m, RelicFx.Light, VfxKeys.AssassinBurstSpark), Relics.V("lantern", 1));
            }

            // 월하의 별똥 — 처치 n번마다 가장 밀집한 곳에 운석
            if (Every("moonMeteor"))
            {
                Vector2 at = RelicFx.Densest(PlayerPos, 9f, 1.8f) ?? pos;
                Echo("moonMeteor", m => RelicProjectile.Spawn(at + new Vector2(-1.5f, 7f), new Vector2(0.22f, -1f), 11f, 2.5f, m,
                    RelicProjectile.Mode.Falling, VfxKeys.MageBasicProjectileLarge, new Color(0.85f, 0.95f, 1f), 0.4f, 0, 1.8f, VfxKeys.MageGravityExplosion), // 블랙홀과 헷갈리지 않게 파란 탄 그림
                    Relics.V("moonMeteor", 1));
            }

            // 종이새 떼 — 처치 n번마다 적을 한데 몰고 폭발
            if (Every("paperBirds"))
            {
                Vector2 at = RelicFx.Densest(PlayerPos, 8f, 2f) ?? pos;
                Echo("paperBirds", m => RelicPullZone.Spawn(at, 2.4f, 0.8f, 9f, m, new Color(1f, 1f, 0.9f, 0.9f), VfxKeys.AssassinHeartbeat, VfxKeys.AssassinBurstSpark),
                     Relics.V("paperBirds", 1));
            }

            // 종언의 종(저주) — 처치 n번마다 화면 전체 폭발
            if (Every("doomBell"))
            {
                Vector2 at = PlayerPos + Vector2.up * 0.5f;
                Echo("doomBell", m => RelicFx.Circle(at, 14f, m, new Color(1f, 0.4f, 0.3f, 0.9f), VfxKeys.MageCollapseCritical, 2.5f), Relics.V("doomBell", 1));
                HitFeel.Shake(14f, 0.4f);
            }
        }

        // ───────────────────────── 연쇄 처치 ─────────────────────────

        /// <summary>연쇄 처치(0.8초 안 3마리째부터, 보너스 골드가 나온 처치) — <see cref="EnemyRewards"/>가 부른다.</summary>
        public static void OnChainKill(Vector2 pos)
        {
            if (Relics.InRelicDamage) return;
            if (!Relics.KillCountsForCommon(ProfileService.Current)) return; // 위와 같다 — 시그니처 연쇄만
            chainKills++;

            // 연쇄 세트 2 — 연쇄 처치 두 번에 한 번 작은 충격파(50%). 4 — 연쇄 처치마다 바닥을 가르는 칼날 2줄(30%)
            int chainTags = Relics.TagCount("연쇄");
            if (chainTags >= 2 && chainKills % 2 == 0)
                RelicFx.Circle(pos, 1.6f, 0.5f, RelicFx.Gold, VfxKeys.AssassinLandRing);
            if (chainTags >= 4 && !LineEffectsOff)
            {
                RelicFx.Line(pos + new Vector2(-5f, 0.2f), pos + new Vector2(5f, 0.2f), 0.5f, 0.3f, RelicFx.Gold, 0.25f);
                RelicFx.Line(pos + new Vector2(-5f, 0.8f), pos + new Vector2(5f, 0.6f), 0.5f, 0.3f, RelicFx.Gold, 0.25f);
            }

            // 편지 — 가까운 적 n마리에 편지, 1초 뒤 그 자리 폭발
            if (Relics.Rank("letter") > 0)
            {
                Relics.Proc("letter");
                float m = Relics.V("letter", 1);
                bool gold = Relics.Awakened("letter");
                foreach (var c in RelicFx.Nearest(pos, Relics.VI("letter", 0), 6f))
                {
                    var target = c;
                    VfxPlayer.Spawn(VfxKeys.MechanicHitSpark, RelicFx.Center(target), DotScale.Value, 15f, RelicFx.SortingOrder);
                    Vector2 last = RelicFx.Center(target);
                    RelicFx.After(1f, () =>
                    {
                        Vector2 at = target != null ? RelicFx.Center(target) : last;
                        RelicFx.Circle(at, 1.3f, m, new Color(1f, 0.9f, 0.6f, 0.9f), VfxKeys.MageFireExplosion);
                        if (gold) RelicFx.After(AwakenDelay, () => RelicFx.Circle(at, 1.3f, m * AwakenPower, RelicFx.Gold));
                    });
                }
            }

            // 연쇄 모래시계 — 연쇄 n번마다 바닥 충격파
            if (!LineEffectsOff && Relics.Rank("chainHourglass") > 0 && chainKills % Mathf.Max(1, Relics.VI("chainHourglass", 0)) == 0)
                Echo("chainHourglass", m => GroundSweep(PlayerPos, 7f, m, new Color(1f, 0.85f, 0.5f, 0.9f)), Relics.V("chainHourglass", 1));

            // 일곱째 날의 왕관 — 회귀 때 모은 봉인을 하나 써서 화면 베기
            var p = ProfileService.Current;
            if (!LineEffectsOff && p.relicSeals > 0 && Relics.Rank("seventhCrown") > 0)
            {
                p.relicSeals--;
                Echo("seventhCrown", m => ScreenSlash(m, RelicFx.Gold), Relics.V("seventhCrown", 1));
            }
        }

        /// <summary>플레이어 높이로 화면을 가로지르는 베기(두 줄).</summary>
        static void ScreenSlash(float mult, Color color)
        {
            Vector2 c = PlayerPos;
            RelicFx.Line(c + new Vector2(-14f, 0.3f), c + new Vector2(14f, 0.3f), 0.9f, mult, color, 0.3f);
            RelicFx.Line(c + new Vector2(-14f, 1.2f), c + new Vector2(14f, 0.9f), 0.6f, mult * 0.5f, color, 0.3f);
            VfxPlayer.Spawn(VfxKeys.AssassinBolt2, c + Vector2.up * 0.6f, DotScale.Value, 15f, RelicFx.SortingOrder);
            HitFeel.Shake(8f, 0.25f);
        }

        // ───────────────────────── 매 프레임·피격 ─────────────────────────

        /// <summary>사냥 중 매 프레임 — 콤보 종착역(콤보가 n의 배수를 넘을 때마다 화면 베기).</summary>
        public static void Tick()
        {
            if (LineEffectsOff || Relics.Rank("comboTerminal") <= 0) { lastComboStep = 0; return; }
            int every = Mathf.Max(1, Relics.VI("comboTerminal", 0));
            int step = CombatModifiers.Combo / every;
            if (step > lastComboStep)
                Echo("comboTerminal", m => ScreenSlash(m, new Color(0.6f, 0.9f, 1f, 0.95f)), Relics.V("comboTerminal", 1));
            lastComboStep = step;
        }

        /// <summary>적이 직접 공격에 맞았다 — 회귀 엽서(회귀한 적이 있으면, 스테이지 첫 엘리트 피격에 메아리).</summary>
        public static void OnEnemyHit(GameObject enemy)
        {
            if (enemy == null || postcardUsedThisStage || Relics.InRelicDamage) return;
            if (Relics.Rank("regressionPostcard") <= 0 || ProfileService.Current.regressions <= 0) return;
            if (enemy.GetComponent<EnemyElite>() == null) return;
            postcardUsedThisStage = true;
            var col = enemy.GetComponent<Collider2D>();
            Echo("regressionPostcard", m => RelicFx.After(0.2f, () =>
            {
                if (col == null) return;
                RelicFx.Hit(col, m, 1f, 0.5f);
                VfxPlayer.Spawn(VfxKeys.AssassinRipHit, RelicFx.Center(col), DotScale.Value, 15f, RelicFx.SortingOrder);
            }), Relics.V("regressionPostcard", 0));
        }
    }
}
