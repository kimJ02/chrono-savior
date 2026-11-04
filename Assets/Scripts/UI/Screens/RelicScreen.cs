using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 시공 유물 소환(유물 개방) — Figma "시공 유물 소환"(32:17)의 640×360판: 왼쪽 5×5 유물 칸, 오른쪽 판(시간의 파편 + 버튼 둘).
    /// 로비의 "가챠" 건물에서 연다. 동작은 원본 가챠(`GachaService`, 예전 로비 "기원" 탭)와 같다.
    ///
    /// Figma 640판은 오른쪽 판에 글자가 없어서 **옛 1920판 내용**(보유 시간의 파편·확률·1회/10회 개방)으로 채웠다.
    /// 등급 이름은 Figma대로 노말·레어·에픽·레전드. 확률은 **원본 값**(55·30·12·3%)이다 — Figma 옛 판의 70·27·2·1%와 다르다(사용자 확인 전).
    /// 유물은 원본대로 35종이라 25칸 두 쪽으로 넘겨 본다. 원본 아이콘은 이모지라 도트 글꼴에 없다 — 칸엔 등급 색 ◆와 개수,
    /// 이름·효과는 칸을 고르면(마우스 올림·방향키) 뜨는 설명 상자에. 아직 없는 유물은 "?"(Figma 옛 판 "미확인 유물").
    /// </summary>
    public class RelicScreen : PxScreen
    {
        static RelicScreen instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;
        public static void Open() { if (instance != null) instance.open = true; }
        public static void Close() { if (instance != null) instance.open = false; }

        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Lobby;
        protected override NavLayout Layout => NavLayout.Grid;
        protected override int NavColumns => Cols;

        // Figma 좌표(640판)
        static readonly Color Bg = Px.Hex(0x414141);                       // 391:4
        static readonly Rect TitleBox = new Rect(30f, 20f, 70f, 15f);      // 391:16 16px
        const float GridX = 16f, GridY = 42f, GridStep = 68f, SlotSize = 32f; // 391:14 … 391:42
        const int Cols = 5, PerPage = 25;
        static readonly Rect Panel = new Rect(336f, 16f, 288f, 328f);      // 391:7
        static readonly Rect ShardIcon = new Rect(352f, 32f, 64f, 64f);    // 88:6 (32px 그림 2배)
        static readonly Rect Pull1Box = new Rect(352f, 296f, 96f, 32f);    // 391:6
        static readonly Rect Pull10Box = new Rect(512f, 296f, 96f, 32f);   // 391:2
        // Figma에 없는 것(옛 1920판 내용을 640판 오른쪽 판 안에)
        static readonly Rect CloseBox = new Rect(588f, 24f, 24f, 24f);
        const float PageY = 15f;

        static readonly string[] GradeNames = { "노말", "레어", "에픽", "레전드" };
        static readonly Color Unknown = Px.Hex(0x2a2a34);

        readonly Image[] gems = new Image[PerPage];
        readonly Image[] gemEdges = new Image[PerPage];
        readonly Text[] marks = new Text[PerPage];
        readonly Text[] counts = new Text[PerPage];
        Text shardText, costText, resultText, pageText;
        PxButton pull1, pull10;
        PxTooltip tip;
        int page;
        List<ItemDef> last = new List<ItemDef>();
        Sprite whiteGem;

        static int Pages => (ItemDatabase.All.Length + PerPage - 1) / PerPage;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (instance == this) instance = null;
        }

        protected override void Build()
        {
            if (instance == null) instance = this;
            whiteGem = PxArt.Derive(PxArt.Get("Common/cursor"), "white", c => c.a > 0 ? new Color32(255, 255, 255, c.a) : c);

            Px.FullFill(Back, "Background", Bg);
            Px.Text(Content, "Title", "유물 개방", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 16, Color.white);
            Px.TextButton(Content, "PrevPage", PxArt.Get("Common/button_small"), 108f, PageY, 20f, 20f, "<", 10,
                          Px.Hex(0x0e1a3c), () => { page = (page + Pages - 1) % Pages; RefreshTip(); });
            pageText = Px.Text(Content, "Page", "", 128f, PageY, 36f, 20f, 10, Color.white);
            Px.TextButton(Content, "NextPage", PxArt.Get("Common/button_small"), 164f, PageY, 20f, 20f, ">", 10,
                          Px.Hex(0x0e1a3c), () => { page = (page + 1) % Pages; RefreshTip(); });

            for (int i = 0; i < PerPage; i++)
            {
                float x = GridX + (i % Cols) * GridStep, y = GridY + (i / Cols) * GridStep;
                var b = Px.Button(Content, "Slot" + i, PxArt.Get("Relic/slot"), x, y, SlotSize, SlotSize, null, out var inner);
                // ◆ 뒤에 한 도트 큰 어두운 ◆ — 밝은 칸 바탕에서 노말(연회색)도 보이게
                gemEdges[i] = Px.Image(inner, "GemEdge", whiteGem, 7f, 6f, 18f, 18f);
                gemEdges[i].color = Px.Ink;
                gems[i] = Px.Image(inner, "Gem", whiteGem, 8f, 7f, 16f, 16f);
                marks[i] = Px.Text(inner, "Mark", "?", 0f, 0f, SlotSize, SlotSize, 13, Px.Hex(0x4a5064));
                counts[i] = Px.Text(inner, "Count", "", 0f, 21f, SlotSize - 3f, 10f, 8, Color.white, TextAnchor.MiddleRight)
                              .WithOutline(Px.Ink);
                AddNav(b);
            }

            Px.Image(Content, "Panel", PxArt.Get("Relic/panel"), Panel.x, Panel.y, Panel.width, Panel.height);
            Px.Image(Content, "Shard", PxArt.Get("Icons/shard_dark"), ShardIcon.x, ShardIcon.y, ShardIcon.width, ShardIcon.height);
            Px.Text(Content, "ShardLabel", "보유 시간의 파편", 424f, 42f, 160f, 14f, 10, Color.white, TextAnchor.MiddleLeft);
            shardText = Px.Text(Content, "Shards", "", 424f, 56f, 160f, 30f, 24, Color.white, TextAnchor.MiddleLeft);
            costText = Px.Text(Content, "Cost", "", 352f, 104f, 256f, 14f, 10, Color.white, TextAnchor.MiddleLeft);

            int total = 0;
            for (int g = 0; g < 4; g++) total += ItemDatabase.Weight((ItemGrade)g);
            for (int g = 0; g < 4; g++)
            {
                var gem = Px.Image(Content, "RateGem" + g, whiteGem, 354f, 126f + g * 18f, 12f, 12f);
                gem.color = ItemDatabase.GradeColor((ItemGrade)g);
                int pct = Mathf.RoundToInt(100f * ItemDatabase.Weight((ItemGrade)g) / total);
                Px.Text(Content, "Rate" + g, $"{GradeNames[g]}  {pct}%", 372f, 124f + g * 18f, 200f, 16f, 10, Color.white,
                        TextAnchor.MiddleLeft);
            }
            Px.Text(Content, "LastLabel", "최근 결과", 352f, 200f, 256f, 14f, 10, Color.white, TextAnchor.MiddleLeft);
            resultText = Px.Text(Content, "Last", "", 352f, 216f, 256f, 72f, 8, Color.white, TextAnchor.UpperLeft);
            resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
            resultText.lineSpacing = 1.2f;

            pull1 = AddNav(Px.TextButton(Content, "Pull1", PxArt.Get("Common/button"), Pull1Box.x, Pull1Box.y, Pull1Box.width,
                                         Pull1Box.height, "1회 개방", 13, Color.white, () => Pull(1)));
            pull10 = AddNav(Px.TextButton(Content, "Pull10", PxArt.Get("Common/button"), Pull10Box.x, Pull10Box.y,
                                          Pull10Box.width, Pull10Box.height, "10회 개방", 13, Color.white, () => Pull(10)));
            Px.IconButton(Content, "Close", PxArt.Get("Common/button_small"), PxArt.Get("Icons/close"), CloseBox.x, CloseBox.y,
                          CloseBox.width, CloseBox.height, 2f, 2f, 19f, 19f, Close);
            tip = new PxTooltip(Content);
        }

        protected override void OnShown() => page = 0;

        protected override void OnEscape() => Close();

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            var inv = p.items;
            pageText.text = $"{page + 1}/{Pages}";
            for (int i = 0; i < PerPage; i++)
            {
                var def = ItemAt(i);
                int n = def != null ? inv.Count(def.id) : 0;
                Nav[i].gameObject.SetActive(def != null);
                if (def == null) continue;
                gems[i].enabled = gemEdges[i].enabled = n > 0;
                gems[i].color = ItemDatabase.GradeColor(def.grade);
                marks[i].enabled = n <= 0;
                counts[i].enabled = n > 0;
                if (n > 0) counts[i].text = n >= ItemDatabase.CapOf(def.id) ? "MAX" : n.ToString();
            }
            shardText.text = p.shards.ToString("N0");
            costText.text = $"1회 개방에 파편 {GachaService.Cost}개 · 천장 {inv.pity}/{GachaService.PityAt}";
            pull1.Interactable = p.shards >= GachaService.Cost;
            pull10.Interactable = p.shards >= GachaService.Cost * 10;
        }

        ItemDef ItemAt(int slot)
        {
            int k = page * PerPage + slot;
            return k < ItemDatabase.All.Length ? ItemDatabase.All[k] : null;
        }

        protected override void OnNavChanged(int index) => RefreshTip();

        void RefreshTip()
        {
            if (tip == null) return;
            int i = NavIndex;
            var def = i >= 0 && i < PerPage ? ItemAt(i) : null;
            if (def == null) { tip.Hide(); return; }
            int n = ProfileService.Current.items.Count(def.id);
            int cap = ItemDatabase.CapOf(def.id);
            string grade = $"<color=#{ColorUtility.ToHtmlStringRGB(ItemDatabase.GradeColor(def.grade))}>{GradeNames[(int)def.grade]}</color>";
            string body = n > 0
                ? $"{def.name}  {grade}\n보유 {n}/{cap}{(n >= cap ? "  MAX" : "")}\n{def.description}"
                : $"미확인 유물  {grade}";
            var r = new Rect(GridX + (i % Cols) * GridStep, GridY + (i / Cols) * GridStep, SlotSize, SlotSize);
            tip.Show(body, r);
        }

        void Pull(int n)
        {
            var p = ProfileService.Current;
            last = GachaService.Pull(p, n);
            if (last.Count == 0)
            {
                CommonPopup.Alert("유물 개방", "뽑을 수 있는 유물이 없다.");
                return;
            }
            SaveService.Save();
            var sb = new System.Text.StringBuilder();
            foreach (var d in last)
            {
                if (sb.Length > 0) sb.Append("  ");
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(ItemDatabase.GradeColor(d.grade))}>{d.name}</color>");
            }
            resultText.text = sb.ToString();
            RefreshTip();
        }
    }
}
