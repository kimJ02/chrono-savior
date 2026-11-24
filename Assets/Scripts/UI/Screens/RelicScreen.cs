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
        protected override int NavColumns => view != null ? view.Columns : 5;

        // 모양(좌표·색)은 프리팹 RelicScreenView에 있다(2026-11-23 프리팹 UI 11호). 여기엔 한 쪽 칸 수·데이터·동작만.
        const int PerPage = 30; // 한 쪽 칸 수(6줄×5열) — 유물 35종을 두 쪽으로

        static readonly string[] GradeNames = { "노말", "레어", "에픽", "레전드" };

        RelicScreenView view;
        readonly Image[] gems = new Image[PerPage];
        readonly Image[] gemEdges = new Image[PerPage];
        readonly Image[] locks = new Image[PerPage]; // 아직 없는 유물 — 자물쇠(Common 그림과 같은 32×32)
        readonly Text[] counts = new Text[PerPage];
        Text shardText, countText, pageText;
        PxButton pull1, pull10;
        PxTooltip tip;
        int page;

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

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

        /// <summary>
        /// 모양은 프리팹(<see cref="RelicScreenView"/>, `Resources/UI/Views/RelicScreenView`)이 원본이다(2026-11-23 프리팹 UI 11호).
        /// 여기선 견본 칸을 30칸으로 복제하고, 확률 판의 데이터(등급 이름·색·확률)를 채우고, 버튼 동작·키보드 순서(칸 0~29 → [1] → [10])를 잇는다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            if (instance == null) instance = this;
            view = LoadView<RelicScreenView>(RelicScreenView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[RelicScreen] 화면 프리팹(Resources/" + RelicScreenView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/유물 소환 만들기", this);
                view = RelicScreenView.CreateFromCode(Root);
            }
            if (view.rateRows.Length != GradeNames.Length)
                Debug.LogError($"[RelicScreen] 화면 프리팹 확률 줄 수가 다르다({view.rateRows.Length}) — {GradeNames.Length}(노말·레어·에픽·레전드)이어야 한다", view);

            var slots = view.BuildSlots(PerPage);
            for (int i = 0; i < PerPage; i++)
            {
                var sl = slots[i];
                gemEdges[i] = sl != null ? sl.gemEdge : null;
                gems[i] = sl != null ? sl.gem : null;
                locks[i] = sl != null ? sl.lockIcon : null;
                counts[i] = sl != null ? sl.count : null;
                AddNav(sl != null ? sl.Wire() : null);
            }
            view.prevPage.Wire(() => { page = (page + Pages - 1) % Pages; RefreshTip(); });
            pageText = view.pageText;
            view.nextPage.Wire(() => { page = (page + 1) % Pages; RefreshTip(); });

            shardText = view.shardText;
            countText = view.countText;

            // 유물 확률 판 — 등급 이름·색·확률은 데이터(ItemDatabase)에서
            int total = 0;
            for (int g = 0; g < GradeNames.Length; g++) total += ItemDatabase.Weight((ItemGrade)g);
            for (int g = 0; g < GradeNames.Length && g < view.rateRows.Length; g++)
            {
                var row = view.rateRows[g];
                if (row.gem != null)
                {
                    var white = row.gem.GetComponent<PxWhiteArt>(); // 흰 ◆(실행 중 만든 그림)를 먼저 다시 넣는다
                    if (white != null) white.Apply();
                    row.gem.color = ItemDatabase.GradeColor((ItemGrade)g);
                }
                if (row.name != null) row.name.text = GradeNames[g] + ":";
                if (row.value != null) row.value.text = Mathf.RoundToInt(100f * ItemDatabase.Weight((ItemGrade)g) / total) + "%";
            }

            pull1 = AddNav(view.pull1.Wire(() => Pull(1)));
            pull10 = AddNav(view.pull10.Wire(() => Pull(10)));
            view.close.Wire(Close);
#if UNITY_EDITOR
            // 에디터 전용 "유물 전부 지급"(2026-11-19 테스트 유물 확인용) — 30종을 모두 상한(각성)까지. 빌드한 게임엔 없다.
            view.testAllRelics.Wire(() =>
            {
                var inv = ProfileService.Current.items;
                foreach (var d in ItemDatabase.All)
                    while (inv.Count(d.id) < ItemDatabase.Cap(d.grade)) inv.Add(d.id);
                SaveService.Save();
            });
#else
            if (view.testAllRelics.button != null) view.testAllRelics.button.gameObject.SetActive(false);
#endif
            tip = new PxTooltip(view.board != null ? view.board : Root);
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
                tip.Show($"{n}회 소환 — 시간의 파편 {GachaService.Cost * n}개\n천장 {ProfileService.Current.items.pity}/{GachaService.PityAt}",
                         RelicScreenView.RectOf(i == PerPage ? pull1 : pull10));
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
            tip.Show(body, view.SlotRect(i));
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
