using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 설치기가 적을 지질 때 그 적 자리에 뜨는 작은 파란 전기 — 그림만이다(피해는 <see cref="GunnerTurret"/>·<see cref="GunnerField"/>).
    /// 2026-11-10 사용자 I-B '전기 아크형'(다른 게임 사례 조사·Codex xhigh·예시 그림에서 고름): 늘 깔린 판정 띠·범위 원 대신 **맞는 순간**에 알린다.
    /// 중계기 원형 틱·링크·내부 장판 틱이 같은 적을 같은 때 때려도 그림은 **적마다 <see cref="Interval"/>초에 한 번만**(피해와 무관하게 그림만 제한 — Codex).
    /// **적중 분기 아크**(2026-11-10 사용자 'A'): 전기가 뜰 때 그 적을 때린 곳(가장 가까운 연결선 위 점·닫힌 도형 변·중계기 머리)에서 적 몸 가장자리까지
    /// 짧은 전기(<see cref="GunnerZapArc"/>)도 같이 — 선에서 떨어진 적이 왜 맞는지 보인다. 한 프레임 <see cref="MaxArcsPerFrame"/>개·동시에 <see cref="MaxArcsAlive"/>개까지
    /// (넘으면 아크만 생략, 피해·적 자리 전기는 그대로 — Codex).
    /// </summary>
    public static class GunnerZap
    {
        public const float Interval = 0.25f, Fps = 24f;
        public const int SortingOrder = 4;
        static readonly Dictionary<Collider2D, float> last = new Dictionary<Collider2D, float>();
        static readonly List<Collider2D> stale = new List<Collider2D>();
        static float lastPrune;
        public const int MaxArcsPerFrame = 4, MaxArcsAlive = 8;
        static readonly List<GunnerZapArc> arcs = new List<GunnerZapArc>();
        static int arcFrame = -1, arcsThisFrame, seedCounter;
        /// <summary>아크 선 머티리얼 — 씬 Player의 <see cref="GunnerFieldVfx"/> 칸(VfxUnlit)에서 받는다. 없으면 아크 없이.</summary>
        public static Material LineMaterial { get; set; }

        // 도메인 리로드 없이 플레이를 다시 시작해도 옛 기록이 남지 않게(Codex 리뷰)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { last.Clear(); lastPrune = 0f; arcs.Clear(); arcFrame = -1; LineMaterial = null; }

        /// <summary>
        /// 이 적 자리에 전기를 띄운다(이 적에게 방금 띄웠으면 넘어간다). <paramref name="from"/>이 있으면 거기서 적 몸 가장자리(반지름 <paramref name="enemyRadius"/>)까지
        /// 아크도. 띄웠으면 true.
        /// </summary>
        public static bool At(Collider2D enemy, Vector2 at, Vector2? from = null, float enemyRadius = 0f)
        {
            if (enemy != null)
            {
                if (last.TryGetValue(enemy, out float t) && Time.time - t < Interval) return false;
                if (last.Count > 64 || Time.time - lastPrune > 2f) Prune();
                last[enemy] = Time.time;
            }
            VfxPlayer.Spawn(VfxKeys.MechanicZap, at, DotScale.Value, Fps, SortingOrder);
            if (from.HasValue) Arc(from.Value, at, enemyRadius);
            return true;
        }

        /// <summary>지금 보이는 아크 수(테스트용).</summary>
        public static int ArcsAlive { get { int n = 0; foreach (var a in arcs) if (a != null && a.Busy) n++; return n; } }

        static void Arc(Vector2 from, Vector2 enemyCenter, float enemyRadius)
        {
            if (LineMaterial == null) return;
            if (arcFrame != Time.frameCount) { arcFrame = Time.frameCount; arcsThisFrame = 0; }
            if (arcsThisFrame >= MaxArcsPerFrame) return;
            arcs.RemoveAll(a => a == null); // 런이 끝나 치워진 것
            GunnerZapArc free = null;
            int alive = 0;
            foreach (var a in arcs) { if (a.Busy) alive++; else if (free == null) free = a; }
            if (alive >= MaxArcsAlive) return;
            if (free == null) { free = GunnerZapArc.Create(LineMaterial); arcs.Add(free); }
            Vector2 v = enemyCenter - from;
            float len = v.magnitude;
            Vector2 to = len > enemyRadius * 0.9f + 1e-4f ? enemyCenter - v / len * enemyRadius * 0.9f : enemyCenter; // 적 몸 가장자리
            free.Play(from, to, unchecked(++seedCounter * 7919 + Time.frameCount));
            arcsThisFrame++;
        }

        /// <summary>죽은 적·오래된 기록을 지운다.</summary>
        static void Prune()
        {
            lastPrune = Time.time;
            stale.Clear();
            foreach (var kv in last)
                if (kv.Key == null || Time.time - kv.Value > 1f) stale.Add(kv.Key);
            foreach (var k in stale) last.Remove(k);
        }

        /// <summary>테스트용 — 기록을 비운다.</summary>
        public static void ResetForTests()
        {
            last.Clear();
            arcFrame = -1;
            foreach (var a in arcs) if (a != null) a.gameObject.SetActive(false);
        }

        /// <summary>이 적에게 지금 전기를 띄울 수 있나(0.25초 제한) — 출발점 계산을 아끼려고 먼저 묻는다.</summary>
        public static bool Ready(Collider2D enemy) =>
            enemy == null || !last.TryGetValue(enemy, out float t) || Time.time - t >= Interval;
    }
}
