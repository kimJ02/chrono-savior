using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 일시정지 화면의 **모양**(2026-11-22 프리팹 UI 시범 1호) — 띠·"일시정지" 판·아이콘 버튼 셋·조작키 안내.
    /// 모양은 프리팹 `Assets/Resources/UI/Views/PauseMenuView.prefab`이 원본이고, 위치·크기·그림·글자는 에디터에서 고친다.
    /// 동작(버튼이 무엇을 하는지·키보드 순서·언제 보이는지)은 <see cref="PauseMenu"/>가 코드로 연결한다 — 이 컴포넌트는 버튼을 가리키기만 한다.
    ///
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/일시정지 화면 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `PauseMenu.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다 — 그래서 처음 만든 프리팹은 예전 화면과 같다.
    /// 프리팹이 없으면 <see cref="PauseMenu"/>가 경고를 남기고 같은 코드로 짓는다(예전 동작 그대로 — 되돌릴 길).
    /// </summary>
    public class PauseMenuView : MonoBehaviour
    {
        /// <summary>`Resources.Load` 경로(확장자 없이).</summary>
        public const string ResourcePath = "UI/Views/PauseMenuView";
        /// <summary>프리팹 에셋 경로 — 만드는 메뉴가 쓴다.</summary>
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        [Tooltip("키보드(←/→) 순서대로: ▶ 계속 · ⚙ 설정 · ↩ 로비로. 순서를 바꾸면 PauseMenu가 연결하는 동작도 같이 바뀐다.")]
        [SerializeField] PxButton[] buttons = new PxButton[0];
        [Tooltip("버튼마다 눌릴 때 1도트 내려갈 안쪽(아이콘을 담는 자리) — buttons와 같은 순서")]
        [SerializeField] RectTransform[] buttonInners = new RectTransform[0];

        public int ButtonCount => buttons.Length;

        /// <summary>
        /// i번째 버튼을 쓸 수 있게 한다 — 버튼 상태 그림(마우스 올림·눌림)은 실행 중에 만들므로 프리팹에 저장되지 않아 여기서 다시 준비한다.
        /// </summary>
        public PxButton WireButton(int i, System.Action onClick)
        {
            var b = buttons[i];
            b.Init(b.GetComponent<Image>(), i < buttonInners.Length ? buttonInners[i] : null);
            b.OnClick = onClick;
            return b;
        }

        // ── 처음 모양 — Figma "일시정지 메뉴"(32:119, 640×360). 배경 7·6을 원점으로 뺀 좌표(예전 PauseMenu에서 옮김) ──
        static readonly Rect Band = new Rect(0f, 110f, 640f, 128f);      // 42:74
        static readonly Rect Plate = new Rect(256f, 96f, 128f, 32f);     // 77:2
        static readonly Rect TitleBox = new Rect(268f, 93f, 103f, 37f);  // 154:3 (20px 흰색 가운데)
        const float FrameY = 142f, FrameSize = 64f, IconInset = 16f, IconSize = 32f;
        static readonly float[] FrameX = { 148f, 288f, 428f };          // 155:5 · 155:6 · 155:4
        static readonly string[] Icons = { "Icons/play", "Icons/gear", "Icons/exit" };

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static PauseMenuView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "PauseMenuView");
            var view = root.gameObject.AddComponent<PauseMenuView>();

            // 판 밖까지 덮는 어두운 막(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content) 순서 — 그리는 순서가 같다
            Px.FullFill(root, "Backdrop", WithAlpha(Px.Backdrop, Px.BackdropAlpha));
            var board = Px.Board(root);
            Px.Image(board, "Band", PxArt.Get("Pause/band"), Band.x, Band.y, Band.width, Band.height);
            Px.Image(board, "TitlePlate", PxArt.Get("Common/title_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            Px.Text(board, "Title", "일시정지", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 20, Color.white);

            view.buttons = new PxButton[Icons.Length];
            view.buttonInners = new RectTransform[Icons.Length];
            for (int i = 0; i < Icons.Length; i++)
            {
                var b = Px.IconButton(board, "Button" + i, PxArt.Get("Common/icon_frame"), PxArt.Get(Icons[i]),
                                      FrameX[i], FrameY, FrameSize, FrameSize, IconInset, IconInset, IconSize, IconSize, null);
                view.buttons[i] = b;
                view.buttonInners[i] = b.Inner;
            }

            // 조작키 안내 — 예전엔 설정 창에 있었는데 2026-11-16 Figma 새 설정창엔 자리가 없어 여기(띠 아래 빈 곳)로 옮겼다. Figma엔 없는 글자.
            Px.Text(board, "Keys",
                    "←→ 이동 · C 점프 · ↓ 아래로 내려가기 · Z 공격 · X 스킬\nF 포탈 들어가기 · ESC 일시정지 · Enter/Z 메뉴 고르기",
                    0f, 262f, 640f, 32f, 10, Color.white).lineSpacing = 1.3f;
            return view;
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
