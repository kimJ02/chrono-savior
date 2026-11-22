using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 유물 소환(가챠) — 드라이브 새 그림 + 사용자 스크린샷(2026-11-22): 큰 창(512×304) 위에 제목판 "유물 소환"·닫기, 왼쪽 **6줄×5열 = 30칸 격자**,
    /// 오른쪽 **보유 시간의 파편 · 보유 유물 수 · 유물 확률(노말/레어/에픽/레전드) · [1]·[10] 소환 버튼**. 로비의 "가챠" 건물에서 연다.
    /// 동작은 원본 가챠(`GachaService`)와 같다.
    ///
    /// 확률은 **원본 값**(55·30·12·3%)이다 — 스크린샷의 70·27·2·1%는 Figma 옛 판 값(사용자 확인 전).
    /// 유물은 원본대로 35종이라 30칸 두 쪽으로 넘겨 본다(스크린샷은 "7/30" — 30종 가정인지 확인 필요). 쪽 넘김은 스크린샷에 자리가 없어
    /// 격자 밑에 작은 &lt; &gt;와 마우스 휠로 둔다. 원본 아이콘은 이모지라 도트 글꼴에 없다 — 칸엔 등급 색 ◆와 개수, 이름·효과는 칸을 고르면 뜨는 설명 상자에.
    /// 아직 없는 유물은 어두운 칸(유물칸 비활성화) + 자물쇠. 소환 결과는 팝업으로 알린다(스크린샷엔 결과 자리가 없다).
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

        // 스크린샷에서 잰 값(640판 환산, ±1도트) — 창 왼쪽 위(W)를 기준으로 한 상대 좌표
        static readonly Color Bg = Px.Hex(0x414141);
        static readonly Vector2 W = new Vector2(64f, 36f);                   // 창 512×304(Common/window_tall)
        static readonly Rect Plate = new Rect(192f, -15f, 128f, 32f);        // 제목판(Common/title_plate)
        static readonly Rect CloseBox = new Rect(478f, -4f, 40f, 40f);       // 나가기 버튼(Common/close_frame + close_x)
        static readonly Rect GridPanel = new Rect(23f, 44f, 220f, 236f);     // 유물칸 백그라운드
        const float SlotX = 43f, SlotY = 52f, SlotStep = 37f, SlotSize = 32f; // 칸 32, 간격 5
        const int Cols = 5, Rows = 6, PerPage = Cols * Rows;
        const float RightX = 257f, RightW = 240f;
        static readonly Rect ShardBar = new Rect(RightX, 44f, RightW, 32f);  // 유물 설명 막대 1
        static readonly Rect CountBar = new Rect(RightX, 84f, RightW, 32f);  // 유물 설명 막대 2
        static readonly Rect RatePanel = new Rect(RightX, 131f, RightW, 120f);
        static readonly Rect Pull1Box = new Rect(259f, 255f, 96f, 32f);
        static readonly Rect Pull10Box = new Rect(398f, 255f, 96f, 32f);
        static readonly float[] RateRowY = { 16f, 44f, 73f, 103f };           // 확률 줄 가운데 y(판 안쪽 기준)
        static readonly float[] RateLineY = { 30f, 58f, 88f };                // 줄 사이 가는 선
        static readonly Color RateValue = Px.Hex(0xfff0c0);
        static readonly Color RateLine = Px.Hex(0x30364e);
        // 스크린샷에 없는 것 — 쪽 넘김(격자 밑 빈틈)
        const float PageY = 282f, PageBtnW = 18f, PageBtnH = 14f;

        static readonly string[] GradeNames = { "노말", "레어", "에픽", "레전드" };

        readonly Image[] gems = new Image[PerPage];
        readonly Image[] gemEdges = new Image[PerPage];
        readonly Image[] locks = new Image[PerPage]; // 아직 없는 유물 — 자물쇠(Common 그림과 같은 32×32)
        readonly Text[] counts = new Text[PerPage];
        Text shardText, countText, pageText;
        PxButton pull1, pull10;
        PxTooltip tip;
        int page;
        Sprite whiteGem;

        static int Pages => (ItemDatabase.All.Length + PerPage - 1) / PerPage;
        static Rect SlotRect(int i) => new Rect(W.x + SlotX + (i % Cols) * SlotStep, W.y + SlotY + (i / Cols) * SlotStep, SlotSize, SlotSize);

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
            Px.Image(Content, "Window", PxArt.Get("Common/window_tall"), W.x, W.y, 512f, 304f);
            Px.Image(Content, "TitlePlate", PxArt.Get("Common/title_plate"), W.x + Plate.x, W.y + Plate.y, Plate.width, Plate.height);
            Px.Text(Content, "Title", "유물 소환", W.x + Plate.x, W.y + Plate.y, Plate.width, Plate.height, 20, Color.white);
            Px.Image(Content, "GridPanel", PxArt.Get("Relic/grid_panel"), W.x + GridPanel.x, W.y + GridPanel.y, GridPanel.width, GridPanel.height);

            for (int i = 0; i < PerPage; i++)
            {
                var r = SlotRect(i);
                var b = Px.Button(Content, "Slot" + i, PxArt.Get("Relic/slot"), r.x, r.y, SlotSize, SlotSize, null, out var inner);
                // ◆ 뒤에 한 도트 큰 어두운 ◆ — 밝은 칸 바탕에서 노말(연회색)도 보이게
                gemEdges[i] = Px.Image(inner, "GemEdge", whiteGem, 7f, 6f, 18f, 18f);
                gemEdges[i].color = Px.Ink;
                gems[i] = Px.Image(inner, "Gem", whiteGem, 8f, 7f, 16f, 16f);
                locks[i] = Px.Image(inner, "Lock", PxArt.Get("Relic/lock"), 0f, 0f, SlotSize, SlotSize); // 32×32 그대로(1:1)
                counts[i] = Px.Text(inner, "Count", "", 0f, 21f, SlotSize - 3f, 10f, 8, Color.white, TextAnchor.MiddleRight)
                              .WithOutline(Px.Ink);
                AddNav(b);
            }
            Px.TextButton(Content, "PrevPage", PxArt.Sliced("Common/button", 4), W.x + GridPanel.x + 76f, W.y + PageY, PageBtnW, PageBtnH, "<", 8, Color.white,
                          () => { page = (page + Pages - 1) % Pages; RefreshTip(); }).Art.type = Image.Type.Sliced;
            pageText = Px.Text(Content, "Page", "", W.x + GridPanel.x + 76f + PageBtnW, W.y + PageY, 48f, PageBtnH, 8, Color.white);
            Px.TextButton(Content, "NextPage", PxArt.Sliced("Common/button", 4), W.x + GridPanel.x + 76f + PageBtnW + 48f, W.y + PageY, PageBtnW, PageBtnH, ">", 8, Color.white,
                          () => { page = (page + 1) % Pages; RefreshTip(); }).Art.type = Image.Type.Sliced;

            // 오른쪽: 보유 시간의 파편 · 보유 유물 수
            Px.Image(Content, "ShardBar", PxArt.Get("Relic/info_bar"), W.x + ShardBar.x, W.y + ShardBar.y, ShardBar.width, ShardBar.height);
            Px.Image(Content, "ShardIcon", PxArt.Get("Icons/shard"), W.x + ShardBar.x + 40f, W.y + ShardBar.y + 8f, 16f, 16f);
            shardText = Px.Text(Content, "Shards", "", W.x + ShardBar.x + 60f, W.y + ShardBar.y, 170f, ShardBar.height, 13, Color.white, TextAnchor.MiddleLeft);
            Px.Image(Content, "CountBar", PxArt.Get("Relic/info_bar"), W.x + CountBar.x, W.y + CountBar.y, CountBar.width, CountBar.height);
            countText = Px.Text(Content, "OwnedCount", "", W.x + CountBar.x, W.y + CountBar.y, CountBar.width, CountBar.height, 13, Color.white);

            // 유물 확률 판 — 판 위에 걸친 작은 이름표
            float rx = W.x + RatePanel.x, ry = W.y + RatePanel.y;
            Px.Image(Content, "RatePanel", PxArt.Get("Relic/rate_panel"), rx, ry, RatePanel.width, RatePanel.height);
            int total = 0;
            for (int g = 0; g < 4; g++) total += ItemDatabase.Weight((ItemGrade)g);
            for (int g = 0; g < 4; g++)
            {
                float cy = ry + RateRowY[g];
                var gem = Px.Image(Content, "RateGem" + g, whiteGem, rx + 7f, cy - 6f, 12f, 12f);
                gem.color = ItemDatabase.GradeColor((ItemGrade)g);
                Px.Text(Content, "RateName" + g, GradeNames[g] + ":", rx + 32f, cy - 10f, 120f, 20f, 16, Color.white, TextAnchor.MiddleLeft);
                int pct = Mathf.RoundToInt(100f * ItemDatabase.Weight((ItemGrade)g) / total);
                Px.Text(Content, "RateValue" + g, pct + "%", rx + 116f, cy - 10f, 100f, 20f, 16, RateValue, TextAnchor.MiddleRight);
            }
            for (int k = 0; k < RateLineY.Length; k++)
                Px.Fill(Content, "RateLine" + k, RateLine, rx + 20f, ry + RateLineY[k], 198f, 1f);
            var plate = Px.Image(Content, "RatePlate", PxArt.Sliced("Common/title_plate", 4), rx + 89f, ry - 10f, 62f, 19f);
            plate.type = Image.Type.Sliced;
            Px.Text(Content, "RatePlateText", "유물 확률", rx + 89f, ry - 10f, 62f, 19f, 11, Color.white).WithOutline(Px.Ink);

            pull1 = AddNav(Px.TextButton(Content, "Pull1", PxArt.Get("Relic/pull1"), W.x + Pull1Box.x, W.y + Pull1Box.y, Pull1Box.width,
                                         Pull1Box.height, "1", 16, Color.white, () => Pull(1)));
            pull10 = AddNav(Px.TextButton(Content, "Pull10", PxArt.Get("Relic/pull10"), W.x + Pull10Box.x, W.y + Pull10Box.y,
                                          Pull10Box.width, Pull10Box.height, "10", 16, Color.white, () => Pull(10)));
            Px.IconButton(Content, "Close", PxArt.Get("Common/close_frame"), PxArt.Get("Icons/close_x"),
                          W.x + CloseBox.x, W.y + CloseBox.y, CloseBox.width, CloseBox.height, 0f, 0f, CloseBox.width, CloseBox.height, Close);
