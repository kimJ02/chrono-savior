using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 설치기 빌드의 **링크·내부 장판 그림** — 그림만이다(피해는 <see cref="GunnerField"/>·<see cref="GunnerAttack"/>).
    /// 2026-11-08 VFX 스프린트, 팀원 문서 '이펙트와 연출':
    /// - 중계선: "레이저빔 에셋 재활용" — 레이저 몸통 그림(`mechanic/laser_center_loop_small`)을 연결선 길이만큼 **이어 붙인다**(타일).
    ///   그림이 없으면(팩을 안 가져온 클론) 예전처럼 하늘색 얇은 막대.
    /// - 내부공간: "푸른 반투명 + 하얀 점 파티클, 에셋 없이" — 설치기 3개 이상이 닫힌 도형을 만들면 그 안을 푸른 반투명으로 칠하고
    ///   작은 흰 점들이 안에서 깜빡이며 떠오른다. 칠하기는 다각형 메시라 머티리얼이 필요해 씬 연결 칸(<see cref="fillMaterial"/>)으로 받는다.
    /// 씬의 Player에 놓인다(메카닉 키트 <see cref="GunnerAttack"/>가 같은 오브젝트에서 찾아 부른다). 그림 오브젝트는 런 오브젝트라 런이 끝나면 치워진다.
    /// </summary>
    public class GunnerFieldVfx : MonoBehaviour
    {
        /// <summary>정렬: 연결선은 레이저와 같은 3(투사체), 내부 칠은 필드 효과 -3, 흰 점은 그 위 -2(CLAUDE.md 예약표).</summary>
        public const int LinkSortingOrder = 3, FillSortingOrder = -3, DotSortingOrder = -2;
        public static readonly Color FillColor = new Color(0x8f / 255f, 0xd8 / 255f, 1f, 0.2f);
        public const int DotCount = 14;
        /// <summary>흰 점 하나가 나타났다 사라지는 시간(초)과 떠오르는 속도(유닛/초).</summary>
        public const float DotLifeMin = 0.6f, DotLifeMax = 1.3f, DotRise = 0.25f;
        const float LinkFps = 15f;

        [Tooltip("내부 장판 칠하기 머티리얼(Assets/Materials/VfxUnlit — 꼭짓점 색을 그대로 칠하는 스프라이트 셰이더). 외주 효과로 바꿀 땐 이 칸만.")]
        [SerializeField] Material fillMaterial;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public Material FillMaterial { get => fillMaterial; set => fillMaterial = value; }

        GameObject root;
        readonly List<SpriteRenderer> links = new List<SpriteRenderer>();
        MeshRenderer fill;
        Mesh fillMesh;
        readonly List<Dot> dots = new List<Dot>();
        List<Vector2> polygon;

        struct Dot { public SpriteRenderer sr; public float t, life; }

        /// <summary>지금 보이는 연결선 수·내부 칠이 보이는지(테스트용).</summary>
        public int VisibleLinks { get { int n = 0; foreach (var l in links) if (l.enabled) n++; return n; } }
        public bool FillVisible => fill != null && fill.enabled;

        /// <summary>장판을 그린다 — 꼭짓점(볼록 껍질 순서)과 연결선. 꼭짓점이 3개 이상이면 안을 칠한다(<see cref="GunnerField.DamageTick"/>의 내부 판정과 같은 조건).</summary>
        public void Show(List<Vector2> points, List<GunnerField.Segment> segs, Sprite fallbackSprite)
        {
            EnsureRoot();
            root.SetActive(true);
            DrawLinks(segs, fallbackSprite);
            polygon = points;
            bool closed = points.Count >= 3 && fillMaterial != null;
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

        void DrawLinks(List<GunnerField.Segment> segs, Sprite fallbackSprite)
        {
            var frames = VfxFrames.Get(VfxKeys.MechanicLaser("center", "loop", false));
            while (links.Count < segs.Count)
            {
                var go = new GameObject("Link", typeof(SpriteRenderer));
                go.transform.SetParent(root.transform, false);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sortingOrder = LinkSortingOrder;
                if (frames.Length > 0)
                {
                    sr.drawMode = SpriteDrawMode.Tiled; // 길이만큼 이어 붙인다(늘리지 않는다 — 도트가 안 뭉개지게)
                    go.AddComponent<VfxPlayer>().Play(frames, LinkFps, true, 0f, Color.white);
                }
                else
                {
                    sr.sprite = fallbackSprite;
                    sr.color = new Color(GunnerAttack.LaserColor.r, GunnerAttack.LaserColor.g, GunnerAttack.LaserColor.b, 0.6f);
                }
                links.Add(sr);
            }
            for (int i = 0; i < links.Count; i++)
            {
                bool on = i < segs.Count;
                links[i].enabled = on;
                if (!on) continue;
                Vector2 a = segs[i].a, b = segs[i].b, v = b - a;
                var tf = links[i].transform;
                tf.position = (a + b) * 0.5f;
                tf.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                if (links[i].drawMode == SpriteDrawMode.Tiled && links[i].sprite != null)
                {
                    tf.localScale = new Vector3(DotScale.Value, DotScale.Value, 1f); // 도트 크기는 같게, 길이는 이어 붙여서
                    links[i].size = new Vector2(Mathf.Max(0.01f, v.magnitude) / DotScale.Value, links[i].sprite.bounds.size.y);
                }
                else
                    tf.localScale = new Vector3(Mathf.Max(0.01f, v.magnitude), 0.06f, 1f);
            }
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
