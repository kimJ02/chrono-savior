using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 타격감 — 원본 기준 **히트스톱 길이 · 화면 흔들림 · 플레이어 피격 시 화면 붉어짐**(2026-10-24 사용자 지시).
    ///
    /// - **히트스톱**: 실제로 멈추는 건 `Systems/GameFlow.Hitstop`(`timeScale`을 만지는 유일한 곳)이고, 여기는 길이 값과
    ///   요청 통로(<see cref="CombatEvents.RequestHitstop"/>)만 둔다. 원본은 공격마다 길이가 다르다 — `dealDamage`의
    ///   `hitstop`/`noHitstop`(project_test.html:1683). 예전엔 직접 타격이 전부 0.045, 처치가 보스까지 전부 0.085였다.
    /// - **화면 흔들림**: 원본 `shake(mag, dur)`(:1572) — 세기·시간은 큰 쪽으로 덮어쓰고, 매 프레임(히트스톱 중에도 — 원본
    ///   `rawDt`) `남은 시간 / 0.18` 비율로 줄어든다(:1583). 세기는 원본 px(화면 높이 720 기준)라 `World/CameraFollow2D`가
    ///   우리 화면 높이 대비 같은 비율로 옮긴다.
    /// - **화면 붉어짐**: 원본 `state.redFlash`(:1912 맞으면 0.35, :4426 초당 -2.2, :6204 `rgba(255,30,10, r×0.35)`).
    ///   그리는 건 `UI/Screens/CombatHud`, 줄이는 건 `Systems/CombatDirector`.
    /// </summary>
    public static class HitFeel
    {
        // ---- 히트스톱 길이(초) ----

        /// <summary>기본 직접 타격 — 원본 `CONFIG.hitstop.hit`(:743).</summary>
        public const float HitstopHit = 0.045f;
        /// <summary>스킬 타격 — 원본 `CONFIG.hitstop.skill`(:743). 화염 폭발(:1987)·임계 대붕괴(:2112).</summary>
        public const float HitstopSkill = 0.07f;
        /// <summary>약한 스킬 타격 — 원본 `hitstop: 0.03`. 일반 대붕괴(:2112)·임계 바깥 고리(:2120).</summary>
        public const float HitstopLight = 0.03f;
        /// <summary>멈춤 없음 — 원본 `noHitstop`. 메카닉 총알·드론 탄(:3721), 중력탄 충돌(:2036).</summary>
        public const float HitstopNone = 0f;
        /// <summary>처치 — 원본 `CONFIG.hitstop.kill`(:1847).</summary>
        public const float HitstopKill = 0.085f;
        /// <summary>보스 처치 — 원본 `e.boss ? 0.25`(:1847).</summary>
        public const float HitstopBossKill = 0.25f;
        /// <summary>성소 파괴 — 원본 :1804.</summary>
        public const float HitstopShrine = 0.12f;
        /// <summary>연쇄 처치 보너스 — 원본 :1835.</summary>
        public const float HitstopChain = 0.10f;

        // ---- 화면 흔들림 ----

        /// <summary>원본 흔들림이 줄어드는 기준 시간 — `m = mag × max(0, t / 0.18)`(:1583). 기본 지속 시간이기도 하다.</summary>
        public const float ShakeDecayRef = 0.18f;
        /// <summary>원본 화면 높이(px). 흔들림 px를 우리 화면에 같은 비율로 옮길 때 쓴다.</summary>
        public const float OriginalViewHeightPx = 720f;

        /// <summary>
        /// 흔들림 세기 배율 — 2026-10-28 사용자 "적 타격할 때 화면 흔들림 수치 올려줘". 원본 px 값(스킬·처치·피격)은 그대로 기록하고
        /// 화면에 옮길 때만 곱한다(`CameraFollow2D`) — 의도적 편차, docs/original-parity.md.
        /// </summary>
        public const float ShakeStrength = 1.5f;

        /// <summary>
        /// 평타로 몹을 맞힐 때의 약한 흔들림(원본 px) — **원본엔 없다**(원본은 일반 타격에 흔들림이 없고 스킬·처치·피격에만 있다).
        /// 2026-10-28 사용자 결정. 히트스톱처럼 <see cref="HitShakeGap"/>초 간격을 둬서 섬영·레이저처럼 자주 맞혀도 계속 떨리지 않는다.
        /// 거는 곳은 `Systems/CombatDirector`(직접 타격·무기 틱 알림을 받는다). 모든 캐릭터 평타가 같은 값이다 — 처음 2px·0.08초는
        /// 안 보여서 5px·0.14초로 올렸다(사용자 2026-10-28 "모든 캐릭터 평타에 같은 값으로").
        /// </summary>
        public const float HitShakePx = 10f;
        public const float HitShakeTime = 0.14f;
        public const float HitShakeGap = 0.25f;

        /// <summary>
        /// 지속 피해 틱(레이저·포탑·연결장·섬영 궤적·불길 장판·깔린 블랙홀)의 흔들림 — 평타보다 약하게(사용자 2026-10-28
        /// "도트뎀처럼 들어가는 건 평타보다 낮게"). 간격은 평타와 따로 센다 — 틱이 평타 흔들림을 막지 않게.
        /// </summary>
        public const float TickShakePx = 2f;
        public const float TickShakeTime = 0.08f;

        public static float ShakeMag { get; private set; }
        public static float ShakeTime { get; private set; }

        /// <summary>원본 `shake(mag, dur = 0.18)` — 세기·시간 모두 큰 쪽으로 덮어쓴다.</summary>
        public static void Shake(float magPx, float duration = ShakeDecayRef)
        {
            ShakeMag = Mathf.Max(ShakeMag, magPx);
            ShakeTime = Mathf.Max(ShakeTime, duration);
        }

        /// <summary>원본 `updateShake(rawDt)` — 남은 시간을 줄이고 이번 프레임 흔들림 크기(원본 px)를 돌려준다.</summary>
        public static float TickShake(float rawDt)
        {
            if (ShakeTime <= 0f) return 0f;
            ShakeTime -= rawDt;
            float m = ShakeMag * Mathf.Max(0f, ShakeTime / ShakeDecayRef);
            if (ShakeTime <= 0f)
            {
                ShakeTime = 0f;
                ShakeMag = 0f;
            }
            return m;
        }

        // ---- 플레이어가 맞을 때 ----

        public const float RedFlashOnHurt = 0.35f;
        public const float RedFlashDecayPerSecond = 2.2f;
        public const float RedFlashAlphaScale = 0.35f;

        /// <summary>화면 붉어짐 세기(0~0.35). 실제 불투명도는 × <see cref="RedFlashAlphaScale"/>.</summary>
        public static float RedFlash { get; private set; }

        /// <summary>원본 `damagePlayer`의 연출(:1912~:1913) — 화면 붉어짐 0.35 + 흔들림(7, 0.22).</summary>
        public static void PlayerHurt()
        {
            RedFlash = RedFlashOnHurt;
            Shake(7f, 0.22f);
        }

        public static void TickRedFlash(float dt) => RedFlash = Mathf.Max(0f, RedFlash - dt * RedFlashDecayPerSecond);

        /// <summary>런 시작·테스트 격리.</summary>
        public static void Reset()
        {
            ShakeMag = 0f;
            ShakeTime = 0f;
            RedFlash = 0f;
        }
    }
}
