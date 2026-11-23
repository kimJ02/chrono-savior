using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 사냥 결과 창의 **모양**(2026-11-23 프리팹 UI 6호) — 어두운 막·창 틀·위 띠(제목·스테이지·시간)·붕괴 원인 줄·칸 셋(전리품·경험치·처치 수)·
    /// "원정 통계" 이름표·나눔선·통계 상자(세 줄)·진행 막대(바탕·채움·틀·글자)·"원정 진행도" 이름표·버튼 둘(다시 입장·마을로).
    /// 모양은 프리팹 `Assets/Resources/UI/Views/RunResultScreenView.prefab`이 원본이고, 위치·크기·그림·글꼴·색은 에디터에서 고친다.
    /// 글자 내용(끝난 이유에 따라 제목·칸 순서·버튼 글자가 바뀐다), 막대 채움, 버튼 동작은 <see cref="RunResultScreen"/>가 코드로 채운다.
    ///
    /// ⚠️ 이름을 바꾸지 말 것 — 테스트가 이름으로 찾는다: `Title`·`Cause`·`CellLabel0~2`·`StatLabel0~2`·`BarText0`·`Retry`·`Ok`.
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/결과 창 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `RunResultScreen.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class RunResultScreenView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/RunResultScreenView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        public enum Slot { Retry, Ok }

        [Header("띠 — 코드가 채운다")]
        [SerializeField] Text title;
        [SerializeField] Text sub;
        [SerializeField] Text time;
        [Tooltip("붕괴 원인 줄 — 쓰러졌을 때만 켜진다")]
        [SerializeField] Text cause;

        [Header("칸 셋·통계 세 줄 — 코드가 채운다")]
        [SerializeField] Text[] boxLabels = new Text[0];
        [SerializeField] Text[] boxValues = new Text[0];
        [SerializeField] Text[] statLabels = new Text[0];
        [SerializeField] Text[] statValues = new Text[0];

        [Header("진행 막대")]
        [Tooltip("채움 그림(가로 채우기) — 채움 끝은 이 칸의 폭을 정수 도트로 나눠 맞춘다")]
        [SerializeField] Image barFill;
        [SerializeField] Text barText;

        [Header("버튼 — Retry(다시 입장) · Ok(마을로). 키보드 ←/→ 순서도 이 순서")]
        [SerializeField] PxButton[] buttons = new PxButton[0];
        [SerializeField] RectTransform[] buttonInners = new RectTransform[0];
        [SerializeField] Text[] buttonLabels = new Text[0];

        [Header("색 — 쓰러졌을 때 제목, 평소 제목")]
        [SerializeField] Color deadColor = new Color32(0xff, 0x6a, 0x5a, 0xff);
        [SerializeField] Color titleColor = Color.white;

        public Text Title => title;
        public Text Sub => sub;
        public Text TimeText => time;
        public Text Cause => cause;
        public Text BoxLabel(int i) => i < boxLabels.Length ? boxLabels[i] : null;
        public Text BoxValue(int i) => i < boxValues.Length ? boxValues[i] : null;
        public Text StatLabel(int i) => i < statLabels.Length ? statLabels[i] : null;
        public Text StatValue(int i) => i < statValues.Length ? statValues[i] : null;
        public int BoxCount => Mathf.Min(boxLabels.Length, boxValues.Length);
        public int StatCount => Mathf.Min(statLabels.Length, statValues.Length);
        public Text BarText => barText;
        public int ButtonCount => buttons.Length;
        public Text ButtonLabel(Slot s) => (int)s < buttonLabels.Length ? buttonLabels[(int)s] : null;
        public Color DeadColor => deadColor;
        public Color TitleColor => titleColor;

        /// <summary>진행 막대를 비율(0~1)로 채운다 — 채움 칸 폭을 정수 도트로 나눠 끝이 반 도트에 걸리지 않게(2026-11-20).</summary>
        public void SetProgress(float ratio)
        {
            if (barFill == null) return;
            float px = Mathf.Max(1f, ((RectTransform)barFill.transform).sizeDelta.x);
            barFill.fillAmount = Mathf.Round(Mathf.Clamp01(ratio) * px) / px;
        }

        /// <summary>
        /// 버튼을 쓸 수 있게 한다 — 버튼 상태 그림(마우스 올림·눌림)은 실행 중에 만들므로 프리팹에 저장되지 않아 여기서 다시 준비한다.
        /// </summary>
        public PxButton WireResultButton(Slot s, System.Action onClick)
        {
            int i = (int)s;
            var b = buttons[i];
            b.Init(b.GetComponent<Image>(), i < buttonInners.Length ? buttonInners[i] : null);
            b.OnClick = onClick;
            return b;
        }

        // ── 처음 모양 — Figma "결과창"(77:3, 640×360 판 그대로, 예전 RunResultScreen에서 옮김) ──
        static readonly Rect Frame = new Rect(64f, 28f, 512f, 304f);      // 588:16 "결과백그라운드 1"
        static readonly Rect Banner = new Rect(72f, 36f, 496f, 46f);      // 588:13 "결과배너 1"
        static readonly Rect TitleBox = new Rect(89f, 37f, 260f, 29f);    // 448:48 "원정기록"
        static readonly Rect SubBox = new Rect(89f, 64f, 260f, 16f);      // 448:49 "stage 1-2"
        static readonly Rect TimeBox = new Rect(360f, 36f, 196f, 46f);    // Figma엔 없음 — 띠 오른쪽
        static readonly Rect CauseBox = new Rect(80f, 86f, 480f, 18f);    // Figma엔 없음 — 띠와 칸 사이(쓰러졌을 때만)
        static readonly float[] BoxX = { 108f, 256f, 404f };              // 447:23·25·24 "경과창 프레임"
        const float BoxY = 108f, BoxW = 128f, BoxH = 56f, BoxLine = 27f;  // 칸 안 가로줄 y(그림에서 잰 값)
        static readonly Rect StatsPlate = new Rect(108f, 180f, 64f, 20f); // 447:26 이름표
        static readonly Rect StatsLine = new Rect(172f, 188f, 360f, 4f);  // 590:42 "나눔선 1"
        static readonly Rect StatsBox = new Rect(108f, 226f, 192f, 64f);  // 447:30
        static readonly float[] StatRowY = { 3f, 22f, 41f };              // 통계 상자 안 세 칸 위(그림의 가로줄 21·40 사이)
        const float StatRowH = 18f;
        static readonly Rect ProgressPlate = new Rect(335f, 203f, 64f, 20f); // 590:43 이름표 "원정 진행도"
        const float BarX = 340f, BarY = 223f, BarW = 180f, BarH = 16f;
        const float BarFillPx = 182f;                                     // 채움 그림 폭(칸)
        static readonly Rect RetryBox = new Rect(341f, 278f, 96f, 32f);   // 590:50 "버튼 1"
        static readonly Rect OkBox = new Rect(447f, 278f, 96f, 32f);      // 448:47

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static RunResultScreenView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "RunResultScreenView");
            var view = root.gameObject.AddComponent<RunResultScreenView>();

            // 판 밖까지 덮는 어두운 막(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content) — 그리는 순서가 같다
            Px.FullFill(root, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, Px.BackdropAlpha));
            var board = Px.Board(root);
            Px.Image(board, "Frame", PxArt.Get("Common/window_tall"), Frame.x, Frame.y, Frame.width, Frame.height).raycastTarget = true;
            Px.Image(board, "Banner", PxArt.Get("Result/banner"), Banner.x, Banner.y, Banner.width, Banner.height);
            view.title = Px.Text(board, "Title", "", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 20, Color.white, TextAnchor.MiddleLeft);
            view.sub = Px.Text(board, "Sub", "", SubBox.x, SubBox.y, SubBox.width, SubBox.height, 10, Color.white, TextAnchor.MiddleLeft);
            view.time = Px.Text(board, "Time", "", TimeBox.x, TimeBox.y, TimeBox.width, TimeBox.height, 12, Color.white, TextAnchor.MiddleRight);
            view.cause = Px.Text(board, "Cause", "", CauseBox.x, CauseBox.y, CauseBox.width, CauseBox.height, 10, view.deadColor);

            view.boxLabels = new Text[3];
            view.boxValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                Px.Image(board, "Cell" + i, PxArt.Get("Result/reward_box"), BoxX[i], BoxY, BoxW, BoxH);
                view.boxLabels[i] = Px.Text(board, "CellLabel" + i, "", BoxX[i], BoxY + 3f, BoxW, BoxLine - 3f, 10, Color.white);
                view.boxValues[i] = Px.Text(board, "CellValue" + i, "", BoxX[i], BoxY + BoxLine + 1f, BoxW, BoxH - BoxLine - 4f, 13, Color.white);
            }

            Px.Image(board, "StatsPlate", PxArt.Get("Result/name_plate"), StatsPlate.x, StatsPlate.y, StatsPlate.width, StatsPlate.height);
            Px.Text(board, "StatsLabel", "원정 통계", StatsPlate.x, StatsPlate.y, StatsPlate.width, StatsPlate.height, 10, Color.white);
            Px.Image(board, "StatsLine", PxArt.Get("Result/divider"), StatsLine.x, StatsLine.y, StatsLine.width, StatsLine.height);
            Px.Image(board, "StatsBox", PxArt.Get("Result/stats_box"), StatsBox.x, StatsBox.y, StatsBox.width, StatsBox.height);
            view.statLabels = new Text[3];
            view.statValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                float y = StatsBox.y + StatRowY[i];
                view.statLabels[i] = Px.Text(board, "StatLabel" + i, "", StatsBox.x + 7f, y, 90f, StatRowH, 10, Color.white, TextAnchor.MiddleLeft);
                view.statValues[i] = Px.Text(board, "StatValue" + i, "", StatsBox.x + 92f, y, 92f, StatRowH, 10, Color.white, TextAnchor.MiddleRight);
            }

            // 막대 하나 — 바탕 → 채움(진한 하늘, Figma 색) → 틀 → 글자
            Px.Image(board, "BarBack0", PxArt.Get("Result/bar_back"), BarX, BarY, BarW, BarH);
            view.barFill = Px.Image(board, "BarFill0", PxArt.Get("Result/bar_fill"), BarX - 1f, BarY, BarFillPx, BarH); // 채움 그림은 182칸
            view.barFill.type = Image.Type.Filled;
            view.barFill.fillMethod = Image.FillMethod.Horizontal;
            Px.Image(board, "BarFrame0", PxArt.Get("Result/bar_frame"), BarX, BarY, BarW, BarH);
            view.barText = Px.Text(board, "BarText0", "", BarX, BarY, BarW, BarH, 10, Color.white).WithOutline(Px.Ink);
            Px.Image(board, "ProgressPlate", PxArt.Get("Result/name_plate"), ProgressPlate.x, ProgressPlate.y, ProgressPlate.width, ProgressPlate.height);
            Px.Text(board, "ProgressLabel", "원정 진행도", ProgressPlate.x, ProgressPlate.y, ProgressPlate.width, ProgressPlate.height, 10, Color.white);

            var retry = Px.TextButton(board, "Retry", PxArt.Get("Result/button"), RetryBox.x, RetryBox.y, RetryBox.width,
                                      RetryBox.height, "다시 입장", 12, Color.white, null);
            var ok = Px.TextButton(board, "Ok", PxArt.Get("Result/button"), OkBox.x, OkBox.y, OkBox.width, OkBox.height,
                                   "마을로", 12, Color.white, null);
            view.buttons = new[] { retry, ok };
            view.buttonInners = new[] { retry.Inner, ok.Inner };
            view.buttonLabels = new[] { retry.Inner.GetComponentInChildren<Text>(), ok.Inner.GetComponentInChildren<Text>() };
            return view;
        }
    }
}
