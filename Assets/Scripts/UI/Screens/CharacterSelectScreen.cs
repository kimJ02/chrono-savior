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
        public static void Close() { if (instance != null) instance.open = false; }

        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Lobby;

        static readonly Color Gold = Px.Hex(0xfac70e);

        // ── Figma 좌표(640×360 판 기준 — 배경 일러스트 왼쪽 위가 0,0) ──
        const string Art = "CharacterSelect/";
        static readonly Vector2 ClassFramePos = new Vector2(40f, 51f);   // 64×64 마름모 틀
        static readonly Vector2 ClassIconPos = new Vector2(56f, 67f);    // 32×32 직업 아이콘
        static readonly Vector2 NamePos = new Vector2(114f, 55f);        // 직업 이름(20)
        static readonly float[] TypeX = { 40f, 173f };                   // 계열 칸 둘(64×64), 아이콘은 칸 안 (12,12) 40×40
        const float TypeY = 148f, TypeFrame = 64f, TypeIcon = 40f, TypeIconInset = 12f;
        const float TypeTextDx = 60f, TypeTextW = 64f, TypeTextH = 41f;  // 계열 이름 — 칸 오른쪽 2줄(14)
        const float TypeHoverW = 124f;                                   // 칸 + 이름까지 마우스 올림 자리
        const float SlotY = 263f, SlotSize = 80f, SlotIconX = 24f, SlotIconY = 13f, SlotIcon = 32f;
        static readonly float[] SlotX = { 64f, 164f, 264f, 364f };       // 직업 선택칸 4개
        const float SlotLabelY = 320f, SlotLabelH = 20f;                 // 칸 아래쪽 띠 안의 이름(14)
        static readonly Rect SelectBox = new Rect(485f, 302f, 96f, 32f);
        // 설명 창(256×192) — Figma엔 견본만 있어 자리가 없다. 계열 칸·선택칸을 안 가리는 빈 자리에 둔다.
        static readonly Rect TipBox = new Rect(250f, 60f, 256f, 192f);

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

        /// <summary>카드에서 고를 수 있는 캐릭터 — 씬 검증이 전투 씬 Player의 키트와 맞춰 본다.</summary>
        public static IEnumerable<CharacterId> Selectable
        {
            get
            {
                foreach (var e in Entries)
                    if (e.Id.HasValue) yield return e.Id.Value;
            }
        }

        Image bg, classIcon, tipIcon;
        GameObject tip;
        Text nameText, soonText, tipName, tipDesc;
        readonly Image[] typeIcons = new Image[2];
        readonly Text[] typeNames = new Text[2];
        readonly GameObject[][] typeParts = new GameObject[2][];
        readonly Text[] slotNames = new Text[4];
        int shownCard;
        int hoveredType = -1;

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
            Px.FullFill(Back, "Background", Px.Ink);
            bg = Px.Image(Content, "Bg", PxArt.Get(Art + Entries[0].Bg), 0f, 0f, PixelCanvas.Width, PixelCanvas.Height);

            // 왼쪽 위 — 고른 직업 아이콘 + 이름
            Px.Image(Content, "ClassFrame", PxArt.Get(Art + "class_frame"), ClassFramePos.x, ClassFramePos.y, 64f, 64f);
            classIcon = Px.Image(Content, "ClassIcon", PxArt.Get(Art + Entries[0].Cls), ClassIconPos.x, ClassIconPos.y, 32f, 32f);
            nameText = Px.Text(Content, "Name", "", NamePos.x, NamePos.y, 220f, 28f, 20, Color.white, TextAnchor.MiddleLeft);
            soonText = Px.Text(Content, "Soon", "준비 중", NamePos.x, NamePos.y + 30f, 120f, 14f, 10, Gold, TextAnchor.MiddleLeft);

            // 계열 칸 둘 — 마우스를 올리면 설명 창
            for (int k = 0; k < 2; k++)
            {
                int kk = k;
                float x = TypeX[k];
                var frame = Px.Image(Content, "TypeFrame" + k, PxArt.Get(Art + "type_frame"), x, TypeY, TypeFrame, TypeFrame);
                typeIcons[k] = Px.Image(Content, "TypeIcon" + k, PxArt.Get(Art + Entries[0].Icon1), x + TypeIconInset, TypeY + TypeIconInset,
                                        TypeIcon, TypeIcon);
                typeNames[k] = Px.Text(Content, "TypeName" + k, "", x + TypeTextDx, TypeY, TypeTextW, TypeTextH, 14, Color.white);
                var hover = Px.Fill(Content, "TypeHover" + k, new Color(0f, 0f, 0f, 0f), x, TypeY, TypeHoverW, TypeFrame);
                hover.raycastTarget = true;
                var area = hover.gameObject.AddComponent<HoverArea>();
                area.Enter = () => hoveredType = kk;
                area.Exit = () => { if (hoveredType == kk) hoveredType = -1; };
                typeParts[k] = new[] { frame.gameObject, typeIcons[k].gameObject, typeNames[k].gameObject, hover.gameObject };
            }

            // 아래 — 직업 선택칸 4개(처음 누르면 보기, 한 번 더(또는 Enter·선택 버튼) 고르기)
            for (int i = 0; i < Entries.Length; i++)
            {
                int k = i;
                var b = Px.IconButton(Content, "Slot" + i, PxArt.Get(Art + "slot"), PxArt.Get(Art + Entries[i].Cls), SlotX[i], SlotY,
                                      SlotSize, SlotSize, SlotIconX, SlotIconY, SlotIcon, SlotIcon,
                                      () => { if (shownCard == k) Pick(k); else shownCard = k; });
                slotNames[i] = Px.Text(Content, "SlotName" + i, Entries[i].Name, SlotX[i], SlotLabelY, SlotSize, SlotLabelH, 14, Color.white);
                AddNav(b);
            }
            AddNav(Px.TextButton(Content, "Select", PxArt.Get(Art + "select_button"), SelectBox.x, SelectBox.y, SelectBox.width,
                                 SelectBox.height, "선택", 20, Color.white, () => Pick(Mathf.Clamp(shownCard, 0, Entries.Length - 1))));

            // 계열 설명 창 — 맨 위에 그린다
            var tipImg = Px.Image(Content, "Tooltip", PxArt.Get(Art + "tooltip"), TipBox.x, TipBox.y, TipBox.width, TipBox.height);
            tip = tipImg.gameObject;
            tipIcon = Px.Image(tipImg.transform, "Icon", PxArt.Get(Art + Entries[0].Icon1), 25f, 28f, 40f, 40f);
            tipName = Px.Text(tipImg.transform, "Name", "", 102f, 28f, 142f, 18f, 14, Gold, TextAnchor.MiddleLeft);
            tipDesc = Px.Text(tipImg.transform, "Desc", "", 102f, 52f, 142f, 128f, 10, Color.white, TextAnchor.UpperLeft);
            tipDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            tip.SetActive(false);
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

        protected override void OnEscape() => Close();

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
                slotNames[i].color = p.character == Entries[i].Id ? Gold : ok ? Color.white : new Color(1f, 1f, 1f, 0.45f);
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
            ProfileService.Current.SetSelectedCharacter(e.Id.Value); // 플레이어(전투 씬)는 다음 프레임에 이 값을 따라간다
            SaveService.Save();
            Close();
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
