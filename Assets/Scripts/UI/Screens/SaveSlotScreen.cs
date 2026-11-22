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

        // 640판 좌표(GPT 배치안 — 설정 창과 같은 512×304 테두리)
        static readonly Rect Frame = new Rect(65f, 29f, 512f, 304f);
        static readonly Rect Plate = new Rect(250f, 7f, 128f, 32f);
        static readonly Rect TitleBox = new Rect(262f, 4f, 103f, 37f);
        static readonly Rect CloseBox = new Rect(534f, 17f, 40f, 40f);
        const float CardX = 81f, CardW = 480f, CardH = 80f, TextX = 92f, BtnX1 = 352f, BtnX2 = 456f, BtnW = 96f, BtnH = 32f;
        static readonly float[] CardY = { 52f, 144f, 236f };
        static readonly Color CardFill = new Color(0f, 0f, 0f, 0.35f);
        static readonly Color Gold = Px.Hex(0xfac70e);
        static readonly Color Sub = new Color(0.82f, 0.84f, 0.9f);

        readonly Text[] titles = new Text[SaveService.SlotCount];
        readonly Text[] lines = new Text[SaveService.SlotCount];
        readonly Text[] times = new Text[SaveService.SlotCount];

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
            Px.FullFill(Back, "Dim", new Color(0f, 0f, 0f, 0.55f));
            Px.Image(Content, "Frame", PxArt.Get("Settings/window"), Frame.x, Frame.y, Frame.width, Frame.height).raycastTarget = true;
            Px.Image(Content, "TitlePlate", PxArt.Get("Common/title_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            Px.Text(Content, "Title", "세이브 슬롯", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 16, Color.white);
            Px.IconButton(Content, "Close", PxArt.Get("Common/close_frame"), PxArt.Get("Icons/close_x"),
                          CloseBox.x, CloseBox.y, CloseBox.width, CloseBox.height, 0f, 0f, CloseBox.width, CloseBox.height, Close);

            for (int i = 0; i < SaveService.SlotCount; i++)
            {
                int slot = i + 1;
                float y = CardY[i];
                Px.Fill(Content, "Card" + slot, CardFill, CardX, y, CardW, CardH);
                titles[i] = Px.Text(Content, "SlotTitle" + slot, "", TextX, y + 6f, 250f, 16f, 13, Gold, TextAnchor.MiddleLeft);
                lines[i] = Px.Text(Content, "SlotInfo" + slot, "", TextX, y + 24f, 252f, 36f, 10, Color.white, TextAnchor.UpperLeft);
                lines[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                lines[i].lineSpacing = 1.15f;
                times[i] = Px.Text(Content, "SlotTime" + slot, "", TextX, y + 62f, 252f, 14f, 10, Sub, TextAnchor.MiddleLeft);
                AddNav(Px.TextButton(Content, "Continue" + slot, PxArt.Get("Common/button"), BtnX1, y + 24f, BtnW, BtnH,
                                     "이어하기", 13, Color.white, () => Continue(slot)));
                AddNav(Px.TextButton(Content, "Reset" + slot, PxArt.Get("Common/button"), BtnX2, y + 24f, BtnW, BtnH,
                                     "초기화", 13, Color.white, () => AskReset(slot)));
            }
#if UNITY_EDITOR
            // 에디터 전용 직업 테스트(2026-11-22 사용자) — 최종 빌드 + 유물 전부 각성으로 바로 들어간다. 슬롯 1~3은 안 건드린다(slot_9).
            var tests = new (string label, CharacterId who, int branch)[]
            {
                ("법사 폭발", CharacterId.Mage, 0), ("법사 중력", CharacterId.Mage, 1),
                ("메카 레이저", CharacterId.Gunner, 0), ("메카 설치", CharacterId.Gunner, 1),
                ("섬영 집중", CharacterId.Blade, 0), ("섬영 폭풍", CharacterId.Blade, 1),
            };
            Px.Text(Content, "TestLabel", "직업 테스트(에디터)", 4f, 336f, 60f, 20f, 8, Gold).WithOutline(Px.Ink);
            for (int k = 0; k < tests.Length; k++)
            {
                var t = tests[k];
                var b = Px.TextButton(Content, "ClassTest" + k, PxArt.Sliced("Common/button", 4), 66f + k * 86f, 336f, 82f, 20f,
                                      t.label, 9, Color.white, () => EnterClassTest(t.who, t.branch));
                b.Art.type = Image.Type.Sliced;
            }
#endif
        }

#if UNITY_EDITOR
        void EnterClassTest(CharacterId who, int branch)
        {
            var p = SavePresets.BuildClassTest(who, branch);
            try { System.IO.File.WriteAllText(SaveService.SlotPath(SavePresets.ClassTestSlot), JsonUtility.ToJson(p, true)); }
            catch (System.Exception e) { CommonPopup.Alert("직업 테스트", "세이브를 쓰지 못했다: " + e.Message); return; }
            Close();
            Flow?.EnterSlot(SavePresets.ClassTestSlot, CommonPopup.AlertIfTransitionFails("직업 테스트"));
        }
#endif

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
                CharacterId.Blade => "섬영",
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
                    CharacterSelectScreen.OpenForRun();
                }
            }));
        }

        void AskReset(int slot) =>
            CommonPopup.Confirm("슬롯 초기화",
                $"슬롯 {slot}의 진행을 지우고\n'{SavePresets.Name(slot)}' 처음 상태로 되돌릴까요?",
                () => SaveService.ResetSlot(slot), "초기화");
    }
}
