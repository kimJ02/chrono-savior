using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.World
{
    /// <summary>
    /// 1스테이지 그림 — Figma "스테이지 HUD"(124:3)의 배경·땅·발판을 **원본 파일 그대로**(2026-10-24 C안).
    ///
    /// 이 컴포넌트는 `20_Combat` 씬에 놓여 있다(2026-11-04 씬 분할 — 예전엔 씬을 열 때 스스로 생겼다). 시작할 때 씬에 이미 있는
    /// 판정 상자 위에 그림만 깐다. **기록된 런타임 생성 예외**: 타일은 지역 그림(`StageArtConfig`)과 판정 상자 크기로 계산해 깔기 때문에
    /// 씬에 구워 넣지 않는다(지역마다 씬 그림이 중복되고, 판정 상자를 바꿀 때마다 다시 구워야 한다). 생성물은 전부 이 오브젝트나
    /// 발판의 자식이라 `20_Combat`에 남는다:
    /// - **땅**(`Ground`): 선(LineRenderer)을 끄고 64px 타일(원본 32px을 Figma처럼 2배) 세 줄 — 맨 위가 풀, 아래는 흙.
    ///   타일은 한 장씩 놓는다(<see cref="TileStrip"/>).
    /// - **발판**(`PlatformSet` 아래 `Platform_*`): 선을 끄고 16px 풀 타일(Figma가 32px을 반으로 줄인 그림).
    ///   발판 오브젝트의 자식이라 보스 무대로 바뀔 때(전투 담당 `CombatDirector`가 무대에 맞는 `PlatformSet`을 켜고 끈다) 같이 켜지고 꺼진다.
    /// - **배경**: 원본 640×360을 정수 3배로 깔고 카메라를 따라가되 천천히 움직인다(시차). 카메라가 필드 끝에서 끝까지
    ///   가도 그림 가장자리가 안 보이게, 카메라 위치를 그림 안쪽 범위로 옮겨 계산한다(<see cref="BackgroundRawCenter"/>).
    /// - **보스 포탈**: 그림 묶음의 포탈 그림으로 바꾼다 — <see cref="BossPortal.ApplyArt"/>.
    /// 그림은 **스테이지 그림 묶음(<see cref="StageArtSet"/>)에서 직렬화로** 받는다(2026-11-06 에셋 교체 경계 — 예전엔 `Resources/Art/Figma/Stage1/`을 경로 문자열로 읽었다).
    /// 그림을 바꿀 땐 그 에셋만 바꾼다. 그림 파일 가져오기 설정은 `Editor/FigmaArtImporter`.
    /// </summary>
    [DefaultExecutionOrder(100)] // 카메라(`CameraFollow2D.LateUpdate`)가 움직인 뒤에 배경을 맞춘다
    public class StageVisuals : MonoBehaviour
    {
        public const int BackgroundOrder = -100;
        public const int GroundOrder = -5;
        public const int GroundRows = 3;

        [Tooltip("이 스테이지의 그림 묶음(Assets/Data/Stages/) — 땅·발판·배경·포탈 그림과 그 그림에 딸린 배율·범위.")]
        [SerializeField] StageArtSet art;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public StageArtSet Art { get => art; set => art = value; }

        [Header("같은 씬의 판정 상자 — 그 위에 그림을 깐다")]
        [Tooltip("땅(LineRenderer가 그려진 판정 상자).")]
        [SerializeField] Transform ground;
        [Tooltip("발판 세트(일반·보스) — 그 아래 Platform_* 마다 타일을 깐다.")]
        [SerializeField] PlatformSet[] platformSets;
        [Tooltip("보스 포탈 — Figma 포탈 그림으로 바꾼다.")]
        [SerializeField] BossPortal portal;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다(2026-11-05 2b — 예전엔 이름·타입으로 씬을 뒤졌다).</summary>
        public Transform Ground { get => ground; set => ground = value; }
        public PlatformSet[] PlatformSets { get => platformSets; set => platformSets = value; }
        public BossPortal Portal { get => portal; set => portal = value; }

        Transform background;

        void Start() => Build();

        /// <summary>씬에 있는 땅·발판·카메라에 그림을 붙인다. 두 번 불러도 한 번만 깐다.</summary>
        public void Build()
        {
            // 포탈 그림은 포탈이 있는 씬(전투)에서만 필요하다 — 로비엔 포탈이 없다.
            if (art == null || art.groundTop == null || art.groundDirt == null || art.platformTile == null || art.background == null
                || (portal != null && art.portal == null))
            {
                Debug.LogError("[StageVisuals] 스테이지 그림 묶음(StageArtSet)이 비었거나 칸이 빠졌다 — 메뉴 ChronoSavior/씬 구성 검증", this);
                return;
            }
            Sprite top = art.groundTop, dirt = art.groundDirt, plat = art.platformTile, bg = art.background;

            if (ground != null) BuildGround(ground.gameObject, top, dirt);

            if (platformSets != null)
                foreach (var set in platformSets)
                    if (set != null)
                        foreach (Transform child in set.transform)
                            BuildPlatform(child, plat);

            if (background == null) BuildBackground(bg);

            if (portal != null) portal.ApplyArt(art.portal, art.portalScale);
        }

        void BuildGround(GameObject ground, Sprite top, Sprite dirt)
        {
            if (transform.Find("GroundTiles") != null) return;
            var lr = ground.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = false;

            var col = ground.GetComponent<BoxCollider2D>();
            float width = col.size.x * Mathf.Abs(ground.transform.lossyScale.x);
            float centerX = ground.transform.TransformPoint(col.offset).x;
            float surface = Core.FieldBounds.GroundY;
            float tile = top.bounds.size.y; // 0.5유닛(32px 원본, 픽셀/유닛 64)

            var root = new GameObject("GroundTiles").transform;
            root.SetParent(transform, false);
            for (int row = 0; row < GroundRows; row++)
                TileStrip("Row" + row, root, row == 0 ? top : dirt, centerX - width / 2f, surface - tile * row, width, GroundOrder);
        }

        static void BuildPlatform(Transform platform, Sprite tileSprite)
        {
            var col = platform.GetComponent<BoxCollider2D>();
            if (col == null || platform.Find("Tiles") != null) return;
            var lr = platform.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = false;

            float width = col.size.x * Mathf.Abs(platform.lossyScale.x);
            // 비활성(보스 무대) 오브젝트는 콜라이더 bounds가 비어 있어서 크기·위치를 직접 계산한다.
            Vector3 topCenter = platform.TransformPoint(col.offset + new Vector2(0f, col.size.y / 2f));
            TileStrip("Tiles", platform, tileSprite, topCenter.x - width / 2f, topCenter.y, width, GroundOrder);
        }

        /// <summary>
        /// 타일을 한 장씩 왼쪽부터 늘어놓는다. `SpriteRenderer`의 타일 모드는 쓰지 않는다 — 2026-10-24 캡처에서 타일 모드로
        /// 그린 것만 다른 그림(배경)이 찍혔다. 폭이 타일 배수가 아니면 마지막 장은 그림을 남는 픽셀만큼 잘라 붙여서
        /// 그림 끝이 판정 끝과 맞는다.
        /// </summary>
        static Transform TileStrip(string name, Transform parent, Sprite tile, float left, float top, float width, int order)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            float ppu = tile.pixelsPerUnit;
            int tilePx = Mathf.RoundToInt(tile.rect.width);
            int widthPx = Mathf.RoundToInt(width * ppu);
            float tileH = tile.rect.height / ppu;
            for (int i = 0, x = 0; x < widthPx; i++, x += tilePx)
            {
                int px = Mathf.Min(tilePx, widthPx - x);
                var go = new GameObject(i.ToString());
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = px == tilePx ? tile : Partial(tile, px);
                sr.sortingOrder = order;
                go.transform.position = new Vector3(left + (x + px / 2f) / ppu, top - tileH / 2f, 0f);
            }
            return root;
        }

        static readonly Dictionary<(Sprite, int), Sprite> partials = new Dictionary<(Sprite, int), Sprite>();

        /// <summary>타일 그림의 왼쪽 px칸만 잘라 낸 그림(같은 텍스처, 같은 픽셀/유닛).</summary>
        static Sprite Partial(Sprite tile, int px)
        {
            if (partials.TryGetValue((tile, px), out var s) && s != null) return s;
            var r = tile.rect;
            s = Sprite.Create(tile.texture, new Rect(r.x, r.y, px, r.height), new Vector2(0.5f, 0.5f),
                              tile.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            s.name = tile.name + "_" + px;
            partials[(tile, px)] = s;
            return s;
        }

        void BuildBackground(Sprite bg)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(art.backgroundScale, art.backgroundScale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bg;
            sr.sortingOrder = BackgroundOrder;
            background = go.transform;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (background == null) return;
            var cam = CameraFollow2D.CurrentCamera; // 카메라는 다른 씬(00_Persistent)이라 등록으로 읽는다
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            background.position = BackgroundPosition(art, c.x, c.y);
        }

        /// <summary>카메라 중심이 (camX, camY)일 때 배경 그림의 위치(그림 가운데 기준).</summary>
        public static Vector3 BackgroundPosition(StageArtSet art, float camX, float camY)
        {
            Vector2 raw = BackgroundRawCenter(art, camX, camY);
            var rect = art.background.rect;
            float k = art.backgroundScale / art.background.pixelsPerUnit; // 원본 1px = 유닛
            float dx = (raw.x - rect.width / 2f) * k;
            float dy = -(raw.y - rect.height / 2f) * k; // 그림은 위에서 아래로, 월드는 아래에서 위로
            return new Vector3(camX - dx, camY - dy, 0f);
        }

        /// <summary>
        /// 카메라 중심이 가리키는 배경 원본의 점(px, 왼쪽 위 기준). 카메라가 움직일 수 있는 범위 전체를 그림 안쪽 범위로
        /// 비례해 옮기므로, 어디에 있어도 화면에 그림 가장자리가 안 걸린다.
        /// </summary>
        public static Vector2 BackgroundRawCenter(StageArtSet art, float camX, float camY)
        {
            float halfW = StageArtConfig.ViewHalfWidth, halfH = StageArtConfig.ViewHalfHeight;
            float minX = Core.FieldBounds.MinX + halfW, maxX = Core.FieldBounds.MaxX - halfW;
            float tx = maxX > minX ? Mathf.InverseLerp(minX, maxX, camX) : 0.5f;
            float u = Mathf.Lerp(art.backgroundViewLeftPx, art.backgroundViewRightPx, tx);

            // 땅에 섰을 때(카메라 최저) 그림 속 풀밭이 지면 높이에 오게: 지면은 화면 아래에서 GroundScreenFraction.
            float pxPerUnitRaw = art.background.pixelsPerUnit / art.backgroundScale;
            float halfHRaw = halfH * pxPerUnitRaw;
            float vLow = art.backgroundGrassYPx - (1f - StageArtConfig.GroundScreenFraction) * 2f * halfHRaw + halfHRaw;
            float vHigh = halfHRaw; // 맨 위 발판에 섰을 때 — 그림 윗변이 화면 윗변
            float minY = CameraFollow2D.MinY, maxY = CameraFollow2D.MaxY;
            float ty = maxY > minY ? Mathf.InverseLerp(minY, maxY, camY) : 0f;
            float v = Mathf.Lerp(vLow, vHigh, ty);
            return new Vector2(u, v);
        }
    }
}
