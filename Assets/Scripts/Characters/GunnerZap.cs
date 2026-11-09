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
    /// </summary>
    public static class GunnerZap
    {
        public const float Interval = 0.25f, Fps = 24f;
        public const int SortingOrder = 4;
        static readonly Dictionary<Collider2D, float> last = new Dictionary<Collider2D, float>();
        static readonly List<Collider2D> stale = new List<Collider2D>();
        static float lastPrune;

        // 도메인 리로드 없이 플레이를 다시 시작해도 옛 기록이 남지 않게(Codex 리뷰)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { last.Clear(); lastPrune = 0f; }

        /// <summary>이 적 자리에 전기를 띄운다(이 적에게 방금 띄웠으면 넘어간다). 띄웠으면 true.</summary>
        public static bool At(Collider2D enemy, Vector2 at)
        {
            if (enemy != null)
            {
                if (last.TryGetValue(enemy, out float t) && Time.time - t < Interval) return false;
                if (last.Count > 64 || Time.time - lastPrune > 2f) Prune();
                last[enemy] = Time.time;
            }
            VfxPlayer.Spawn(VfxKeys.MechanicZap, at, DotScale.Value, Fps, SortingOrder);
            return true;
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
        public static void ResetForTests() => last.Clear();
    }
}
