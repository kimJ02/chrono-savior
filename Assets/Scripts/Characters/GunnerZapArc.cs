using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 설치기가 적을 지질 때 **연결선(또는 중계기 머리)에서 그 적 몸까지 뻗는 짧은 지그재그 전기** 하나 — 그림만(<see cref="GunnerZap"/>이 풀로 돌려 쓴다).
    /// 2026-11-10 사용자: "전기가 있는 부분만 맞는 게 아니라 사이사이도 맞는데 그게 안 보인다" → 판정 범위를 칠하는 안은 싫다 → 다른 게임 사례(Factorio 테슬라 연쇄 아크)·
    /// Codex xhigh·예시 그림에서 'A 적중 분기 아크'를 골랐다. 맞는 순간 <see cref="Life"/>초만 번쩍 — 왜 맞았는지 전기가 직접 잇는다.
    /// 선 둘(바깥 파랑 2px + 안 흰청 1px), 그림 <see cref="StepPx"/>px마다 꺾고 ±<see cref="JitterPx"/>px, 양 끝 고정, 그림 px 격자에 맞춘다.
    /// </summary>
    public class GunnerZapArc : MonoBehaviour
    {
        public const float Life = 0.13f;
        public const int StepPx = 6, JitterPx = 3, SortingOrder = 4;
        static readonly Color Outer = new Color(0.35f, 0.67f, 1f, 1f), Inner = new Color(0.88f, 0.96f, 1f, 1f);

        LineRenderer outer, inner;
        float t;
        readonly Vector3[] pts = new Vector3[64];

        /// <summary>지금 보이나(풀이 빈 것을 찾을 때·테스트용).</summary>
        public bool Busy => gameObject.activeSelf;
        public LineRenderer Line => inner;

        public static GunnerZapArc Create(Material mat)
        {
            var go = new GameObject("GunnerZapArc");
            RunTransient.Mark(go);
            var a = go.AddComponent<GunnerZapArc>();
            a.outer = NewLine(go.transform, "Outer", mat, DotScale.Units(2f), Outer, SortingOrder);
            a.inner = NewLine(go.transform, "Inner", mat, DotScale.Units(1f), Inner, SortingOrder + 1);
            go.SetActive(false);
            return a;
        }

        static LineRenderer NewLine(Transform parent, string name, Material mat, float width, Color col, int order)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var l = go.GetComponent<LineRenderer>();
            l.sharedMaterial = mat;
            l.useWorldSpace = true;
            l.widthMultiplier = width;
            l.startColor = l.endColor = col;
            l.numCapVertices = l.numCornerVertices = 0;
            l.alignment = LineAlignment.TransformZ;
            l.sortingOrder = order;
            return l;
        }

        /// <summary>from → to 전기를 띄운다(seed로 모양이 정해진다).</summary>
        public void Play(Vector2 from, Vector2 to, int seed)
        {
            gameObject.SetActive(true);
            t = 0f;
            Vector2 v = to - from;
            float len = v.magnitude;
            Vector2 n = len > 1e-5f ? new Vector2(-v.y, v.x) / len : Vector2.up;
            int count = Mathf.Clamp(Mathf.CeilToInt(len / DotScale.Units(StepPx)) + 1, 2, pts.Length);
            for (int k = 0; k < count; k++)
            {
                Vector2 q = from + v * (k / (float)(count - 1));
                if (k > 0 && k < count - 1) q += n * DotScale.Units(GunnerFieldVfx.Jitter(seed, k));
                pts[k] = GunnerFieldVfx.Snap(q);
            }
            outer.positionCount = inner.positionCount = count;
            outer.SetPositions(pts);
            inner.SetPositions(pts);
        }

        void Update()
        {
            t += Time.deltaTime; // 일시정지·히트스톱 땐 같이 멈춘다
            if (t >= Life) gameObject.SetActive(false);
        }
    }
}
