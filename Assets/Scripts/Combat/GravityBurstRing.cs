using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 원본 `gravityBurst` 그리기(project_test.html:5239~:5255) — 대붕괴가 터진 자리에 잠깐 보이는 보라색 원판과 고리.
    /// **그림만이다** — 피해는 <see cref="MageSkillEffects.GravityCollapse"/>가 그 자리에서 이미 줬다.
    /// `k = 1 - t/수명`이 1 → 0으로 줄면서 고리는 반지름의 0.90배 → 1.08배로 퍼지고, 원판(0.84배 → 1배)과 함께 흐려진다.
    /// 임계 폭발은 고리가 굵고 바퀴살 6개가 돈다. 원본은 가산 혼합(`lighter`)인데 여기선 보통 반투명이다.
    /// </summary>
    public class GravityBurstRing : MonoBehaviour
    {
        const int Segments = 40;
        const int SortingOrder = 4; // 중력점 고리(3) 위
        static readonly Color FillColor = new Color(160f / 255f, 90f / 255f, 1f);  // rgba(160,90,255, 0.20k)
        static readonly Color RingColor = new Color(224f / 255f, 184f / 255f, 1f); // rgba(224,184,255, 0.88k)
        static readonly Color SpokeColor = new Color(1f, 235f / 255f, 1f);         // rgba(255,235,255, 0.58k)
        static Material material;

        float radius, life, age;
        bool critical, ringOnly;
        LineRenderer ring;
        Mesh discMesh;
        MeshRenderer disc;
        LineRenderer[] spokes;

        public float Radius => radius;
        public bool Critical => critical;
        public bool RingOnly => ringOnly;

        public static GravityBurstRing Spawn(Vector2 pos, float radius, float life, bool critical, bool ringOnly)
        {
            var go = new GameObject("GravityBurst");
            go.transform.position = pos;
            RunTransient.Mark(go);
            var b = go.AddComponent<GravityBurstRing>();
            b.radius = radius;
            b.life = life;
            b.critical = critical;
            b.ringOnly = ringOnly;
            b.Build();
            b.Draw(1f);
            return b;
        }

        void Build()
        {
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            ring = NewLine("Ring", Segments, loop: true);

            if (!ringOnly) // 원본 `if (!z.ringOnly) ctx.fill()` — 바깥 고리엔 원판이 없다
            {
                var child = new GameObject("Disc");
                child.transform.SetParent(transform, false);
                discMesh = new Mesh();
                var verts = new Vector3[Segments + 1];
                var tris = new int[Segments * 3];
                for (int i = 0; i < Segments; i++)
                {
                    float a = i / (float)Segments * Mathf.PI * 2f;
                    verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    tris[i * 3] = 0;
                    tris[i * 3 + 1] = (i + 1) % Segments + 1;
                    tris[i * 3 + 2] = i + 1;
                }
                discMesh.vertices = verts;
                discMesh.triangles = tris;
                child.AddComponent<MeshFilter>().sharedMesh = discMesh;
                disc = child.AddComponent<MeshRenderer>();
                disc.sharedMaterial = material;
                disc.sortingOrder = SortingOrder;
            }

            if (critical)
            {
                spokes = new LineRenderer[6];
                for (int i = 0; i < spokes.Length; i++)
                {
                    spokes[i] = NewLine("Spoke", 2, loop: false);
                    spokes[i].widthMultiplier = 0.02f; // 원본 lineWidth 2px
                }
            }
        }

        LineRenderer NewLine(string name, int points, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var lr = child.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.positionCount = points;
            lr.sharedMaterial = material;
            lr.sortingOrder = SortingOrder + 1;
            return lr;
        }

        void Update()
        {
            age += Time.deltaTime; // 히트스톱(timeScale 0) 동안엔 원본처럼 멈춘다
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            Draw(Mathf.Clamp01(1f - age / life));
        }

        void Draw(float k)
        {
            float ringR = radius * (1.08f - k * 0.18f);
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * ringR, Mathf.Sin(a) * ringR, 0f));
            }
            ring.widthMultiplier = ((critical ? 7f : 4f) * k + 1f) / 100f; // 원본 px ÷ 100
            ring.startColor = ring.endColor = WithAlpha(RingColor, 0.88f * k);

            if (disc != null)
            {
                disc.transform.localScale = Vector3.one * (radius * (1f - k * 0.16f));
                var colors = new Color[Segments + 1];
                var c = WithAlpha(FillColor, 0.20f * k);
                for (int i = 0; i < colors.Length; i++) colors[i] = c;
                discMesh.colors = colors;
            }

            if (spokes != null)
            {
                for (int i = 0; i < spokes.Length; i++)
                {
                    float a = Time.time * 3f + i * Mathf.PI / 3f; // 원본 `state.time * 3 + i * PI / 3`
                    var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    spokes[i].SetPosition(0, d * (radius * 0.25f));
                    spokes[i].SetPosition(1, d * (radius * 0.9f));
                    spokes[i].startColor = spokes[i].endColor = WithAlpha(SpokeColor, 0.58f * k);
                }
            }
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        void OnDestroy()
        {
            if (discMesh != null) Destroy(discMesh);
        }
    }
}
