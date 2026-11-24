using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 세이브 슬롯 창의 **모양**(2026-11-23 프리팹 UI 13호) — 어두운 막·창 테두리·제목판·닫기, 슬롯 칸 셋(`Slot1~3` 묶음 — 칸 바탕·제목·내용·마지막 저장·[이어하기]·[초기화]).
    /// 모양은 프리팹 `Assets/Resources/UI/Views/SaveSlotScreenView.prefab`이 원본이고, 위치·크기·그림·글꼴·색은 에디터에서 고친다.
    /// 칸마다 시나리오 이름·진행 요약·저장 시각 글자와 버튼 동작·키보드 순서(1번 이어하기 → 1번 초기화 → 2번 …, 2열 격자)는 <see cref="SaveSlotScreen"/>가 코드로 한다.
    ///
    /// - 슬롯 칸은 <see cref="slots"/>에 1번부터 순서대로. 묶음 안 좌표는 묶음 왼쪽 위 기준이라 묶음을 옮기면 칸 전체가 같이 움직인다.
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/세이브 슬롯 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `SaveSlotScreen.Build`와 같은 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class SaveSlotScreenView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/SaveSlotScreenView";
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

        /// <summary>슬롯 칸 하나 — 제목·내용·마지막 저장 글자와 버튼 둘.</summary>
        [System.Serializable]
        public class SlotRef
        {
            public Text title;
            public Text info;
            public Text time;
            public ButtonRef continueButton = new ButtonRef();
            public ButtonRef resetButton = new ButtonRef();
        }

        public ButtonRef close = new ButtonRef();
        public SlotRef[] slots = new SlotRef[0];

        // ── 처음 모양 — Figma에 없는 화면(2026-11-19 GPT 배치안, 설정 창과 같은 512×304 테두리). 예전 SaveSlotScreen에서 옮김 ──
        static readonly Rect Frame = new Rect(65f, 29f, 512f, 304f);
        static readonly Rect Plate = new Rect(250f, 7f, 128f, 32f);
        static readonly Rect TitleBox = new Rect(262f, 4f, 103f, 37f);
        static readonly Rect CloseBox = new Rect(534f, 17f, 40f, 40f);
        const float CardX = 81f, CardW = 480f, CardH = 80f, TextDX = 11f, BtnDX1 = 271f, BtnDX2 = 375f, BtnW = 96f, BtnH = 32f; // 칸 안 좌표(예전 TextX 92·BtnX 352/456 − 칸 81)
        static readonly float[] CardY = { 52f, 144f, 236f };
        static readonly Color CardFill = new Color(0f, 0f, 0f, 0.35f);
        static readonly Color Gold = new Color32(0xfa, 0xc7, 0x0e, 0xff);
        static readonly Color Sub = new Color(0.82f, 0.84f, 0.9f);

        static ButtonRef Ref(PxButton b) => new ButtonRef { button = b, inner = b.Inner };

        /// <summary>
        /// 예전 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static SaveSlotScreenView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "SaveSlotScreenView");
            var v = root.gameObject.AddComponent<SaveSlotScreenView>();

            Px.FullFill(root, "Dim", new Color(0f, 0f, 0f, 0.55f)); // 판 밖까지(예전엔 PxScreen.Back 층)
            var board = Px.Board(root);
            Px.Image(board, "Frame", PxArt.Get("Settings/window"), Frame.x, Frame.y, Frame.width, Frame.height).raycastTarget = true;
            Px.Image(board, "TitlePlate", PxArt.Get("Common/title_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            Px.Text(board, "Title", "세이브 슬롯", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 16, Color.white);
            v.close = Ref(Px.IconButton(board, "Close", PxArt.Get("Common/close_frame"), PxArt.Get("Icons/close_x"),
                                        CloseBox.x, CloseBox.y, CloseBox.width, CloseBox.height, 0f, 0f, CloseBox.width, CloseBox.height, null));

            v.slots = new SlotRef[CardY.Length];
            for (int i = 0; i < CardY.Length; i++)
            {
                int n = i + 1;
                var g = Px.Node(board, "Slot" + n, CardX, CardY[i], CardW, CardH);
                Px.Fill(g, "Card", CardFill, 0f, 0f, CardW, CardH);
                var s = new SlotRef
                {
                    title = Px.Text(g, "Title", "", TextDX, 6f, 250f, 16f, 13, Gold, TextAnchor.MiddleLeft),
                    info = Px.Text(g, "Info", "", TextDX, 24f, 252f, 36f, 10, Color.white, TextAnchor.UpperLeft),
                    time = Px.Text(g, "Time", "", TextDX, 62f, 252f, 14f, 10, Sub, TextAnchor.MiddleLeft),
                };
                s.info.horizontalOverflow = HorizontalWrapMode.Wrap;
                s.info.lineSpacing = 1.15f;
                s.continueButton = Ref(Px.TextButton(g, "Continue", PxArt.Get("Common/button"), BtnDX1, 24f, BtnW, BtnH, "이어하기", 13, Color.white, null));
                s.resetButton = Ref(Px.TextButton(g, "Reset", PxArt.Get("Common/button"), BtnDX2, 24f, BtnW, BtnH, "초기화", 13, Color.white, null));
                v.slots[i] = s;
            }
            return v;
        }
    }
}
