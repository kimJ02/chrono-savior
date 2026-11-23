using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 전투 화면의 보유 유물 칸(2026-11-19 사용자 "어떤 유물을 가지고 있는지 플레이 화면에서 보여줘" — GPT와 정함).
    /// Figma HUD는 그대로 두고 빈자리(오른쪽, 콤보 아래 ~ 미니맵 위)에만 더한다.
    ///
    /// - 지금 캐릭터·갈래에서 켜진 유물만, 정의 순서로 2열 × 11줄(22칸). 넘치면 마지막 칸에 "+N".
    /// - 칸 = 등급색 보석 · 이름 앞 두 글자 · 랭크 점(1~3). 각성(랭크 상한)이면 금테.
    /// - 유물이 발동하면 그 칸 테두리가 잠깐 하얗게(<see cref="Relics.Proc"/>).
    /// - 마우스를 올리면 왼쪽에 이름·랭크·효과 설명 상자.
    /// 유물 그림이 아직 없어 글자·색으로 구분한다(그림이 생기면 보석 자리에 넣으면 된다).
    /// </summary>
    public class RelicHudStrip
    {
        const float X = 504f, Y = 112f, CellW = 64f, CellH = 12f;
        const int Cols = 2, Rows = 11, Max = Cols * Rows;
        const float FlashTime = 0.15f;
        static readonly Color Bg = new Color(0.06f, 0.06f, 0.09f, 1f);
        static readonly Color Edge = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color GoldEdge = new Color(1f, 0.8f, 0.25f, 1f);
        static readonly Color PipOn = Color.white, PipOff = new Color(1f, 1f, 1f, 0.25f);

        class Cell
        {
            public RectTransform root;
            public Image edge, fill, gem;
            public Text label;
            public Image[] pips;
            public ItemDef def;
            public bool awakened;
        }

        readonly Cell[] cells = new Cell[Max];
        readonly PxTooltip tip;
        readonly List<ItemDef> active = new List<ItemDef>();
        int hover = -1, shownHover = -2;
        // 2026-11-20 최적화 — 목록·글자·랭크 점은 유물 상태가 바뀔 때만 다시 그린다(매 프레임은 발동 테두리만)
        int builtStamp = -1;
        bool builtVisible;
        int shownCount;

        public RelicHudStrip(Transform parent) : this(Px.Node(parent, "RelicStrip", X, Y, CellW * Cols, CellH * Rows), parent) { }

        /// <summary>
        /// 이미 있는 자리(프리팹의 `RelicStrip`, 2026-11-23 프리팹 UI 14호) 안에 칸을 짓는다 — 자리·크기는 프리팹이 정하고 칸 모양·개수는 여기서.
        /// 설명 상자는 자리의 부모(판) 위에 띄운다.
        /// </summary>
        public static RelicHudStrip Attach(RectTransform root) => new RelicHudStrip(root, root.parent);

        RelicHudStrip(RectTransform root, Transform parent)
        {
            for (int i = 0; i < Max; i++)
            {
                float cx = (i % Cols) * CellW, cy = (i / Cols) * CellH;
                var c = new Cell();
                c.root = Px.Node(root, "Relic" + i, cx, cy, CellW - 1f, CellH - 1f);
                c.edge = Px.Fill(c.root, "Edge", Edge, 0f, 0f, CellW - 1f, CellH - 1f);
                c.edge.raycastTarget = true; // 마우스 올림
                c.fill = Px.Fill(c.root, "Fill", Bg, 1f, 1f, CellW - 3f, CellH - 3f);
                c.gem = Px.Fill(c.root, "Gem", Color.white, 2f, 2f, 7f, 7f);
                c.label = Px.Text(c.root, "Label", "", 11f, 0f, 36f, CellH - 1f, 8, Color.white, TextAnchor.MiddleLeft).WithOutline(Px.Ink);
                c.pips = new Image[3];
                for (int k = 0; k < 3; k++) c.pips[k] = Px.Fill(c.root, "Pip" + k, PipOff, 49f + k * 4f, 4f, 3f, 3f);
                int idx = i;
                var hov = c.edge.gameObject.AddComponent<RelicCellHover>();
                hov.Enter = () => hover = idx;
                hov.Exit = () => { if (hover == idx) hover = -1; };
                c.root.gameObject.SetActive(false);
                cells[i] = c;
            }
            tip = new PxTooltip(parent);
        }

        /// <summary>매 프레임 — 켜진 유물 목록·랭크·발동 반짝임·설명 상자.</summary>
        public void Refresh(bool visible)
        {
            var p = ProfileService.Current;
            int st = Relics.StateVersion;
            if (st != builtStamp || visible != builtVisible) Rebuild(p, visible, st);

            // 매 프레임 — 보이는 칸의 발동 테두리만(값이 같으면 Unity가 다시 그리지 않는다)
            for (int i = 0; i < shownCount; i++)
            {
                var c = cells[i];
                if (c.def == null) continue;
                bool flash = Relics.SinceProc(c.def.id) < FlashTime;
                var want = flash ? Color.white : c.awakened ? GoldEdge : Edge;
                if (c.edge.color != want) c.edge.color = want;
            }

            if (hover != shownHover)
            {
                shownHover = hover;
                var def = hover >= 0 && hover < Max ? cells[hover].def : null;
                if (def == null) tip.Hide();
                else
                {
                    int rank = Relics.Rank(p, def.id), cap = ItemDatabase.Cap(def.grade);
                    string grade = $"<color=#{ColorUtility.ToHtmlStringRGB(ItemDatabase.GradeColor(def.grade))}>{ItemDatabase.GradeName(def.grade)}</color>";
                    string body = $"{def.name}  {grade}\n랭크 {rank}/{cap}{(rank >= cap ? "  <color=#fac70e>각성</color>" : "")}\n{def.description}";
                    var r = new Rect(X + (hover % Cols) * CellW, Y + (hover / Cols) * CellH, CellW, CellH);
                    tip.Show(body, r);
                }
            }
            if (!visible) { tip.Hide(); shownHover = -2; }
        }

        void Rebuild(PlayerProfile p, bool visible, int st)
        {
            builtStamp = st;
            builtVisible = visible;
            active.Clear();
            if (visible)
                foreach (var d in ItemDatabase.All)
                    if (Relics.Rank(p, d.id) > 0) active.Add(d);

            int overflow = active.Count > Max ? active.Count - (Max - 1) : 0;
            shownCount = Mathf.Min(active.Count, Max);
            for (int i = 0; i < Max; i++)
            {
                var c = cells[i];
                bool more = overflow > 0 && i == Max - 1;
                var def = !more && i < active.Count ? active[i] : null;
                c.def = def;
                c.awakened = false;
                c.root.gameObject.SetActive(def != null || more);
                if (more)
                {
                    c.gem.enabled = false;
                    c.label.text = $"+{overflow}";
                    foreach (var pip in c.pips) pip.enabled = false;
                    c.edge.color = Edge;
                    continue;
                }
                if (def == null) continue;
                int rank = Relics.Rank(p, def.id), cap = ItemDatabase.Cap(def.grade);
                c.awakened = rank >= cap;
                c.gem.enabled = true;
                c.gem.color = ItemDatabase.GradeColor(def.grade);
                c.label.text = Short(def.name);
                for (int k = 0; k < 3; k++)
                {
                    c.pips[k].enabled = k < cap;
                    c.pips[k].color = k < rank ? PipOn : PipOff;
                }
                c.edge.color = c.awakened ? GoldEdge : Edge;
            }
            shownHover = -2; // 설명 상자도 새 내용으로
        }

        /// <summary>이름 앞 두 글자(띄어쓰기 빼고).</summary>
        static string Short(string name)
        {
            var s = name.Replace(" ", "");
            return s.Length <= 2 ? s : s.Substring(0, 2);
        }

        /// <summary>테스트용 — 지금 칸에 보이는 유물 수.</summary>
        public int ShownCount => active.Count;
    }

    /// <summary>유물 칸 마우스 올림 알림.</summary>
    public class RelicCellHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action Enter, Exit;
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
    }
}
