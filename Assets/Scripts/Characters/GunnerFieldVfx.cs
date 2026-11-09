using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 설치기 빌드의 **링크·내부 장판 그림** — 그림만이다(피해는 <see cref="GunnerField"/>·<see cref="GunnerAttack"/>).
    /// 2026-11-08 VFX 스프린트, 팀원 문서 '이펙트와 연출':
    /// - 중계선: **전기 아크**(2026-11-10 사용자 I-B '전기 아크형' — 다른 게임 사례(Factorio 테슬라: 곧은 기준 + 둘레 아크, 랜덤 오프셋으로 반복감 제거)·Codex xhigh·
    ///   예시 그림에서 고름). 링크마다 선(LineRenderer) 셋: 판정 경로 그대로인 **희미한 1px 기준선**(실제 맞는 자리를 흐리지 않게 — Codex) + 흔들리는 주 아크(2px 흰청) +
    ///   보조 아크(1px 파랑 반투명). 아크는 그림 <see cref="ArcStepPx"/>px 간격 점을 ±<see cref="ArcJitterPx"/>px 흔들어 <see cref="ArcFps"/>fps로 다시 뽑고, 양 끝은 중계기에 고정.
    ///   예전의 굵은 레이저 몸통 타일·판정 폭 옅은 띠는 뺐다(화면이 막혔다). 맞는 순간은 <see cref="GunnerZap"/>이 적 자리에 전기를 띄운다.
    /// - 내부공간: "푸른 반투명 + 하얀 점 파티클, 에셋 없이" — 설치기 3개 이상이 닫힌 도형을 만들면 그 안을 푸른 반투명으로 칠하고
    ///   작은 흰 점들이 안에서 깜빡이며 떠오른다. 칠하기는 다각형 메시라 머티리얼이 필요해 씬 연결 칸(<see cref="fillMaterial"/>)으로 받는다.
    /// 씬의 Player에 놓인다(메카닉 키트 <see cref="GunnerAttack"/>가 같은 오브젝트에서 찾아 부른다). 그림 오브젝트는 런 오브젝트라 런이 끝나면 치워진다.
    /// </summary>
    public class GunnerFieldVfx : MonoBehaviour
    {
        /// <summary>정렬: 연결선은 레이저와 같은 3(투사체), 내부 칠은 필드 효과 -3, 흰 점은 그 위 -2(CLAUDE.md 예약표).</summary>
        public const int LinkSortingOrder = 3, FillSortingOrder = -3, DotSortingOrder = -2;
        public static readonly Color FillColor = new Color(0x8f / 255f, 0xd8 / 255f, 1f, 0.11f); // I-B: 아크가 주인공이라 칠은 옅게
        public const int DotCount = 6;
        /// <summary>흰 점 하나가 나타났다 사라지는 시간(초)과 떠오르는 속도(유닛/초).</summary>
        public const float DotLifeMin = 0.6f, DotLifeMax = 1.3f, DotRise = 0.25f;
        /// <summary>아크 점 간격·흔들림(그림 px)·다시 뽑는 속도.</summary>
        public const int ArcStepPx = 6, ArcJitterPx = 3;
        public const float ArcFps = 15f;
        static readonly Color BaseLineColor = new Color(0.45f, 0.75f, 1f, 0.45f), MainArcColor = new Color(0.85f, 0.97f, 1f, 1f), SubArcColor = new Color(0.35f, 0.65f, 1f, 0.6f);

        [Tooltip("내부 장판 칠하기 머티리얼(Assets/Materials/VfxUnlit — 꼭짓점 색을 그대로 칠하는 스프라이트 셰이더). 외주 효과로 바꿀 땐 이 칸만.")]
        [SerializeField] Material fillMaterial;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public Material FillMaterial { get => fillMaterial; set { fillMaterial = value; GunnerZap.LineMaterial = value; } }

        // 적중 분기 아크(GunnerZap)도 같은 선 머티리얼을 쓴다 — 씬의 이 칸에서 받는다
        void Awake() => GunnerZap.LineMaterial = fillMaterial;

        GameObject root;
        /// <summary>링크마다 [기준선, 주 아크, 보조 아크].</summary>
        readonly List<LineRenderer[]> links = new List<LineRenderer[]>();
        readonly List<Vector3> arcPts = new List<Vector3>();
        /// <summary>마지막으로 아크를 뽑은 프레임 번호·그때의 링크 끝점 — 같으면 다시 안 뽑는다(아크는 <see cref="ArcFps"/>fps로만 바뀐다, Codex 리뷰).</summary>
        int arcFrame = -1;
        readonly List<Vector4> arcEnds = new List<Vector4>();
        MeshRenderer fill;
        Mesh fillMesh;
        readonly List<Dot> dots = new List<Dot>();
        List<Vector2> polygon;

        struct Dot { public SpriteRenderer sr; public float t, life; }

        /// <summary>지금 보이는 연결선 수·내부 칠이 보이는지(테스트용).</summary>
        public int VisibleLinks { get { int n = 0; foreach (var l in links) if (l[0].enabled) n++; return n; } }
        /// <summary>i번 링크의 [기준선, 주 아크, 보조 아크](테스트용).</summary>
        public LineRenderer[] LinkLines(int i) => links[i];
        public bool FillVisible => fill != null && fill.enabled;


        /// <summary>
        /// 장판을 그린다 — 꼭짓점(볼록 껍질 순서)과 연결선·판정 띠. 안쪽 칠은 **판정과 같은 조건**(꼭짓점 3개 이상이고 모든 변이 이어진 닫힌 도형,
        /// <see cref="GunnerField.DamageTick"/>)일 때만 — 예전엔 꼭짓점 3개면 늘 칠해서, 한 변이 링크 거리를 넘으면 칠은 보이는데 안쪽은 안 맞았다.
        /// </summary>
        public void Show(List<Vector2> points, List<GunnerField.Segment> segs, int tier, Sprite fallbackSprite)
        {
            EnsureRoot();
            root.SetActive(true);
            DrawLinks(segs);
            polygon = points;
            bool closed = points.Count >= 3 && segs.Count >= points.Count && fillMaterial != null;
            if (closed) DrawFill(points);
            if (fill != null) fill.enabled = closed;
            foreach (var d in dots) d.sr.enabled = closed;
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        void EnsureRoot()
        {
            if (root != null) return;
            root = new GameObject("GunnerFieldLinks");
            RunTransient.Mark(root); // 설치기와 같이 런이 끝나면 치워진다
            links.Clear();
            dots.Clear();
            fill = null;
        }

        void DrawLinks(List<GunnerField.Segment> segs)
        {
            if (fillMaterial == null) return; // 선도 머티리얼이 있어야 그린다(없으면 분홍 선)
            while (links.Count < segs.Count)
            {
                var set = new LineRenderer[3];
                set[0] = NewLine("LinkBase", DotScale.Units(1f), BaseLineColor, LinkSortingOrder);
                set[1] = NewLine("LinkArc", DotScale.Units(2f), MainArcColor, LinkSortingOrder + 1);
                set[2] = NewLine("LinkArcSub", DotScale.Units(1f), SubArcColor, LinkSortingOrder);
                links.Add(set);
            }
            int frame = Mathf.FloorToInt(Time.time * ArcFps); // 이 프레임 번호로 아크 모양이 정해진다(같은 프레임엔 같은 모양)
            bool same = frame == arcFrame && arcEnds.Count == segs.Count;
            for (int i = 0; same && i < segs.Count; i++)
                same = arcEnds[i] == new Vector4(segs[i].a.x, segs[i].a.y, segs[i].b.x, segs[i].b.y);
            for (int i = 0; i < links.Count; i++)
            {
                bool on = i < segs.Count;
                foreach (var l in links[i]) l.enabled = on;
            }
            if (same) return;
            arcFrame = frame;
            arcEnds.Clear();
            for (int i = 0; i < segs.Count; i++)
            {
                arcEnds.Add(new Vector4(segs[i].a.x, segs[i].a.y, segs[i].b.x, segs[i].b.y));
                Vector2 a = Snap(segs[i].a), b = Snap(segs[i].b);
                links[i][0].positionCount = 2;
                links[i][0].SetPosition(0, a);
                links[i][0].SetPosition(1, b);
                SetArc(links[i][1], a, b, frame * 7919 + i * 104729);
                SetArc(links[i][2], a, b, frame * 6151 + i * 15485863 + 17);
            }
        }

        LineRenderer NewLine(string name, float width, Color color, int order)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(root.transform, false);
            var l = go.GetComponent<LineRenderer>();
            l.sharedMaterial = fillMaterial;
            l.useWorldSpace = true;
            l.widthMultiplier = width;
            l.startColor = l.endColor = color;
            l.numCapVertices = 0;
            l.numCornerVertices = 0;
            l.alignment = LineAlignment.TransformZ;
            l.sortingOrder = order;
            return l;
        }

        /// <summary>a→b 전기 아크 — 그림 <see cref="ArcStepPx"/>px마다 점, 가운데 점만 ±<see cref="ArcJitterPx"/>px 흔들고 양 끝은 고정. 점은 그림 px 격자에 맞춘다.</summary>
        void SetArc(LineRenderer l, Vector2 a, Vector2 b, int seed)
        {
            Vector2 v = b - a;
            float len = v.magnitude;
            Vector2 n = len > 1e-5f ? new Vector2(-v.y, v.x) / len : Vector2.up;
            float step = DotScale.Units(ArcStepPx);
            int count = Mathf.Max(2, Mathf.CeilToInt(len / step) + 1);
            arcPts.Clear();
            for (int k = 0; k < count; k++)
            {
                float t = k / (float)(count - 1);
                Vector2 q = a + v * t;
                if (k > 0 && k < count - 1) q += n * DotScale.Units(Jitter(seed, k));
                arcPts.Add(Snap(q));
            }
            l.positionCount = arcPts.Count;
            for (int k = 0; k < arcPts.Count; k++) l.SetPosition(k, arcPts[k]);
        }

        /// <summary>시드·점 번호로 정해지는 −<see cref="ArcJitterPx"/>~+<see cref="ArcJitterPx"/> 정수(할당 없는 해시).</summary>
        public static int Jitter(int seed, int k)
        {
            uint h = (uint)seed * 2654435761u ^ (uint)k * 2246822519u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (int)(h % (uint)(ArcJitterPx * 2 + 1)) - ArcJitterPx;
        }

        /// <summary>그림 px 격자(도트 배율)에 맞춘다 — 선이 반 픽셀에 걸려 흔들리지 않게(Codex).</summary>
        public static Vector2 Snap(Vector2 p)
        {
            float u = DotScale.Units(1f);
            return new Vector2(Mathf.Round(p.x / u) * u, Mathf.Round(p.y / u) * u);
        }

        void DrawFill(List<Vector2> points)
        {
            if (fill == null)
            {
                var go = new GameObject("Fill", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                fillMesh = new Mesh { name = "GunnerFieldFill" };
                go.GetComponent<MeshFilter>().sharedMesh = fillMesh;
                fill = go.GetComponent<MeshRenderer>();
                fill.sharedMaterial = fillMaterial;
                fill.sortingOrder = FillSortingOrder;
                for (int i = 0; i < DotCount; i++)
                {
                    var dgo = new GameObject("Dot", typeof(SpriteRenderer));
                    dgo.transform.SetParent(root.transform, false);
                    var sr = dgo.GetComponent<SpriteRenderer>();
                    sr.sprite = VfxDots.DotSprite();
                    sr.sortingOrder = DotSortingOrder;
                    float life = Random.Range(DotLifeMin, DotLifeMax);
                    sr.transform.position = RandomPointInside(points); // 처음부터 안에 흩어져 있게(제각각 다른 때에 깜빡이도록 시작 시각도 흩는다)
                    dots.Add(new Dot { sr = sr, t = Random.value * life, life = life });
                }
            }
            // 볼록 다각형이라 첫 점에서 부채꼴로 나눠 칠한다
            var verts = new Vector3[points.Count];
            var colors = new Color[points.Count];
            for (int i = 0; i < points.Count; i++) { verts[i] = points[i]; colors[i] = FillColor; }
            var tris = new int[(points.Count - 2) * 3];
            for (int i = 0; i < points.Count - 2; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = i + 2; }
            fillMesh.Clear();
            fillMesh.vertices = verts;
            fillMesh.colors = colors;
            fillMesh.triangles = tris;
            fillMesh.RecalculateBounds();
        }


        void Update()
        {
            if (root == null || !root.activeSelf || fill == null || !fill.enabled || polygon == null) return;
            float dt = Time.deltaTime; // 일시정지·히트스톱 땐 같이 멈춘다
            for (int i = 0; i < dots.Count; i++)
            {
                var d = dots[i];
                d.t += dt;
                if (d.t >= d.life)
                {
                    d.t = 0f;
                    d.life = Random.Range(DotLifeMin, DotLifeMax);
                    d.sr.transform.position = RandomPointInside(polygon);
                }
                d.sr.transform.position += new Vector3(0f, DotRise * dt, 0f);
                float a = Mathf.Sin(Mathf.PI * Mathf.Clamp01(d.t / d.life)); // 나타났다 사라진다
                d.sr.color = new Color(1f, 1f, 1f, a * 0.9f);
                dots[i] = d;
            }
        }

        /// <summary>다각형 안의 아무 점 — 감싸는 사각형에서 뽑아 안에 들 때까지(볼록이라 금방 든다).</summary>
        static Vector2 RandomPointInside(List<Vector2> poly)
        {
            Vector2 min = poly[0], max = poly[0];
            foreach (var p in poly) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            for (int k = 0; k < 20; k++)
            {
                var q = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
                if (GunnerField.PointInPolygon(q, poly)) return q;
            }
            Vector2 c = Vector2.zero;
            foreach (var p in poly) c += p;
            return c / poly.Count;
        }

        void OnDestroy()
        {
            if (fillMesh != null) Destroy(fillMesh);
        }
    }
}
