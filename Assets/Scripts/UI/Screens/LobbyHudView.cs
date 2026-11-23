using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 로비 화면 위 표시의 **모양**(2026-11-23 프리팹 UI 10호) — 위 가운데 멸망 판(남은 기간·다음 원정), 왼쪽 위 캐릭터 정보 판,
    /// 왼쪽 아래 빠른 버튼 셋(캐릭터·업적·설정), 오른쪽 아래 미니맵(`Minimap` — 판·땅 선·건물 견본·캐릭터 점), 건물 앞 안내,
    /// 건물 이름/"F / ↑" 글씨 견본, 맨 아래 경험치 바.
    /// 모양은 프리팹 `Assets/Resources/UI/Views/LobbyHudView.prefab`이 원본이고, 위치·크기·그림·글꼴·색은 에디터에서 고친다.
    /// 글자 내용·경험치 비율·미니맵 건물/캐릭터 자리·건물 글씨 자리(월드 따라감)·버튼 동작은 <see cref="LobbyHud"/>가 코드로 한다.
    ///
    /// - 미니맵의 건물 칸은 견본(`Minimap/BuildingTemplate`, 꺼 둔 채)을 건물 수만큼 복제해 가로 자리만 코드가 정한다(세로·크기는 견본 그대로).
    ///   캐릭터 점(`Minimap/Player`)도 가로만 코드가 정한다. 미니맵 안 좌표는 `Minimap` 왼쪽 위 기준.
    /// - 건물 이름·"F / ↑" 글씨는 견본(`BuildingNameTemplate`·`BuildingDoorTemplate`, 꺼 둔 채)을 건물 수만큼 복제해 매 프레임 월드 위치로 옮긴다
    ///   (그래서 견본의 자리는 의미 없고 크기·글꼴·색만 쓰인다). 견본은 판(Board)의 직접 자식으로 둘 것.
    /// - 경험치 채움(`ExpFill`)의 폭 = 비율 × 경험치 바탕(`ExpBack`) 폭.
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/로비 HUD 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `LobbyHud.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class LobbyHudView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/LobbyHudView";
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
                var sliced = button.GetComponent<PxSlicedArt>(); // 9-슬라이스 그림은 프리팹에 안 남는다 — 연결 전에 다시 만든다
                if (sliced != null) sliced.Apply();
                button.Init(button.GetComponent<Image>(), inner);
                button.OnClick = onClick;
                return button;
            }
        }

        [Header("위 가운데 — 멸망 판")]
        public Text doom;
        public Text region;

        [Header("왼쪽 위 — 캐릭터 정보")]
        public Text who;
        public Text money;

        [Header("왼쪽 아래 — 빠른 버튼(0 캐릭터 · 1 업적 · 2 설정)")]
        public ButtonRef[] quickButtons = new ButtonRef[0];

        [Header("오른쪽 아래 — 미니맵")]
        public RectTransform minimap;
        public Image buildingTemplate;
        public Image playerDot;
        [Tooltip("미니맵 안쪽 여백(px) — 건물·캐릭터 점이 놓이는 가로 범위 = 미니맵 폭 − 여백×2")]
        public float minimapInset = 2f;

        [Header("건물 앞 안내·건물 글씨 견본")]
        public Text prompt;
        public Text buildingNameTemplate;
        public Text buildingDoorTemplate;

        [Header("맨 아래 — 경험치 바")]
        public Image expBack;
        public Image expFill;

        /// <summary>견본을 n개 복제해 같은 부모 아래, 견본 바로 뒤에 둔다(견본은 꺼진 채 남는다). 이름 = 접두어 + 번호.</summary>
        public static T[] Clone<T>(T template, int n, string prefix) where T : Component
        {
            var list = new T[n];
            if (template == null) return list;
            for (int i = 0; i < n; i++)
            {
                var c = Instantiate(template, template.transform.parent, false);
                c.name = prefix + i;
                c.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1 + i); // 견본 바로 뒤 — 그리는 순서를 견본 자리에 맞춘다
                c.gameObject.SetActive(true);
                list[i] = c;
            }
            return list;
        }

        /// <summary>미니맵 안 가로 자리(px, 미니맵 왼쪽 기준) — 비율 0~1을 안쪽 범위에 정수 도트로.</summary>
        public float MinimapX(float ratio)
        {
            float w = minimap != null ? minimap.sizeDelta.x : 0f;
            return Mathf.Round(Mathf.Clamp01(ratio) * Mathf.Max(0f, w - minimapInset * 2f)) + minimapInset;
        }

        /// <summary>경험치 바 채우기(비율 0~1) — 바탕 폭에 맞춰 정수 도트로.</summary>
        public void SetExp(float ratio)
        {
            if (expFill == null) return;
            float full = expBack != null ? ((RectTransform)expBack.transform).sizeDelta.x : 638f;
            var rt = (RectTransform)expFill.transform;
            rt.sizeDelta = new Vector2(Mathf.Round(Mathf.Clamp01(ratio) * full), rt.sizeDelta.y);
        }

        // ── 처음 모양 — Figma 로비 뼈대("Page 1"), 640×360 판(예전 LobbyHud에서 옮김) ──
        static readonly Color Gold = new Color32(0xfa, 0xc7, 0x0e, 0xff);
        static readonly Color Plate = new Color(0f, 0f, 0f, 0.38f);
        static readonly Color ExpBorder = Px.Hex(0x472e17), ExpBackColor = Px.Hex(0x57306e), ExpFillColor = Px.Hex(0x44ff00);
        static readonly Rect Minimap = new Rect(504f, 312f, 128f, 32f);
        static readonly string[] QuickLabels = { "캐릭터", "업적", "설정" };

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static LobbyHudView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "LobbyHudView");
            var v = root.gameObject.AddComponent<LobbyHudView>();
            var board = Px.Board(root);

            Px.Fill(board, "DoomPlate", Plate, 190f, 4f, 260f, 38f);
            v.doom = Px.Text(board, "Doom", "", 190f, 6f, 260f, 20f, 16, Gold).WithOutline(Px.Ink);
            v.region = Px.Text(board, "Region", "", 190f, 26f, 260f, 14f, 10, Color.white);

            Px.Fill(board, "InfoPlate", Plate, 6f, 6f, 176f, 38f);
            v.who = Px.Text(board, "Who", "", 12f, 8f, 168f, 18f, 12, Color.white, TextAnchor.MiddleLeft);
            v.money = Px.Text(board, "Money", "", 12f, 26f, 168f, 14f, 10, Gold, TextAnchor.MiddleLeft);

            v.quickButtons = new ButtonRef[QuickLabels.Length];
            for (int i = 0; i < QuickLabels.Length; i++)
            {
                var b = Px.TextButton(board, "Quick" + i, PxArt.Sliced("Common/button", 4), 8f + i * 58f, 326f, 54f, 24f,
                                      QuickLabels[i], 10, Color.white, null);
                b.Art.type = Image.Type.Sliced;
                var sa = b.gameObject.AddComponent<PxSlicedArt>(); // 실행 중 만든 9-슬라이스 그림은 프리팹에 저장되지 않는다 — 경로만 남겨 불러올 때 다시 만든다
                sa.artPath = "Common/button";
                sa.border = 4;
                v.quickButtons[i] = new ButtonRef { button = b, inner = b.Inner };
            }

            // 미니맵 — 묶음 안 좌표는 미니맵 왼쪽 위 기준
            var m = v.minimap = Px.Node(board, "Minimap", Minimap.x, Minimap.y, Minimap.width, Minimap.height);
            Px.Fill(m, "Plate", Plate, 0f, 0f, Minimap.width, Minimap.height);
            Px.Fill(m, "Ground", Px.Ink, 2f, 24f, Minimap.width - 4f, 1f);
            v.buildingTemplate = Px.Fill(m, "BuildingTemplate", Px.Hex(0xc7cfdd), 0f, 16f, 5f, 8f); // 가로 자리는 건물 가운데 - 2(코드)
            v.buildingTemplate.gameObject.SetActive(false);
            v.playerDot = Px.Fill(m, "Player", Color.white, 0f, 22f, 2f, 2f);                       // 가로 자리는 캐릭터 - 1(코드)

            v.prompt = Px.Text(board, "Prompt", "", 170f, 292f, 300f, 14f, 10, Gold).WithOutline(Px.Ink);

            // 건물 글씨 견본 — 월드 건물 위에 화면 해상도로 겹쳐 그린다(월드에 그리면 늘어나 흐렸다, 사용자 2026-10-28)
            v.buildingNameTemplate = Px.Text(board, "BuildingNameTemplate", "", 0f, 0f, 80f, 12f, 8, Color.white).WithOutline(Px.Ink);
            v.buildingDoorTemplate = Px.Text(board, "BuildingDoorTemplate", "F / ↑", 0f, 0f, 80f, 10f, 6, Gold).WithOutline(Px.Ink);
            v.buildingNameTemplate.gameObject.SetActive(false);
            v.buildingDoorTemplate.gameObject.SetActive(false);

            Px.Fill(board, "ExpBorder", ExpBorder, 0f, 356f, 640f, 4f);
            v.expBack = Px.Fill(board, "ExpBack", ExpBackColor, 1f, 357f, 638f, 2f);
            v.expFill = Px.Fill(board, "ExpFill", ExpFillColor, 1f, 357f, 0f, 2f);
            return v;
        }
    }
}
