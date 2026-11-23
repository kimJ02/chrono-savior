using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 세이브 슬롯 창(2026-11-19, 교수님 지시 — 플레이테스트 시나리오 3개). Figma에 없는 화면이라 GPT와 정해 만들었다:
    /// 기존 창 그림(설정 창 테두리·제목판·닫기·버튼)을 재사용하고, 타이틀 화면 모양은 그대로 둔다(타이틀의 "새 원정"·"불러오기"가 이 창을 연다).
    ///
    /// - 슬롯 3개 = 고정 시나리오(<see cref="SavePresets"/>): 새 출발 / 성장 / 중력 법사 풀세팅. 슬롯마다 진행이 따로 저장된다(Hades·Hollow Knight식 슬롯).
    /// - 칸마다 [이어하기]와 [초기화] — 초기화는 그 시나리오 처음으로 되돌린다(확인 팝업). 다음 테스터 전에 운영자가 누른다.
    /// - 칸 내용: 슬롯 번호·시나리오 이름 / 캐릭터·레벨·스킬 / 유물·강화·정복 / 마지막 저장 시각.
    /// </summary>
    public class SaveSlotScreen : PxScreen
    {
        static SaveSlotScreen instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;
        public static void Open() { if (instance != null) instance.open = true; }
        public static void Close() { if (instance != null) instance.open = false; }

        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Title;
        protected override NavLayout Layout => NavLayout.Grid;
        protected override int NavColumns => 2;

        // 모양(좌표·색)은 프리팹 SaveSlotScreenView에 있다(2026-11-23 프리팹 UI 13호). 여기엔 글자 내용·동작만.
        SaveSlotScreenView view;
        readonly Text[] titles = new Text[SaveService.SlotCount];
        readonly Text[] lines = new Text[SaveService.SlotCount];
        readonly Text[] times = new Text[SaveService.SlotCount];

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

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
        /// 모양은 프리팹(<see cref="SaveSlotScreenView"/>, `Resources/UI/Views/SaveSlotScreenView`)이 원본이다(2026-11-23 프리팹 UI 13호).
        /// 여기선 칸마다 글자를 받아 오고, 버튼 동작과 키보드 순서(1번 이어하기 → 1번 초기화 → 2번 …)를 잇는다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            if (instance == null) instance = this;
            view = LoadView<SaveSlotScreenView>(SaveSlotScreenView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[SaveSlotScreen] 화면 프리팹(Resources/" + SaveSlotScreenView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/세이브 슬롯 만들기", this);
                view = SaveSlotScreenView.CreateFromCode(Root);
            }
            if (view.slots.Length != SaveService.SlotCount)
                Debug.LogError($"[SaveSlotScreen] 화면 프리팹 슬롯 칸 수가 다르다({view.slots.Length}) — {SaveService.SlotCount}이어야 한다", view);

            view.close.Wire(Close);
            for (int i = 0; i < SaveService.SlotCount && i < view.slots.Length; i++)
            {
                int slot = i + 1;
                var s = view.slots[i];
                titles[i] = s.title;
                lines[i] = s.info;
                times[i] = s.time;
                AddNav(s.continueButton.Wire(() => Continue(slot)));
                AddNav(s.resetButton.Wire(() => AskReset(slot)));
            }
        }

        protected override void OnShown() => SelectNav(0);

        protected override void OnEscape() => Close();

        protected override void Refresh()
        {
            for (int i = 0; i < SaveService.SlotCount; i++)
            {
                int slot = i + 1;
                titles[i].text = $"슬롯 {slot} · {SavePresets.Name(slot)}";
                var p = SaveService.Peek(slot);
                lines[i].text = p == null ? SavePresets.Blurb(slot) : Summary(p, slot);
                var t = SaveService.SlotTime(slot);
                times[i].text = t.HasValue ? $"마지막 저장 {t.Value:MM-dd HH:mm}" : "비어 있음";
            }
        }

        static string Summary(PlayerProfile p, int slot)
        {
            if (p.needsCharacterSelect) return SavePresets.Blurb(slot);
            string who = p.character switch
            {
                CharacterId.Mage => "마법사",
                CharacterId.Gunner => "메카닉",
                CharacterId.Blade => "살수", // 화면 이름 = 살수(예전 섬영, 2026-11-07) — 2026-11-23 사용자 확인으로 맞춤
                _ => p.character.ToString(),
            };
            string skill = p.character switch
            {
                CharacterId.Mage when p.mageBranch != MageBranch.None => $"{(p.mageBranch == MageBranch.Gravity ? "중력" : "폭발")} {p.mageTier}층",
                CharacterId.Gunner when p.gunnerBranch != GunnerBranch.None => $"{(p.gunnerBranch == GunnerBranch.Laser ? "레이저" : "설치")} {p.gunnerTier}층",
                CharacterId.Blade when p.bladeBranch != BladeBranch.None => $"{(p.bladeBranch == BladeBranch.Focus ? "집중" : "폭풍")} {p.bladeTier}층",
                _ => "스킬 없음",
            };
            int relics = 0;
            foreach (var s in p.items.stacks) if (s.count > 0 && ItemDatabase.Get(s.id) != null) relics++;
            int up = p.upgrades.atk + p.upgrades.hp + p.upgrades.ms + p.upgrades.atkSpeed + p.upgrades.crit;
            int cleared = 0;
            foreach (var b in p.regionBossCleared) if (b) cleared++;
            return $"{who} Lv.{p.level} · {skill}\n유물 {relics}종 · 강화 {up} · 정복 {cleared}지역";
        }

        void Continue(int slot)
        {
            Close();
            Flow?.EnterSlot(slot, CommonPopup.AlertIfTransitionFails("불러오기", () =>
            {
                // 새 출발 — 처음 들어가면 캐릭터부터 고른다(예전 "새 원정"과 같은 흐름)
                if (ProfileService.Current.needsCharacterSelect)
                {
                    ProfileService.Current.needsCharacterSelect = false;
                    SaveService.Save();
                    CharacterSelectScreen.Open();
                }
            }));
        }

        void AskReset(int slot) =>
            CommonPopup.Confirm("슬롯 초기화",
                $"슬롯 {slot}의 진행을 지우고\n'{SavePresets.Name(slot)}' 처음 상태로 되돌릴까요?",
                () => SaveService.ResetSlot(slot), "초기화");
    }
}
