using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// Figma 좌표로 uGUI 요소를 만드는 도구(2026-10-24). Figma 레이어의 x·y·너비·높이를 **그대로** 넣는다 —
    /// 640×360 판의 왼쪽 위가 (0, 0)이고 y는 아래로 커진다(uGUI는 위로 커지므로 여기서 뒤집는다).
    /// 화면 전체를 덮는 것(어두운 막 등)은 <see cref="Stretch"/>로 판 밖까지 깐다.
    /// </summary>
    public static class Px
    {
        /// <summary>Figma 글꼴 "DNF Bit Bit v2"(네오플 무료 서체, `Resources/Fonts/DNFBitBitv2_LICENSE.txt`).</summary>
        public static Font Font => font != null ? font : (font = Resources.Load<Font>("Fonts/DNFBitBitv2"));
        static Font font;

        // ── Figma에 반복해서 나오는 색 ──
        /// <summary>테두리·글자 외곽선의 거의 검은 남색 #0e071b.</summary>
        public static readonly Color Ink = Hex(0x0e071b);
        /// <summary>판 배경 회색 #858585(일시정지 띠·창 틀 안쪽).</summary>
        public static readonly Color Panel = Hex(0x858585);
        /// <summary>
        /// 화면 뒤를 덮는 어두운 막 #1e1e1e. **Figma에선 불투명**이지만 그건 뒤에 게임이 없어서다 — 게임 위에선 반투명으로 덮는다
        /// (원본 `#result` 막도 반투명). 불투명도는 <see cref="BackdropAlpha"/>.
        /// </summary>
        public static readonly Color Backdrop = Hex(0x1e1e1e);
        public const float BackdropAlpha = 0.72f;

        public static Color Hex(uint rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, a);

        /// <summary>Figma 좌표(왼쪽 위 기준)의 사각형 자리.</summary>
        public static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = NewRect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>부모를 꽉 채우는 자리(화면 전체 막, 층).</summary>
        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = NewRect(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>화면 가운데의 640×360 설계 판 — Figma 프레임 하나.</summary>
        public static RectTransform Board(Transform parent)
        {
            var rt = NewRect(parent, "Board");
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PixelCanvas.Width, PixelCanvas.Height);
            return rt;
        }

        /// <summary>Figma 그림 한 장을 그 자리·크기 그대로.</summary>
        public static Image Image(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
        {
            var img = Node(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            if (sprite == null) img.color = new Color(1f, 0f, 1f, 0.6f); // 그림이 안 들어왔으면 눈에 띄게(분홍)
            return img;
        }

        /// <summary>한 색으로 채운 사각형.</summary>
        public static Image Fill(Transform parent, string name, Color color, float x, float y, float w, float h)
        {
            var img = Node(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>화면 전체를 한 색으로(판 밖 여백까지).</summary>
        public static Image FullFill(Transform parent, string name, Color color, bool blocksClicks = true)
        {
            var img = Stretch(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = blocksClicks; // 막 뒤의 버튼이 눌리지 않게
            return img;
        }

        /// <summary>
        /// Figma 글자 상자 — 상자(x·y·너비·높이) 안에 정렬한다. 칸 밖으로 넘쳐도 자르지 않는다(받침이 잘려 "깨져 보이던"
        /// IMGUI 문제를 다시 만들지 않게). 크기는 Figma px 그대로 — 캔버스 배율만큼 실제 화면 크기로 그려져 선명하다.
        /// </summary>
        public static Text Text(Transform parent, string name, string text, float x, float y, float w, float h,
                                int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var t = Node(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>글자에 테두리(Figma 글자 외곽선) — 두께는 설계 1도트.</summary>
        /// <summary>
        /// 글씨를 월드 좌표 위에 놓는다(가운데 맞춤) — 카메라는 가운데에 서 있고 화면에 보이는 월드 가로 7.5 × 세로 4.22유닛(`StageArtConfig`)이
        /// 판 640×360에 딱 맞는다. 로비 건물 이름·전투 팝업 글씨가 쓴다.
        /// </summary>
        public static void PlaceAtWorld(Text t, Vector2 world, Vector2 cam)
        {
            var rt = (RectTransform)t.transform;
            float bx = 320f + (world.x - cam.x) * (320f / ChronoSavior.World.StageArtConfig.ViewHalfWidth);
            float by = 180f - (world.y - cam.y) * (180f / ChronoSavior.World.StageArtConfig.ViewHalfHeight);
            rt.anchoredPosition = new Vector2(Mathf.Round(bx - rt.sizeDelta.x / 2f), -Mathf.Round(by - rt.sizeDelta.y / 2f));
        }

        public static Text WithOutline(this Text t, Color color, float dots = 1f)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(dots, -dots);
            return t;
        }

        /// <summary>글자 아래 그림자(Figma drop shadow).</summary>
        public static Text WithShadow(this Text t, Color color, float dx = 0f, float dy = 1f)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = color;
            s.effectDistance = new Vector2(dx, -dy);
            return t;
        }

        /// <summary>그림 버튼 — 바탕 그림 + 눌릴 때 1도트 내려가는 안쪽(<paramref name="inner"/>, 버튼과 같은 크기).</summary>
        public static PxButton Button(Transform parent, string name, Sprite art, float x, float y, float w, float h,
                                      System.Action onClick, out RectTransform inner)
        {
            var img = Image(parent, name, art, x, y, w, h);
            inner = Stretch(img.transform, "Inner");
            var b = img.gameObject.AddComponent<PxButton>();
            b.Init(img, inner);
            b.OnClick = onClick;
            return b;
        }

        /// <summary>글자 버튼 — 글자는 버튼 가운데.</summary>
        public static PxButton TextButton(Transform parent, string name, Sprite art, float x, float y, float w, float h,
                                          string label, int size, Color color, System.Action onClick)
        {
            var b = Button(parent, name, art, x, y, w, h, onClick, out var inner);
            Text(inner, "Label", label, 0f, 0f, w, h, size, color);
            return b;
        }

        /// <summary>아이콘 버튼 — 아이콘 자리는 버튼 왼쪽 위 기준.</summary>
        public static PxButton IconButton(Transform parent, string name, Sprite frame, Sprite icon, float x, float y,
                                          float w, float h, float iconX, float iconY, float iconW, float iconH,
                                          System.Action onClick)
        {
            var b = Button(parent, name, frame, x, y, w, h, onClick, out var inner);
            Image(inner, "Icon", icon, iconX, iconY, iconW, iconH);
            return b;
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }
    }
}
