using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 캐릭터 선택 화면의 **모양**(2026-11-23 프리팹 UI 9호) — 검은 바탕·직업 일러스트 배경·왼쪽 위 직업 틀/아이콘/이름/"준비 중"·
    /// 계열 칸 둘(`Type0`·`Type1` — 틀·아이콘·이름·마우스 올림 자리)·아래 직업 선택칸 넷과 이름·"선택" 버튼·계열 설명 창(`Tooltip`).
    /// 모양은 프리팹 `Assets/Resources/UI/Views/CharacterSelectScreenView.prefab`이 원본이고, 위치·크기·그림·글꼴·색은 에디터에서 고친다.
    /// 무엇을 보여 줄지(고른 직업의 배경·아이콘·이름, 계열 아이콘·이름·설명, 고른 캐릭터 이름 색), 계열 칸·설명 창 켜기,
    /// 버튼 동작·키보드 순서(선택칸 1~4 → 선택)는 <see cref="CharacterSelectScreen"/>가 코드로 한다.
    ///
    /// - 직업 선택칸의 아이콘·이름은 처음 모양에 박혀 있고 실행 중엔 안 바꾼다(칸 순서 = 코드의 캐릭터 표 순서 — 마법사·메카닉·살수·히어로).
    /// - 계열 칸의 마우스 올림 알림은 프리팹에 넣지 않고 실행 중에 `hover` 칸에 붙인다(그 클래스는 화면 안쪽 클래스라 프리팹에 저장할 수 없다).
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/캐릭터 선택 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `CharacterSelectScreen.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class CharacterSelectScreenView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/CharacterSelectScreenView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        /// <summary>버튼 하나 — 버튼과 눌릴 때 1도트 내려갈 안쪽.</summary>
        [System.Serializable]
        public class ButtonRef
        {
            public PxButton button;
            public RectTransform inner;

            public PxButton Wire(System.Action onClick)
            {
                if (button == null) return null;
                button.Init(button.GetComponent<Image>(), inner);
                button.OnClick = onClick;
                return button;
            }
        }

        /// <summary>계열 칸 하나 — 칸 전체(켜고 끄는 단위)·아이콘·이름·마우스 올림 자리.</summary>
        [System.Serializable]
        public class TypeRef
        {
            public GameObject root;
            public Image icon;
            public Text name;
            public Image hover;
        }

        [Header("배경·왼쪽 위")]
        public Image background;
        public Image classIcon;
        public Text nameText;
        public Text soonText;

        [Header("계열 칸 둘")]
        public TypeRef[] types = new TypeRef[0];

        [Header("직업 선택칸·선택 버튼")]
        public ButtonRef[] slots = new ButtonRef[0];
        public Text[] slotNames = new Text[0];
        public ButtonRef select = new ButtonRef();

        [Header("계열 설명 창")]
        public GameObject tooltip;
        public Image tipIcon;
        public Text tipName;
        public Text tipDesc;

        [Header("색 — 고른 캐릭터 이름·준비 중·설명 창 제목")]
        public Color gold = new Color32(0xfa, 0xc7, 0x0e, 0xff);
        [Tooltip("아직 없는(잠긴) 캐릭터 이름의 불투명도")]
        [Range(0f, 1f)] public float lockedNameAlpha = 0.45f;

        // ── 처음 모양 — Figma "캐릭터 선택창"(4:2) 오른쪽 위 구역, 640×360 판(예전 CharacterSelectScreen에서 옮김) ──
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

        static ButtonRef Ref(PxButton b) => new ButtonRef { button = b, inner = b.Inner };

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 그림·이름은 캐릭터 표(<see cref="CharacterSelectScreen.CardLook"/>)의 첫 칸 기준. 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다.
        /// </summary>
        public static CharacterSelectScreenView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "CharacterSelectScreenView");
            var v = root.gameObject.AddComponent<CharacterSelectScreenView>();
            var first = CharacterSelectScreen.CardLook(0);

            // 16:9가 아닌 화면의 남는 곳은 먹색(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content)
            Px.FullFill(root, "Background", Px.Ink);
            var board = Px.Board(root);
            v.background = Px.Image(board, "Bg", PxArt.Get(Art + first.bg), 0f, 0f, PixelCanvas.Width, PixelCanvas.Height);

            // 왼쪽 위 — 고른 직업 아이콘 + 이름
            Px.Image(board, "ClassFrame", PxArt.Get(Art + "class_frame"), ClassFramePos.x, ClassFramePos.y, 64f, 64f);
            v.classIcon = Px.Image(board, "ClassIcon", PxArt.Get(Art + first.cls), ClassIconPos.x, ClassIconPos.y, 32f, 32f);
            v.nameText = Px.Text(board, "Name", "", NamePos.x, NamePos.y, 220f, 28f, 20, Color.white, TextAnchor.MiddleLeft);
            v.soonText = Px.Text(board, "Soon", "준비 중", NamePos.x, NamePos.y + 30f, 120f, 14f, 10, v.gold, TextAnchor.MiddleLeft);

            // 계열 칸 둘 — 칸 하나 = Type0/Type1 묶음(왼쪽 위 = 틀 자리, 안쪽은 묶음 기준)
            v.types = new TypeRef[2];
            for (int k = 0; k < 2; k++)
            {
                var t = Px.Node(board, "Type" + k, TypeX[k], TypeY, TypeHoverW, TypeFrame);
                Px.Image(t, "Frame", PxArt.Get(Art + "type_frame"), 0f, 0f, TypeFrame, TypeFrame);
                var icon = Px.Image(t, "Icon", PxArt.Get(Art + first.icon1), TypeIconInset, TypeIconInset, TypeIcon, TypeIcon);
                var name = Px.Text(t, "Name", "", TypeTextDx, 0f, TypeTextW, TypeTextH, 14, Color.white);
                var hover = Px.Fill(t, "Hover", new Color(0f, 0f, 0f, 0f), 0f, 0f, TypeHoverW, TypeFrame);
                hover.raycastTarget = true;
                v.types[k] = new TypeRef { root = t.gameObject, icon = icon, name = name, hover = hover };
            }

            // 아래 — 직업 선택칸 4개 + 이름, 선택 버튼
            int n = CharacterSelectScreen.CardCount;
            v.slots = new ButtonRef[n];
            v.slotNames = new Text[n];
            for (int i = 0; i < n; i++)
            {
                var look = CharacterSelectScreen.CardLook(i);
                var b = Px.IconButton(board, "Slot" + i, PxArt.Get(Art + "slot"), PxArt.Get(Art + look.cls), SlotX[i], SlotY,
                                      SlotSize, SlotSize, SlotIconX, SlotIconY, SlotIcon, SlotIcon, null);
                v.slots[i] = Ref(b);
                v.slotNames[i] = Px.Text(board, "SlotName" + i, look.name, SlotX[i], SlotLabelY, SlotSize, SlotLabelH, 14, Color.white);
            }
            v.select = Ref(Px.TextButton(board, "Select", PxArt.Get(Art + "select_button"), SelectBox.x, SelectBox.y, SelectBox.width,
                                         SelectBox.height, "선택", 20, Color.white, null));

            // 계열 설명 창 — 맨 위에 그린다(판의 마지막 자식으로 둘 것)
            var tipImg = Px.Image(board, "Tooltip", PxArt.Get(Art + "tooltip"), TipBox.x, TipBox.y, TipBox.width, TipBox.height);
            v.tooltip = tipImg.gameObject;
            v.tipIcon = Px.Image(tipImg.transform, "Icon", PxArt.Get(Art + first.icon1), 25f, 28f, 40f, 40f);
            v.tipName = Px.Text(tipImg.transform, "Name", "", 102f, 28f, 142f, 18f, 14, v.gold, TextAnchor.MiddleLeft);
            v.tipDesc = Px.Text(tipImg.transform, "Desc", "", 102f, 52f, 142f, 128f, 10, Color.white, TextAnchor.UpperLeft);
            v.tipDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            v.tooltip.SetActive(false);
            return v;
        }
    }
}
