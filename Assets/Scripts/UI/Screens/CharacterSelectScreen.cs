using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 캐릭터 선택 — Figma "캐릭터 선택창"(4:2)은 **뼈대(와이어프레임)만** 있어서, 뼈대 배치(설명 창 · 1계열/2계열 · 캐릭터 카드 4장 ·
    /// 뒤로가기/선택하기)를 Figma 공용 부품(창 틀·아이콘 칸·버튼)으로 640판에 옮겼다(사용자 "다 넣을거야"). 카드 아이콘은 Figma 새 도트
    /// (모자 = 마법사, 스패너 = 메카닉, 쌍검 = 섬영, 망치 = 4번째 — 아직 없는 캐릭터라 잠김). 고르는 동작은 예전 로비 "캐릭터 선택" 탭과 같다
    /// (`PlayerRig.Select` — 레벨·경험치·SP는 캐릭터마다 따로). 로비에서 연다.
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

        static readonly Color Bg = Px.Hex(0x414141);
        static readonly Color Gold = Px.Hex(0xfac70e);
        static readonly Rect InfoFrame = new Rect(24f, 24f, 240f, 272f);
        const float CardY = 240f, CardSize = 64f; // 아래 "선택하기" 버튼과 안 겹치게
        static readonly float[] CardX = { 300f, 376f, 452f, 528f };
        static readonly Rect BackBox = new Rect(24f, 316f, 96f, 32f);
        static readonly Rect SelectBox = new Rect(520f, 316f, 96f, 32f);

        struct Entry { public CharacterId? Id; public string Name, Icon, Hint; }
        static readonly Entry[] Entries =
        {
            new Entry { Id = CharacterId.Mage, Name = "마법사", Icon = "Icons/weapon_hat", Hint = "Z 차지 마법탄 · X 전문화 스킬. 멀리서 터뜨리는 마법." },
            new Entry { Id = CharacterId.Gunner, Name = "메카닉", Icon = "Icons/weapon_spanner", Hint = "Z 에너지 탄(레이저 빌드는 광선) · X 전문화 스킬. 드론·설치기." },
            new Entry { Id = CharacterId.Blade, Name = "섬영", Icon = "Icons/weapon_blades", Hint = "관성 이동 — 달릴수록 빨라지고 강해진다. 방향키를 누르는 동안 피격 무적." },
            new Entry { Id = null, Name = "???", Icon = "Icons/weapon_hammer", Hint = "아직 준비 중인 캐릭터." },
        };

        Text nameText, levelText, hintText, branch1, branch1Desc, branch2, branch2Desc;
        readonly Text[] cardNames = new Text[4];
        PlayerRig rig;

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
            Px.FullFill(Back, "Background", Bg);
            var mask = Px.Node(Content, "ArtMask", 290f, 0f, 350f, 268f);
            mask.gameObject.AddComponent<RectMask2D>();
            Px.Image(mask, "Art", PxArt.Get("Hud/portrait"), 15f, -18f, 320f, 320f); // 64px 그림 5배

            Px.Image(Content, "Info", PxArt.Get("Stage/frame_info"), InfoFrame.x, InfoFrame.y, InfoFrame.width, InfoFrame.height);
            nameText = Px.Text(Content, "Name", "", 36f, 34f, 216f, 26f, 20, Color.white, TextAnchor.MiddleLeft);
            levelText = Px.Text(Content, "Level", "", 36f, 62f, 216f, 14f, 10, Gold, TextAnchor.MiddleLeft);
            hintText = Px.Text(Content, "Hint", "", 36f, 82f, 216f, 50f, 10, Color.white, TextAnchor.UpperLeft);
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Px.Fill(Content, "Divider", Px.Ink, 36f, 140f, 216f, 1f);
            Px.Text(Content, "Label1", "1계열", 36f, 148f, 60f, 14f, 10, Color.white, TextAnchor.MiddleLeft);
            branch1 = Px.Text(Content, "Branch1", "", 90f, 148f, 162f, 14f, 12, Gold, TextAnchor.MiddleLeft);
            branch1Desc = Px.Text(Content, "Branch1Desc", "", 36f, 166f, 216f, 50f, 8, Color.white, TextAnchor.UpperLeft);
            branch1Desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Px.Text(Content, "Label2", "2계열", 36f, 220f, 60f, 14f, 10, Color.white, TextAnchor.MiddleLeft);
            branch2 = Px.Text(Content, "Branch2", "", 90f, 220f, 162f, 14f, 12, Gold, TextAnchor.MiddleLeft);
            branch2Desc = Px.Text(Content, "Branch2Desc", "", 36f, 238f, 216f, 50f, 8, Color.white, TextAnchor.UpperLeft);
            branch2Desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            for (int i = 0; i < Entries.Length; i++)
            {
                int k = i;
                var b = Px.IconButton(Content, "Card" + i, PxArt.Get("Common/icon_frame"), PxArt.Get(Entries[i].Icon),
                                      CardX[i], CardY, CardSize, CardSize, 16f, 16f, 32f, 32f,
                                      () => { if (shownCard == k) Pick(k); else shownCard = k; }); // 처음 누르면 보기, 한 번 더(또는 Enter) 고르기
                cardNames[i] = Px.Text(Content, "CardName" + i, Entries[i].Name, CardX[i] - 8f, CardY + CardSize + 1f, CardSize + 16f, 10f,
                                       8, Color.white);
                AddNav(b);
            }
            AddNav(Px.TextButton(Content, "Back", PxArt.Get("Common/button"), BackBox.x, BackBox.y, BackBox.width, BackBox.height,
                                 "뒤로가기", 13, Color.white, Close));
            AddNav(Px.TextButton(Content, "Select", PxArt.Get("Common/button"), SelectBox.x, SelectBox.y, SelectBox.width,
                                 SelectBox.height, "선택하기", 13, Color.white, () => Pick(Mathf.Clamp(shownCard, 0, 3))));
        }

        int shownCard;

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

        protected override void OnNavChanged(int index)
        {
            if (index < Entries.Length) shownCard = index;
        }

        PlayerRig Rig => rig != null ? rig : rig = Object.FindFirstObjectByType<PlayerRig>();

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            var e = Entries[shownCard];
            nameText.text = e.Name;
            hintText.text = e.Hint;
            bool available = e.Id.HasValue && (Rig == null || Rig.HasKit(e.Id.Value));
            if (e.Id.HasValue)
            {
                var cp = p.ProgressOf(e.Id.Value);
                levelText.text = $"Lv.{cp.level} · SP {cp.level - 1 - cp.spUsed}" + (p.character == e.Id ? "   (지금 캐릭터)" : "");
            }
            else levelText.text = "준비 중";

            var (n1, d1, n2, d2) = BranchesOf(e.Id);
            branch1.text = n1; branch1Desc.text = d1;
            branch2.text = n2; branch2Desc.text = d2;

            for (int i = 0; i < Entries.Length; i++)
            {
                bool ok = Entries[i].Id.HasValue && (Rig == null || Rig.HasKit(Entries[i].Id.Value));
                Nav[i].Interactable = ok || i == shownCard; // 잠긴 카드도 골라 볼 수는 있게
                cardNames[i].color = p.character == Entries[i].Id ? Gold : ok ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }
            Nav[Entries.Length + 1].Interactable = available;
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
            if (!e.Id.HasValue || Rig == null || !Rig.HasKit(e.Id.Value)) return;
            if (Rig.Select(e.Id.Value)) SaveService.Save();
            Close();
        }
    }
}
