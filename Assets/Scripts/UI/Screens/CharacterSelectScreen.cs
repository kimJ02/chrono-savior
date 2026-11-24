using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 캐릭터 선택 — Figma "캐릭터 선택창"(4:2)의 **오른쪽 위 구역**(640×360 완성 판, 2026-11-22)을 그대로 옮겼다.
    /// 화면 전체가 **직업별 일러스트 배경**(캐릭터를 고르면 바뀐다)이고 그 위에 왼쪽 위 직업 아이콘·이름, 계열 칸 2개, 아래 직업 선택칸 4개,
    /// 오른쪽 아래 "선택" 버튼이 얹힌다. 계열 칸에 마우스를 올리면 설명 창(Figma "스킬 설명")이 뜨고, 글은 예전과 같은 `SpecTexts` 1티어 설명이다.
    /// 그림은 디자이너가 드라이브에 올린 "캐릭터 선택창.zip" 그대로(`Resources/Art/Figma/UI/CharacterSelect/`).
    /// 칸 순서는 코드 순서(마법사·메카닉·살수·히어로)다 — Figma는 히어로가 3번째지만 사용자가 코드 순서로 두라고 했다(2026-11-22).
    /// 히어로는 아직 없는 캐릭터라 잠겨 있다(Id = null).
    /// 닫기는 ESC뿐이다(Figma에 "뒤로가기" 버튼이 없다). 레벨·SP·한 줄 설명은 새 판에 자리가 없어서 뺐다(사용자 "없어도 됨").
    /// 고르는 동작은 예전과 같다 — 레벨·경험치·SP는 캐릭터마다 따로. 로비에서 연다.
    /// **고르기 = 세이브의 `PlayerProfile.character`를 바꾸고 저장**이다(2026-11-05 2a). 전투의 플레이어(`Characters/PlayerRig`)가 그 값을 따라간다 —
    /// 화면은 월드를 찾지 않는다. 카드 표(`Entries`)에서 고를 수 있는 캐릭터(<see cref="Selectable"/>)는
    /// 전투 씬 Player에 키트가 있어야 한다 — 씬 검증(`Editor/SceneSetupValidator`)이 확인한다(없는 캐릭터를 가리키면 PlayerRig가 프로필을 되돌린다).
    /// </summary>
    public class CharacterSelectScreen : PxScreen
    {
        static CharacterSelectScreen instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;
        public static void Open() { if (instance != null) instance.open = true; }
        public static void Close() { if (instance != null) { instance.open = false; instance.forced = false; } }

        // 2026-11-22 사용자 — 캐릭터는 **회차마다 한 번** 고른다(로비 버튼 없음). 새 원정·환생 직후에 열리고 닫을 수 없다.
        bool forced;

        /// <summary>회차 시작의 캐릭터 고르기 — 고르기 전엔 닫히지 않는다(Esc 무시).</summary>
        public static void OpenForRun() { if (instance != null) { instance.open = true; instance.forced = true; } }

        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Lobby;

        // 모양(좌표·색)은 프리팹 CharacterSelectScreenView에 있다(2026-11-23 프리팹 UI 9호). 여기엔 캐릭터 표(데이터)와 동작만.
        const string Art = "CharacterSelect/";

        struct Entry
        {
            public CharacterId? Id;
            public string Name, Bg, Cls, Icon1, Icon2;
        }

        static readonly Entry[] Entries =
        {
            new Entry { Id = CharacterId.Mage, Name = "마법사", Bg = "bg_mage", Cls = "class_mage", Icon1 = "type_flame", Icon2 = "type_gravity" },
            new Entry { Id = CharacterId.Gunner, Name = "메카닉", Bg = "bg_mechanic", Cls = "class_mechanic", Icon1 = "type_laser", Icon2 = "type_relay" },
            // 살수의 집중 빌드 = 질주 아이콘, 칼날폭풍 빌드 = 칼날 아이콘(Figma 이름만 보고 짝지음 — 틀리면 알려 주세요)
            new Entry { Id = CharacterId.Blade, Name = "살수", Bg = "bg_blade", Cls = "class_blade", Icon1 = "type_dash", Icon2 = "type_blade" },
            new Entry { Id = null, Name = "히어로", Bg = "bg_hero", Cls = "class_hero", Icon1 = null, Icon2 = null },
        };

        /// <summary>칸 수 — 모양(<see cref="CharacterSelectScreenView.CreateFromCode"/>)이 칸을 지을 때 읽는다.</summary>
        internal static int CardCount => Entries.Length;

        /// <summary>i번째 칸의 이름·그림 키(처음 모양을 지을 때만 쓴다 — 실행 중 그림은 <see cref="Refresh"/>가 바꾼다).</summary>
        internal static (string name, string bg, string cls, string icon1) CardLook(int i)
        {
            var e = Entries[Mathf.Clamp(i, 0, Entries.Length - 1)];
            return (e.Name, e.Bg, e.Cls, e.Icon1 ?? Entries[0].Icon1);
        }

        /// <summary>카드에서 고를 수 있는 캐릭터 — 씬 검증이 전투 씬 Player의 키트와 맞춰 본다.</summary>
        public static IEnumerable<CharacterId> Selectable
        {
            get
            {
                foreach (var e in Entries)
                    if (e.Id.HasValue) yield return e.Id.Value;
            }
        }

        CharacterSelectScreenView view;
        Image bg, classIcon, tipIcon;
        GameObject tip;
        Text nameText, soonText, tipName, tipDesc;
        readonly Image[] typeIcons = new Image[2];
        readonly Text[] typeNames = new Text[2];
        readonly GameObject[][] typeParts = new GameObject[2][];
        readonly Text[] slotNames = new Text[4];
        int shownCard;
        int hoveredType = -1;

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
        /// 모양은 프리팹(<see cref="CharacterSelectScreenView"/>, `Resources/UI/Views/CharacterSelectScreenView`)이 원본이다(2026-11-23 프리팹 UI 9호).
        /// 여기선 조각을 받아 오고, 선택칸·선택 버튼 동작과 키보드 순서(선택칸 1~4 → 선택), 계열 칸 마우스 올림만 연결한다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            if (instance == null) instance = this;
            view = LoadView<CharacterSelectScreenView>(CharacterSelectScreenView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[CharacterSelectScreen] 화면 프리팹(Resources/" + CharacterSelectScreenView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/캐릭터 선택 만들기", this);
                view = CharacterSelectScreenView.CreateFromCode(Root);
            }
            if (view.types.Length != 2 || view.slots.Length != Entries.Length || view.slotNames.Length != Entries.Length)
                Debug.LogError($"[CharacterSelectScreen] 화면 프리팹 칸 수가 다르다(계열 {view.types.Length}·선택칸 {view.slots.Length}·이름 {view.slotNames.Length}) " +
                               $"— 계열 2·선택칸 {Entries.Length}·이름 {Entries.Length}이어야 한다", view);

            bg = view.background;
            classIcon = view.classIcon;
            nameText = view.nameText;
            soonText = view.soonText;

            // 계열 칸 둘 — 마우스를 올리면 설명 창
            for (int k = 0; k < 2; k++)
            {
                int kk = k;
                var t = k < view.types.Length ? view.types[k] : null;
                typeIcons[k] = t?.icon;
                typeNames[k] = t?.name;
                typeParts[k] = t != null && t.root != null ? new[] { t.root } : new GameObject[0];
                if (t == null || t.hover == null) continue;
                t.hover.raycastTarget = true;
                var area = t.hover.gameObject.AddComponent<HoverArea>(); // 화면 안쪽 클래스라 프리팹에 저장할 수 없어 실행 중에 붙인다
                area.Enter = () => hoveredType = kk;
                area.Exit = () => { if (hoveredType == kk) hoveredType = -1; };
            }

            // 아래 — 직업 선택칸 4개(처음 누르면 보기, 한 번 더(또는 Enter·선택 버튼) 고르기)
            for (int i = 0; i < Entries.Length; i++)
            {
                int k = i;
                slotNames[i] = i < view.slotNames.Length ? view.slotNames[i] : null;
                var b = i < view.slots.Length ? view.slots[i].Wire(() => { if (shownCard == k) Pick(k); else shownCard = k; }) : null;
                AddNav(b);
            }
            AddNav(view.select.Wire(() => Pick(Mathf.Clamp(shownCard, 0, Entries.Length - 1))));

            // 계열 설명 창
            tip = view.tooltip;
            tipIcon = view.tipIcon;
            tipName = view.tipName;
            tipDesc = view.tipDesc;
            if (tip != null) tip.SetActive(false);
        }

        protected override int DefaultNav
        {
            get
            {
                var cur = ProfileService.Current.character;
                for (int i = 0; i < Entries.Length; i++) if (Entries[i].Id == cur) return i;
                return 0;
            }
        }

        protected override void OnEscape() { if (!forced) Close(); }

        protected override void OnHidden() => hoveredType = -1;

        protected override void OnNavChanged(int index)
        {
            if (index < Entries.Length) shownCard = index;
        }

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            var e = Entries[shownCard];
            bool available = e.Id.HasValue;

            bg.sprite = PxArt.Get(Art + e.Bg);
            classIcon.sprite = PxArt.Get(Art + e.Cls);
            nameText.text = e.Name;
            soonText.gameObject.SetActive(!available);

            var (n1, d1, n2, d2) = BranchesOf(e.Id);
            string[] names = { n1, n2 }, descs = { d1, d2 };
            string[] icons = { e.Icon1, e.Icon2 };
            for (int k = 0; k < 2; k++)
            {
                foreach (var part in typeParts[k]) part.SetActive(available);
                if (!available) continue;
                typeIcons[k].sprite = PxArt.Get(Art + icons[k]);
                typeNames[k].text = names[k].Replace(' ', '\n'); // "폭발 계열" → 두 줄
            }

            bool showTip = available && hoveredType >= 0;
            tip.SetActive(showTip);
            if (showTip)
            {
                tipIcon.sprite = PxArt.Get(Art + icons[hoveredType]);
                tipName.text = names[hoveredType];
                tipDesc.text = descs[hoveredType];
            }

            for (int i = 0; i < Entries.Length; i++)
            {
                bool ok = Entries[i].Id.HasValue;
                Nav[i].Interactable = ok || i == shownCard; // 잠긴 칸도 골라 볼 수는 있게
                if (slotNames[i] != null) slotNames[i].color = p.character == Entries[i].Id ? view.gold : ok ? Color.white : new Color(1f, 1f, 1f, view.lockedNameAlpha);
            }
            Nav[Entries.Length].Interactable = available;
        }

        static (string, string, string, string) BranchesOf(CharacterId? id) => id switch
        {
            CharacterId.Mage => ("폭발 계열", SpecTexts.ExplosionTiers[0], "중력 계열", SpecTexts.GravityTiers[0]),
            CharacterId.Gunner => ("레이저 빌드", SpecTexts.LaserTiers[0], "설치기 빌드", SpecTexts.InstallerTiers[0]),
            CharacterId.Blade => ("집중 빌드", SpecTexts.FocusTiers[0], "칼날폭풍 빌드", SpecTexts.StormTiers[0]),
            _ => ("-", "", "-", ""),
        };

        void Pick(int i)
        {
            var e = Entries[i];
            shownCard = i;
            if (!e.Id.HasValue) return;
            var id = e.Id.Value;
            // 2026-11-22 사용자 — 고르면 경고: 이번 회차에서는 바꿀 수 없다
            CommonPopup.Confirm("캐릭터 선택", $"{e.Name}(으)로 이번 회차를 시작한다.\n이번 회차에서는 캐릭터를 바꿀 수 없다.", () =>
            {
                ProfileService.Current.SetSelectedCharacter(id); // 플레이어(전투 씬)는 다음 프레임에 이 값을 따라간다
                SaveService.Save();
                Close();
            }, "시작", "다시 고르기");
        }

        /// <summary>계열 칸 위에 마우스가 올라온 걸 화면에 알린다(<see cref="PxButton"/>은 키보드 선택용이라 따로 둔다).</summary>
        sealed class HoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public Action Enter, Exit;
            public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
            public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
            void OnDisable() => Exit?.Invoke();
        }
    }
}
