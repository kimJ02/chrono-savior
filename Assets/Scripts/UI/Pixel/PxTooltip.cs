using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 칸 위에 마우스를 올리거나 키보드로 고르면 뜨는 설명 상자(2026-10-24). Figma엔 없다 — 유물·스킬 칸이 그림만 있어서
    /// 이름·효과를 보여줄 자리가 필요했다. 어두운 판 + 1도트 테두리 + 글자. 칸 오른쪽에, 판 밖으로 나가면 왼쪽에 띄운다.
    /// </summary>
    public class PxTooltip
    {
        const float MaxWidth = 200f, PadX = 5f, PadY = 4f;
        static readonly Color Fill = new Color(0.08f, 0.06f, 0.12f, 0.94f);

        readonly RectTransform root;
        readonly Text text;

        public PxTooltip(Transform parent)
        {
            root = Px.Node(parent, "Tooltip", 0f, 0f, 10f, 10f);
            var border = Px.Fill(root, "Border", Px.Ink, 0f, 0f, 10f, 10f);
            Stretch(border.rectTransform, 0f);
            var fill = Px.Fill(root, "Fill", Fill, 0f, 0f, 10f, 10f);
            Stretch(fill.rectTransform, 1f);
            text = Px.Text(root, "Text", "", PadX, PadY, MaxWidth, 10f, 8, Color.white, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = 1.2f;
            root.gameObject.SetActive(false);
        }

        public bool Visible => root.gameObject.activeSelf;
        public string Shown => text.text;

        /// <summary><paramref name="anchor"/>(판 좌표, 왼쪽 위 기준)의 옆에 띄운다.</summary>
        public void Show(string s, Rect anchor)
        {
            text.text = s;
            text.rectTransform.sizeDelta = new Vector2(MaxWidth, 10f);
            float w = Mathf.Min(MaxWidth, text.preferredWidth);
            text.rectTransform.sizeDelta = new Vector2(w, 10f);
            float h = text.preferredHeight;
            float boxW = Mathf.Ceil(w + PadX * 2f), boxH = Mathf.Ceil(h + PadY * 2f);

            float x = anchor.xMax + 4f;
            if (x + boxW > PixelCanvas.Width - 2f) x = anchor.x - boxW - 4f;
            float y = Mathf.Clamp(anchor.y, 2f, PixelCanvas.Height - boxH - 2f);
            root.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
            root.sizeDelta = new Vector2(boxW, boxH);
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        /// <summary>정해 둔 자리(판 좌표, 왼쪽 위)에 띄운다 — 칸 옆에 띄우면 이웃 칸을 가리는 화면용.</summary>
        public void ShowAt(string s, float x, float y)
        {
            Show(s, new Rect(-1000f, y, 0f, 0f));
            root.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        }

        public void Hide() => root.gameObject.SetActive(false);

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
