using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 설정 창의 **모양**(2026-11-23 프리팹 UI 5호) — 어두운 막·창 틀·제목판·X 닫기·설정 줄 다섯(아이콘·이름·◀ 값 ▶ 또는 음량 막대·숫자칸)·나눔선·"적용".
    /// 모양은 프리팹 `Assets/Resources/UI/Views/SettingsWindowView.prefab`이 원본이고, 위치·크기·그림·글자·색은 에디터에서 고친다.
    /// 값(화면 모드·해상도·음량 숫자·막대 채움·손잡이 자리), 고른 줄 색칠, 버튼·막대 동작, 키보드(↑/↓ 줄, ←/→ 값)는 <see cref="SettingsWindow"/>가 코드로 한다.
    ///
    /// - 줄마다 `Row0`~`Row4`(480×32) 아래에 줄 왼쪽 위 기준으로 놓았다 — 줄 하나를 옮기면 그 줄 전체가 움직인다.
    /// - 음량 손잡이는 같은 줄의 막대 바탕(`VolumeBack`) 자리·폭을 읽어 놓는다 — 막대를 옮기거나 늘려도 따라간다.
    /// - ⚠️ "적용" 버튼 이름 `Apply`는 바꾸지 말 것(테스트가 이름으로 찾는다).
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/설정 창 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `SettingsWindow.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class SettingsWindowView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/SettingsWindowView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        /// <summary>버튼 하나 — 버튼과 눌릴 때 1도트 내려갈 안쪽.</summary>
        [System.Serializable]
        public class ButtonRef
        {
            public PxButton button;
            public RectTransform inner;
        }

        /// <summary>음량 줄 하나 — 바탕·채움·손잡이·누르기 칸.</summary>
        [System.Serializable]
        public class VolumeRef
        {
            public RectTransform back;
            public Image fill;
            public RectTransform knob;
            public PxSlider slider;
        }

        [Header("줄 — 0 화면 모드 · 1 해상도 · 2 마스터 볼륨 · 3 배경음 · 4 효과음")]
        [Tooltip("줄 이름 글자(고른 줄은 아래 색으로 칠해진다)")]
        [SerializeField] Text[] labels = new Text[0];
        [Tooltip("값 글자 — 화면 모드·해상도는 ◀ ▶ 사이, 음량은 숫자칸. 코드가 채운다")]
        [SerializeField] Text[] values = new Text[0];

        [Header("◀ ▶ — 0 화면 모드 · 1 해상도")]
        [SerializeField] ButtonRef[] leftArrows = new ButtonRef[0];
        [SerializeField] ButtonRef[] rightArrows = new ButtonRef[0];

        [Header("음량 막대 — 0 마스터 · 1 배경음 · 2 효과음")]
        [SerializeField] VolumeRef[] volumes = new VolumeRef[0];

        [Header("닫기")]
        [SerializeField] ButtonRef close = new ButtonRef();
        [SerializeField] ButtonRef apply = new ButtonRef();

        [Header("줄 이름 색 — Figma: 고른 줄 #ffe066, 나머지 흰색")]
        [SerializeField] Color selectedColor = new Color32(0xff, 0xe0, 0x66, 0xff);
        [SerializeField] Color normalColor = Color.white;

        public int RowCount => labels.Length;
        public int ValueCount => values.Length;
        public int ArrowRowCount => Mathf.Min(leftArrows.Length, rightArrows.Length);
        public int VolumeCount => volumes.Length;
        public Text Label(int i) => i < labels.Length ? labels[i] : null;
        public Text Value(int i) => i < values.Length ? values[i] : null;
        public Color SelectedColor => selectedColor;
        public Color NormalColor => normalColor;

        static PxButton Wire(ButtonRef r, System.Action onClick)
        {
            if (r == null || r.button == null) return null;
            r.button.Init(r.button.GetComponent<Image>(), r.inner);
            r.button.OnClick = onClick;
            return r.button;
        }

        /// <summary>
        /// 버튼·음량 막대를 쓸 수 있게 한다 — 버튼 상태 그림(마우스 올림·눌림)과 막대 콜백은 실행 중에 만들므로 프리팹에 저장되지 않아 여기서 다시 준비한다.
        /// </summary>
        public void WireSettings(System.Action onClose, System.Action<int, int> onArrow, System.Action<int, int> onVolume, int volumeSteps)
        {
            Wire(close, onClose);
            Wire(apply, onClose);
            for (int i = 0; i < ArrowRowCount; i++)
            {
                int r = i;
                Wire(leftArrows[i], () => onArrow(r, -1));
                Wire(rightArrows[i], () => onArrow(r, +1));
            }
            for (int v = 0; v < volumes.Length; v++)
            {
                int k = v;
                var s = volumes[v].slider;
                if (s == null) continue;
                s.Steps = volumeSteps;
                s.OnChange = value => onVolume(k, value);
            }
        }

        /// <summary>
        /// v번째 음량 막대를 비율(0~1)로 채우고, 손잡이 가운데를 채움 끝에 둔다(막대 밖으로는 안 나가게, 정수 도트).
        /// 손잡이는 같은 부모 안의 막대 바탕 자리·폭을 기준으로 놓는다.
        /// </summary>
        public void ShowVolume(int v, float ratio)
        {
            if (v < 0 || v >= volumes.Length) return;
            var vol = volumes[v];
            if (vol.fill != null) vol.fill.fillAmount = ratio;
            if (vol.knob == null || vol.back == null) return;
            float barW = vol.back.sizeDelta.x, knobW = vol.knob.sizeDelta.x;
            float x = vol.back.anchoredPosition.x + Mathf.Clamp(Mathf.Round(ratio * barW - knobW / 2f), 0f, barW - knobW);
            vol.knob.anchoredPosition = new Vector2(x, vol.knob.anchoredPosition.y);
        }

        /// <summary>테스트용 — 음량 손잡이 왼쪽 x(줄 안 좌표).</summary>
        public float KnobX(int v) => volumes[v].knob.anchoredPosition.x;

        // ── 처음 모양 — Figma "설정창"(414:2), 어두운 배경 -314·-174를 원점으로 뺀 값(예전 SettingsWindow에서 옮김) ──
        static readonly Rect Frame = new Rect(65f, 29f, 512f, 304f);     // 416:23 "설정 배경 1"
        static readonly Rect Plate = new Rect(250f, 7f, 128f, 32f);      // 416:32 "설정"(제목판)
        static readonly Rect TitleBox = new Rect(262f, 4f, 103f, 37f);   // 416:33 글자
        static readonly Rect CloseBox = new Rect(534f, 17f, 40f, 40f);   // 588:12 "나가기_버튼 1" + 416:35 "x 1"
        static readonly Rect ApplyBox = new Rect(250f, 283f, 144f, 32f); // 417:42 "적용버튼 1"
        static readonly Rect Divider = new Rect(83f, 149f, 475f, 3f);    // 416:31 "Rectangle 16"
        const float RowX = 81f, RowW = 480f, RowH = 32f;                 // 설정바
        static readonly float[] RowY = { 72f, 110f, 159f, 197f, 235f };  // 화면 모드(420:93) · 해상도(420:89) · 마스터(419:58) · bgm(419:57) · 효과음(420:72)
        // 줄 안(설정바 왼쪽 위 기준)
        const float IconX = 6f, IconY = 4f, LabelX = 36f, LabelW = 96f;
        const float LeftArrowX = 209f, RightArrowX = 442f, ArrowY = 4f, ArrowSize = 24f, GlyphInset = 4f, GlyphSize = 16f;
        const float ValueBoxX = 240f, ValueBoxY = 6f, ValueBoxW = 192f, ValueBoxH = 20f;
        const float BarX = 209f, BarY = 8f, BarW = 208f, BarH = 16f, KnobW = 20f, KnobH = 24f, KnobY = 4f;
        const float NumberX = 436f, NumberY = 4f, NumberW = 36f, NumberH = 24f;
        static readonly Color Line = new Color32(0xc7, 0xcf, 0xdd, 0xff);
        static readonly string[] RowNames = { "화면 모드", "해상도", "마스터 볼륨", "배경음", "효과음" };
        static readonly string[] RowIcons = { "Settings/icon_screen", "Settings/icon_resolution", "Settings/icon_master", "Settings/icon_bgm", "Settings/icon_sfx" };

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static SettingsWindowView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "SettingsWindowView");
            var view = root.gameObject.AddComponent<SettingsWindowView>();

            // 판 밖까지 덮는 어두운 막(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content) — 그리는 순서가 같다
            Px.FullFill(root, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, 0.35f));
            var board = Px.Board(root);
            Px.Image(board, "Frame", PxArt.Get("Settings/window"), Frame.x, Frame.y, Frame.width, Frame.height)
              .raycastTarget = true; // 창 뒤(일시정지 버튼)가 눌리지 않게
            Px.Image(board, "TitlePlate", PxArt.Get("Common/title_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            Px.Text(board, "Title", "설정", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 20, Color.white);
            var closeBtn = Px.IconButton(board, "Close", PxArt.Get("Common/close_frame"), PxArt.Get("Icons/close_x"),
                                         CloseBox.x, CloseBox.y, CloseBox.width, CloseBox.height, 0f, 0f, CloseBox.width, CloseBox.height, null);
            view.close = new ButtonRef { button = closeBtn, inner = closeBtn.Inner };

            int rows = RowNames.Length;
            view.labels = new Text[rows];
            view.values = new Text[rows];
            view.leftArrows = new ButtonRef[2];
            view.rightArrows = new ButtonRef[2];
            view.volumes = new VolumeRef[rows - 2];
            for (int i = 0; i < rows; i++)
            {
                var row = Px.Node(board, "Row" + i, RowX, RowY[i], RowW, RowH);
                Px.Image(row, "Bar", PxArt.Get("Settings/row"), 0f, 0f, RowW, RowH);
                Px.Image(row, "Icon", PxArt.Get(RowIcons[i]), IconX, IconY, 24f, 24f);
                view.labels[i] = Px.Text(row, "Label", RowNames[i], LabelX, 0f, LabelW, RowH, 13, Color.white);
                if (i < 2)
                {
                    view.leftArrows[i] = ArrowButton(row, LeftArrowX, ArrowY, false);
                    Px.Image(row, "ValueBox", PxArt.Get("Settings/value_box"), ValueBoxX, ValueBoxY, ValueBoxW, ValueBoxH);
                    view.values[i] = Px.Text(row, "Value", "", ValueBoxX, ValueBoxY, ValueBoxW, ValueBoxH, 12, Color.white);
                    view.rightArrows[i] = ArrowButton(row, RightArrowX, ArrowY, true);
                }
                else
                {
                    var vol = new VolumeRef();
                    vol.back = (RectTransform)Px.Image(row, "VolumeBack", PxArt.Get("Settings/volume_back"), BarX, BarY, BarW, BarH).transform;
                    vol.fill = Px.Image(row, "VolumeFill", PxArt.Get("Settings/volume_fill"), BarX, BarY, BarW, BarH);
                    vol.fill.type = Image.Type.Filled;
                    vol.fill.fillMethod = Image.FillMethod.Horizontal;
                    vol.knob = (RectTransform)Px.Image(row, "VolumeKnob", PxArt.Get("Settings/volume_knob"), BarX, KnobY, KnobW, KnobH).transform;
                    // 눌러 끄는 칸 — 손잡이가 넘나드는 자리까지(막대 위아래 4도트)
                    var hit = Px.Fill(row, "VolumeHit", new Color(0f, 0f, 0f, 0f), BarX, KnobY, BarW, KnobH);
                    hit.raycastTarget = true;
                    vol.slider = hit.gameObject.AddComponent<PxSlider>();
                    Px.Image(row, "NumberBox", PxArt.Get("Settings/number_box"), NumberX, NumberY, NumberW, NumberH);
                    view.values[i] = Px.Text(row, "Value", "", NumberX, NumberY, NumberW, NumberH, 12, Color.white);
                    view.volumes[i - 2] = vol;
                }
            }
            Px.Fill(board, "Divider", Line, Divider.x, Divider.y, Divider.width, Divider.height);

            var applyBtn = Px.TextButton(board, "Apply", PxArt.Get("Settings/apply"), ApplyBox.x, ApplyBox.y, ApplyBox.width, ApplyBox.height,
                                         "적용", 16, Color.white, null);
            view.apply = new ButtonRef { button = applyBtn, inner = applyBtn.Inner };
            return view;
        }

        /// <summary>◀/▶ — 틀(설정_버튼 24×24) 안에 화살표(16×16). ▶는 ◀ 그림을 좌우로 뒤집는다.</summary>
        static ButtonRef ArrowButton(Transform row, float x, float y, bool right)
        {
            var b = Px.IconButton(row, right ? "ArrowRight" : "ArrowLeft", PxArt.Get("Settings/arrow_frame"), PxArt.Get("Settings/arrow"),
                                  x, y, ArrowSize, ArrowSize, GlyphInset, GlyphInset, GlyphSize, GlyphSize, null);
            if (right)
            {
                var glyph = (RectTransform)b.Inner.Find("Icon");
                glyph.localScale = new Vector3(-1f, 1f, 1f);
                glyph.anchoredPosition += new Vector2(GlyphSize, 0f); // 왼쪽 위 기준이라 뒤집으면 왼쪽으로 넘어간다
            }
            return new ButtonRef { button = b, inner = b.Inner };
        }
    }
}
