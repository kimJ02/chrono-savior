using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 엘리트 표식 — 원본 `spawnEnemyAt()`의 `elite` 플래그(project_test.html:3948)에 대응한다.
    ///
    /// 원본 엘리트는 **별도의 몹 종류가 아니라 아무 몹에게나 붙는 승격 상태**다(`CONFIG.elite` :696).
    /// 체력 ×4, 피해 ×1.5, 몸집 ×1.35, 보상 ×5이고 **골드는 확률을 무시하고 항상 떨군다**
    /// (`killEnemy`의 `if (Math.random() < goldDropChance || e.boss || e.elite)`, `:1818`).
    /// 행동(AI)은 일반 몹과 완전히 같다 — 스포너가 수치를 곱하고, 처치 보상 계산이 "엘리트였다"를 읽어 간다.
    ///
    /// **그림**(2026-11-09 — 도트 배율 통일로 엘리트 그림이 일반 몹과 같은 도트 크기가 되면서, 원본의 엘리트 표시를 옮겼다. 예전 Unity엔 없었다):
    /// 원본 `drawEnemy`(:4780) 그대로 몸을 둘러싼 **보라 오라**(깜빡이며 밝아졌다 어두워짐, 몸보다 0.12씩 크게)와 머리 위 **금색 왕관**,
    /// 가끔 떠오르는 보라 불씨. 오라·왕관은 실행 중에 그린 도트 그림(그림 1px = 격자 <see cref="DotScale.Value"/>칸). 판정(×1.35)은 그대로.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyElite : MonoBehaviour
    {
        static readonly Color AuraColor = new Color(180 / 255f, 90 / 255f, 1f), CrownColor = new Color(1f, 0xd7 / 255f, 0x6e / 255f);
        /// <summary>원본 왕관 모양(아래가 0줄) — 11×6 도트.</summary>
        static readonly string[] CrownRows = { "XXXXXXXXXXX", "XXXXXXXXXXX", "XXXXXXXXXXX", "XXX.XXX.XXX", "XX..XXX..XX", "X....X....X" };
        static Sprite crownSprite;

        SpriteRenderer body, aura, crown;
        float seed;

        void Start()
        {
            body = GetComponent<SpriteRenderer>();
            seed = Random.value * 10f;
            if (body == null || body.sprite == null) return;
            var b = body.bounds;
            // 오라 — 원본 `ellipse(0, -h/2, w/2 + 12, h/2 + 12)`
            aura = NewChild("EliteAura", DotShapes.Ellipse(b.extents.x + 0.12f, b.extents.y + 0.12f), body.sortingOrder - 1);
            aura.transform.position = b.center;
            // 왕관 — 원본 `cy = -e.h - 8`(머리 위 0.08)
            crown = NewChild("EliteCrown", CrownSprite(), body.sortingOrder + 1);
            crown.color = CrownColor;
            crown.transform.position = new Vector3(b.center.x, b.max.y + 0.08f + crown.sprite.bounds.extents.y, 0f);
            VfxDots.Attach(transform, VfxDots.Motion.Rise, 3, AuraColor, b.extents.x, 0.5f, 0.3f, body.sortingOrder + 1); // 원본 가끔 오르는 보라 불씨
        }

        void Update()
        {
            if (aura == null) return;
            float pulse = 0.5f + 0.3f * Mathf.Sin(Time.time * 6f + seed); // 원본 `0.5 + 0.3 * sin(t * 6 + seed)`
            aura.color = new Color(AuraColor.r, AuraColor.g, AuraColor.b, 0.22f * pulse);
        }

        /// <summary>몹 배율과 무관하게 월드 기준 크기로 붙는 자식(그림 PPU가 도트 배율에 맞춰져 있다).</summary>
        SpriteRenderer NewChild(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var s = transform.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.01f, Mathf.Abs(s.y)), 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = order;
            return sr;
        }

        static float Ppu => DotShapes.Ppu;

        static Sprite CrownSprite()
        {
            if (crownSprite != null) return crownSprite;
            int w = CrownRows[0].Length, h = CrownRows.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = CrownRows[y][x] == 'X' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply();
            crownSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            return crownSprite;
        }

    }
}
