using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 화염돌진(폭발 마법사 X) 그림 — **그림만이다**(돌진 이동·무적·장판은 `Characters.MageAttack`).
    /// 2026-11-09 사용자 "순간이동 느낌이라 별로, 화염돌진 같은 느낌으로, 효과 알아서 추가" → Codex 합의:
    /// - 출발 자리에 작은 폭발 퍼프(기가팩 `symmetrical_explosion_002` small orange).
    /// - 돌진하는 동안 몸 **뒤로 화염 분사**(화염탄 혜성 그림, 진행 방향) + 불씨 점.
    /// - 지나가는 순서대로 바닥 불꽃 점화(<see cref="PathFlame"/> — 바닥에 닿은 지점만, 부르는 쪽이 골라 넘긴다)와 작은 연기.
    /// - 도착하면 작은 불꽃 터짐(기가팩 `symmetrical_explosion_001` small orange), 분사는 짧게 남았다 사라진다.
    /// </summary>
    public class FireDashVfx : MonoBehaviour
    {
        public const int JetScale = 2;
        public const float JetFps = 20f, JetLinger = 0.12f, PuffFps = 20f;
        /// <summary>폭발·분사 정렬(캐릭터 앞 — 폭발 줄 4).</summary>
        public const int SortingOrder = MageSkillEffects.ExplosionSortingOrder;
        static readonly Color EmberColor = new Color(1f, 0.6f, 0.2f);

        Vector2 from, to;
        float duration, t, flameLife;
        List<Vector2> flames;
        int nextFlame;
        VfxPlayer jet;
        VfxDots embers;
        bool landed;

        /// <summary>
        /// 돌진 그림을 시작한다 — 몸 중심이 <paramref name="duration"/>초에 걸쳐 <paramref name="from"/>→<paramref name="to"/>로 가는 것에 맞춘다(같은 감속 곡선 <see cref="Ease"/>).
        /// <paramref name="groundFlames"/>는 지나간 바닥의 불꽃 밑동 자리(돌진 경로를 따라 순서대로), <paramref name="flameLife"/>는 불꽃이 타는 시간.
        /// </summary>
        public static FireDashVfx Play(Vector2 from, Vector2 to, float duration, List<Vector2> groundFlames, float flameLife)
        {
            var go = new GameObject("FireDashVfx");
            RunTransient.Mark(go);
            var v = go.AddComponent<FireDashVfx>();
            v.from = from; v.to = to; v.duration = Mathf.Max(0.01f, duration);
            v.flames = groundFlames ?? new List<Vector2>();
            v.flameLife = flameLife;
            VfxPlayer.Spawn(VfxKeys.MageDashPuff, from, 2, PuffFps, SortingOrder); // 출발 퍼프
            v.jet = VfxPlayer.Spawn(VfxKeys.MageFireProjectile, from, JetScale, JetFps, SortingOrder, true);
            if (v.jet != null)
            {
                Vector2 d = to - from;
                v.jet.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                v.jet.transform.SetParent(go.transform, true);
            }
            v.embers = VfxDots.Attach(go.transform, VfxDots.Motion.Rise, 10, EmberColor, 0.15f, 0.35f, 0.6f, SortingOrder);
            v.Apply();
            return v;
        }

        /// <summary>돌진 위치 곡선 — 빠르게 튀어 나가 끝에서 감속(ease-out).</summary>
        public static float Ease(float k) => 1f - (1f - k) * (1f - k);

        void Update()
        {
            t += Time.deltaTime; // 일시정지·히트스톱 땐 같이 멈춘다(돌진 이동도 같은 시간)
            Apply();
        }

        void Apply()
        {
            float k = Mathf.Clamp01(t / duration);
            Vector2 d = to - from;
            Vector2 body = from + d * Ease(k);
            Vector2 back = d.sqrMagnitude > 1e-6f ? -d.normalized : Vector2.left;
            if (embers != null) embers.transform.position = body + back * 0.15f;

            // 지나간 만큼 바닥 불꽃 점화(경로를 따라 순서대로) + 가끔 연기
            float passedX = Ease(k);
            while (nextFlame < flames.Count && ProgressOf(flames[nextFlame]) <= passedX + 1e-4f)
            {
                PathFlame.Ignite(flames[nextFlame], 0f, flameLife * Random.Range(0.85f, 1.1f));
                if (Random.value < 0.35f) VfxPlayer.Spawn(VfxKeys.MageFireSmoke, flames[nextFlame] + new Vector2(0f, 0.2f), 1, 15f, PathFlame.SortingOrder + 1, false, 0.4f, new Color(1f, 1f, 1f, 0.6f));
                nextFlame++;
            }

            if (k < 1f)
            {
                if (jet != null) jet.transform.position = body + back * 0.3f; // 몸 뒤로 분사
                return;
            }
            if (!landed)
            {
                landed = true;
                VfxPlayer.Spawn(VfxKeys.MageDashLand, to, 2, PuffFps, SortingOrder); // 도착 불꽃
                if (embers != null) embers.Emitting = false;
            }
            float linger = t - duration;
            if (jet != null) jet.Alpha = 1f - Mathf.Clamp01(linger / JetLinger);
            if (linger >= Mathf.Max(JetLinger, 0.4f) && nextFlame >= flames.Count) Destroy(gameObject);
        }

        /// <summary>바닥 불꽃 자리가 돌진 경로의 어디쯤(0~1)인지 — 가로로 잰다(세로 돌진이면 세로로).</summary>
        float ProgressOf(Vector2 p)
        {
            Vector2 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return 0f;
            return Mathf.Clamp01(Vector2.Dot(p - from, d) / d.sqrMagnitude);
        }
    }
}