#if UNITY_EDITOR
            // 에디터 전용 "유물 전부 지급"(2026-11-19 테스트 유물 확인용) — 30종을 모두 상한(각성)까지. 빌드한 게임엔 없다.
            var all = Px.TextButton(Content, "TestAllRelics", PxArt.Sliced("Common/button", 4), 352f, 272f, 120f, 16f, "유물 전부 각성 (테스트)", 8,
                                    Color.white, () =>
                                    {
                                        var inv = ProfileService.Current.items;
                                        foreach (var d in ItemDatabase.All)
                                            while (inv.Count(d.id) < ItemDatabase.Cap(d.grade)) inv.Add(d.id);
                                        SaveService.Save();
                                    });
            all.Art.type = Image.Type.Sliced;
#endif
            tip = new PxTooltip(Content);
        }

        protected override void OnShown() => page = 0;

        protected override void OnEscape() => Close();

        /// <summary>마우스 휠 — 쪽을 넘긴다(스크린샷에 쪽 넘김 자리가 없어서 더한 것).</summary>
        protected override void OnKeys()
        {
            float wheel = GameInput.MenuScroll;
            if (wheel == 0f) return;
            page = Mathf.Clamp(page + (wheel > 0f ? -1 : 1), 0, Pages - 1);
            RefreshTip();
        }

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            var inv = p.items;
            pageText.text = $"{page + 1}/{Pages}";
            int owned = 0;
            foreach (var d in ItemDatabase.All) if (inv.Count(d.id) > 0) owned++;
            for (int i = 0; i < PerPage; i++)
            {
                var def = ItemAt(i);
                int n = def != null ? inv.Count(def.id) : 0;
                Nav[i].gameObject.SetActive(def != null);
                if (def == null) continue;
                Nav[i].SetSprite(PxArt.Get(n > 0 ? "Relic/slot" : "Relic/slot_off"));
                gems[i].enabled = gemEdges[i].enabled = n > 0;
                gems[i].color = ItemDatabase.GradeColor(def.grade);
                locks[i].enabled = n <= 0;
                counts[i].enabled = n > 0;
                if (n > 0) counts[i].text = n >= ItemDatabase.CapOf(def.id) ? "각성" : n.ToString();
            }
            shardText.text = $"보유 시간의 파편: {p.shards:N0}";
            countText.text = $"보유 유물 수: {owned}/{ItemDatabase.All.Length}";
            pull1.Interactable = p.shards >= GachaService.Cost;
            pull10.Interactable = p.shards >= GachaService.Cost * 10;
        }

        /// <summary>테스트용 — 그 쪽(0부터)으로 넘기고 갱신한다.</summary>
        public void PageForTest(int p) { page = Mathf.Clamp(p, 0, Pages - 1); Refresh(); }
        /// <summary>테스트용 — 이 쪽에서 켜져 있는 칸 수.</summary>
        public int VisibleSlotCount { get { int n = 0; for (int i = 0; i < PerPage; i++) if (Nav[i].gameObject.activeSelf) n++; return n; } }
        public string OwnedCountText => countText.text;
        public string ShardText => shardText.text;

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
            if (i == PerPage || i == PerPage + 1) // [1]·[10] — 값은 스크린샷 글자엔 없어서 설명 상자로
            {
                int n = i == PerPage ? 1 : 10;
                var box = i == PerPage ? Pull1Box : Pull10Box;
                tip.Show($"{n}회 소환 — 시간의 파편 {GachaService.Cost * n}개\n천장 {ProfileService.Current.items.pity}/{GachaService.PityAt}",
                         new Rect(W.x + box.x, W.y + box.y, box.width, box.height));
                return;
            }
            var def = i >= 0 && i < PerPage ? ItemAt(i) : null;
            if (def == null) { tip.Hide(); return; }
            int have = ProfileService.Current.items.Count(def.id);
            int cap = ItemDatabase.CapOf(def.id);
            string grade = $"<color=#{ColorUtility.ToHtmlStringRGB(ItemDatabase.GradeColor(def.grade))}>{GradeNames[(int)def.grade]}</color>";
            // 2026-11-19 테스트 유물 — 누구 전용·어느 갈래인지, 랭크(상한 = 각성), 지금 캐릭터에서 켜지는지
            string who = def.owner == RelicOwner.Common ? "공용"
                : $"{ItemDatabase.OwnerName(def.owner)} 전용 · {ItemDatabase.BranchName(def.branch)}";
            bool on = Relics.IsActiveFor(ProfileService.Current, def);
            string body = have > 0
                ? $"{def.name}  {grade}  <color=#9aa4b8>{who}</color>\n랭크 {Mathf.Min(have, cap)}/{cap}{(have >= cap ? "  <color=#fac70e>각성</color>" : "")}"
                  + (on ? "" : "  <color=#9aa4b8>(지금 꺼짐)</color>") + $"\n{def.description}"
                : $"미확인 유물  {grade}  <color=#9aa4b8>{who}</color>";
            tip.Show(body, SlotRect(i));
        }

        void Pull(int n)
        {
            var p = ProfileService.Current;
            var got = GachaService.Pull(p, n);
            if (got.Count == 0)
            {
                CommonPopup.Alert("유물 소환", "뽑을 수 있는 유물이 없다.");
                return;
            }
            SaveService.Save();
            // 같은 유물은 묶어서 "이름 ×2" — 10회 결과가 길어지지 않게
            var order = new List<ItemDef>();
            var counts = new Dictionary<string, int>();
            foreach (var d in got)
            {
                if (!counts.ContainsKey(d.id)) { counts[d.id] = 0; order.Add(d); }
                counts[d.id]++;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var d in order)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(ItemDatabase.GradeColor(d.grade))}>{d.name}</color>");
                if (counts[d.id] > 1) sb.Append($" ×{counts[d.id]}");
            }
            CommonPopup.Alert("유물 소환", sb.ToString());
            RefreshTip();
        }
    }
}
