using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 전투 HUD의 **모양**(2026-11-23 프리팹 UI 14호) — 피격 붉은 막, 왼쪽 위 플레이어 판(체력 빨강·막대·숫자, 골드 막대·숫자), 남은 초·토벌·버프 줄·COMBO·포탈 안내 글자,
    /// 보유 유물 칸 자리(`RelicStrip`), 스킬 칸 셋(Z·X·C), 미니맵 판과 그릴 자리(`Minimap`), 보스 판(잔상·빨강·막대·이름·해골), 맨 아래 경험치 바, 월드 팝업 글씨 견본.
    /// 모양은 프리팹 `Assets/Resources/UI/Views/CombatHudView.prefab`이 원본이고, 위치·크기·그림·글꼴·색은 에디터에서 고친다.
    /// 값 읽기(전투 창구·프로필)·글자·막대 폭·쿨다운 덮개·보스 등장/잔상·팝업 띄우기는 <see cref="CombatHud"/>가 코드로 한다.
    ///
    /// - 막대 폭(체력·보스·경험치)은 **처음 프리팹 폭이 100%**다 — 채움 칸(`HpFill`·`BossBar/Fill`)·바탕(`ExpBack`) 폭을 바꾸면 그 폭 기준으로 채운다.
    /// - 스킬 칸의 쿨다운 덮개(`Veil0~2`)는 같은 번호 `Dim` 칸 자리 안에서 아래부터 차오른다 — `Dim` 자리가 덮개의 최대 범위다.
    /// - 유물 칸·미니맵 안쪽(점·발판 선)은 보유 유물·맵·몹 위치로 개수가 바뀌어 코드가 그 자리 안에 짓는다(`RelicHudStrip`·`CombatMinimapView`, 사용자 2026-11-23 "틀만 프리팹").
    /// - 팝업 글씨는 견본(`PopupTemplate`, 꺼 둔 채)을 복제해 쓴다. 견본·`RelicStrip`·`Minimap`은 판(Board) 안에 둘 것.
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/전투 HUD 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `CombatHud.Build`와 같은 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class CombatHudView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/CombatHudView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        /// <summary>스킬 칸 하나 — 바탕 흐림(3조각)·쿨다운 덮개(3조각)·쿨다운 숫자·키 글자·충전 스택.</summary>
        [System.Serializable]
        public class SlotRef
        {
            public RectTransform root;
            public Image[] dim = new Image[0];
            public Image[] veil = new Image[0];
            public Text cooldown;
            public Text key;
            public Text stack;
        }

        [Header("판 — 팝업 글씨·유물 설명 상자를 띄울 곳")]
        public RectTransform board;
        public Image hurtFlash;

        [Header("왼쪽 위 플레이어 판")]
        public Image hpFill;
        public Text hpText;
        public Text goldText;

        [Header("글자")]
        public Text timer;
        public PxTextGradient timerGradient;
        public Text kills;
        public Text buffs;
        public Text comboLabel;
        public Text comboNum;
        public Text portal;
        [Tooltip("월드 팝업 글씨 견본(꺼 둔 채) — 필요할 때마다 복제한다")]
        public Text popupTemplate;

        [Header("자리 — 안쪽은 코드가 짓는다")]
        public RectTransform relicStrip;
        public RectTransform minimap;

        [Header("스킬 칸(0 공격 Z · 1 스킬 X · 2 점프 C)")]
        public SlotRef[] slots = new SlotRef[0];

        [Header("보스 판")]
        public RectTransform bossRoot;
        public Text bossName;
        public Image bossSkullLeft, bossSkullRight;
        public Image bossFill, bossTrail;
        [Tooltip("해골과 이름 글자 사이 간격(px)")]
        public float bossSkullGap = 4f;

        [Header("맨 아래 경험치 바")]
        public Image expBack;
        public Image expFill;

        [Header("색")]
        [Tooltip("남은 시간 10초 이하일 때 숫자 위쪽 색(원본 .low)")]
        public Color timerLow = Px.Hex(0xff6a5a);
        [Tooltip("큰 숫자 아래쪽 연회색")]
        public Color numberBottom = Px.Hex(0xc8c8d2);
        public Color stackText = Px.Hex(0x8ef0c8);
        public Color noStackText = Px.Hex(0xff8a7a);
        public Color portalOpen = Px.Hex(0xffd84a);
        public Color portalLocked = Px.Hex(0xd8d0c0);

        // ── 처음 모양 — 2026-11-22 새 HUD(Drive `HUD.zip` 부품 + 사용자 스크린샷, ±1도트), 예전 CombatHud에서 옮김 ──
        static readonly Rect PlayerBg = new Rect(8f, 4f, 168f, 40f);
        static readonly Vector2 PlayerBarPos = new Vector2(4f, 4f);
        static readonly Rect PlayerFill = new Rect(2f, 2f, 156f, 12f);
        static readonly Vector2 GoldBarPos = new Vector2(4f, 20f);
        static readonly Color32 HpRed = new Color32(234, 50, 60, 255);
        static readonly Rect TimerBox = new Rect(280f, 20f, 80f, 26f);
        const int TimerSize = 20;
        static readonly Rect KillsBox = new Rect(220f, 47f, 200f, 12f);
        static readonly Rect ComboLabel = new Rect(533f, 46f, 99f, 35f);
        static readonly Rect ComboNum = new Rect(533f, 81f, 99f, 23f);
        const float SlotX = 29f, SlotY = 303f, SlotStep = 40f, SlotSize = 32f;
        static readonly Rect Minimap = new Rect(526f, 294f, 100f, 50f);
        static readonly Rect RelicStrip = new Rect(504f, 112f, 128f, 132f); // RelicHudStrip 자리(2열 × 64, 11줄 × 12)
        static readonly Rect BossBar = new Rect(116f, 70f, 408f, 40f);
        static readonly Rect BossBarFrame = new Rect(4f, 20f, 400f, 16f);
        static readonly Rect BossFill = new Rect(7f, 23f, 394f, 10f);
        const float BossSkullSize = 16f;
        static readonly Color32 BossTrailColor = new Color32(255, 236, 190, 255);
        static readonly Rect PortalBox = new Rect(120f, 286f, 400f, 12f);
        static readonly Rect BuffBox = new Rect(8f, 47f, 480f, 12f);
        static readonly Color BuffColor = new Color32(0x8e, 0xf0, 0xc8, 0xff);
        static readonly Rect ExpBar = new Rect(0f, 356f, 640f, 4f);
        static readonly Color ExpBorder = Px.Hex(0x472e17), ExpBackColor = Px.Hex(0x57306e), ExpFillColor = Px.Hex(0x44ff00);
        static readonly Color SlotFill = Px.Hex(0x92a1b9);
        static readonly Color SlotCooldownVeil = Px.Hex(0x00000a, 0.75f);
        static readonly Color SlotCooldownText = Px.Hex(0xfff0b8);
        static readonly Color SlotKeyText = Px.Hex(0xc8b890);
        const float IconDimVeil = 1f - 0.34f; // 원본 `opacity:.34`를 바탕색 66% 덮개로
        // 스킬 칸 안쪽(그림 도트): 네 귀퉁이가 한 도트씩 깎인 24×24 — 몸통·맨 윗줄·맨 아랫줄 세 조각
        static readonly Rect[] SlotInner = { new Rect(4f, 5f, 24f, 22f), new Rect(5f, 4f, 22f, 1f), new Rect(5f, 27f, 22f, 1f) };
        public const int PopupSize = 10;

        PxTextGradient Number(Text t)
        {
            var g = t.gameObject.AddComponent<PxTextGradient>();
            g.Top = Color.white;
            g.Bottom = numberBottom;
            t.WithOutline(Px.Ink);
            return g;
        }

        /// <summary>
        /// 예전 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static CombatHudView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "CombatHudView");
            var v = root.gameObject.AddComponent<CombatHudView>();

            // 원본 `state.redFlash` — 맞으면 화면 전체가 잠깐 붉어진다(판 밖까지, 예전엔 PxScreen.Back 층)
            v.hurtFlash = Px.FullFill(root, "HurtFlash", Color.clear, blocksClicks: false);
            var board = v.board = Px.Board(root);

            // 플레이어 판 → 빨강(막대 뒤) → 막대 그림 → 체력 숫자 → 골드 막대 → 골드 글자
            var bg = Px.Image(board, "PlayerBg", PxArt.Get("Hud/player_hp_bg"), PlayerBg.x, PlayerBg.y, PlayerBg.width, PlayerBg.height);
            v.hpFill = Px.Fill(bg.transform, "HpFill", (Color)HpRed, PlayerBarPos.x + PlayerFill.x, PlayerBarPos.y + PlayerFill.y, PlayerFill.width, PlayerFill.height);
            Px.Image(bg.transform, "HpFrame", PxArt.Get("Hud/player_hp_bar"), PlayerBarPos.x, PlayerBarPos.y, 160f, 16f);
            v.hpText = Px.Text(bg.transform, "HpText", "", PlayerBarPos.x, PlayerBarPos.y, 160f, 16f, 8, Color.white).WithOutline(Px.Ink);
            Px.Image(bg.transform, "GoldFrame", PxArt.Get("Hud/gold_bar"), GoldBarPos.x, GoldBarPos.y, 80f, 16f);
            Px.Text(bg.transform, "GoldLabel", "G", GoldBarPos.x + 4f, GoldBarPos.y, 12f, 16f, 8, Color.white, TextAnchor.MiddleLeft).WithOutline(Px.Ink);
            v.goldText = Px.Text(bg.transform, "GoldText", "", GoldBarPos.x + 14f, GoldBarPos.y, 62f, 16f, 8, Color.white, TextAnchor.MiddleRight).WithOutline(Px.Ink);

            // 글자
            v.timer = Px.Text(board, "Timer", "", TimerBox.x, TimerBox.y, TimerBox.width, TimerBox.height, TimerSize, Color.white);
            v.timerGradient = v.Number(v.timer);
            v.kills = Px.Text(board, "Kills", "", KillsBox.x, KillsBox.y, KillsBox.width, KillsBox.height, 8, Color.white).WithOutline(Px.Ink);
            v.buffs = Px.Text(board, "Buffs", "", BuffBox.x, BuffBox.y, BuffBox.width, BuffBox.height, 8, BuffColor, TextAnchor.MiddleLeft)
                        .WithOutline(Px.Ink); // 원본 `text-shadow:0 1px 2px #000`
            v.comboLabel = Px.Text(board, "ComboLabel", "COMBO", ComboLabel.x, ComboLabel.y, ComboLabel.width, ComboLabel.height, 24, Color.white);
            v.Number(v.comboLabel);
            v.comboNum = Px.Text(board, "ComboNum", "", ComboNum.x, ComboNum.y, ComboNum.width, ComboNum.height, 24, Color.white);
            v.Number(v.comboNum);
            v.portal = Px.Text(board, "PortalPrompt", "", PortalBox.x, PortalBox.y, PortalBox.width, PortalBox.height, 10, Color.white).WithOutline(Px.Ink);

            // 보유 유물 칸 자리(2026-11-19 — Figma에 없음, 오른쪽 빈자리). 칸은 코드가 짓는다.
            v.relicStrip = Px.Node(board, "RelicStrip", RelicStrip.x, RelicStrip.y, RelicStrip.width, RelicStrip.height);

            // 스킬 칸 셋
            string[] icons = { "Hud/icon_attack", "Hud/icon_skill", "Hud/icon_jump" };
            string[] keys = { "Z", "X", "C" };
            v.slots = new SlotRef[3];
            for (int i = 0; i < 3; i++)
            {
                var s = new SlotRef { dim = new Image[3], veil = new Image[3] };
                s.root = Px.Node(board, "Slot" + i, SlotX + i * SlotStep, SlotY, SlotSize, SlotSize);
                Px.Image(s.root, "Icon", PxArt.Get(icons[i]), 0f, 0f, SlotSize, SlotSize);
                for (int k = 0; k < 3; k++)
                {
                    var r = SlotInner[k];
                    s.dim[k] = Px.Fill(s.root, "Dim" + k, new Color(SlotFill.r, SlotFill.g, SlotFill.b, IconDimVeil), r.x, r.y, r.width, r.height);
                    s.veil[k] = Px.Fill(s.root, "Veil" + k, SlotCooldownVeil, r.x, r.y, r.width, r.height);
                }
                Px.Image(s.root, "Frame", PxArt.Get("Hud/slot_frame"), 0f, 0f, SlotSize, SlotSize);
                s.cooldown = Px.Text(s.root, "Cooldown", "", 0f, 0f, SlotSize, SlotSize, 10, SlotCooldownText).WithOutline(Px.Ink);
                // 원본 `.key`는 칸 안 아래지만 Figma 칸은 안쪽이 아이콘 그림이라 칸 바로 밑에
                s.key = Px.Text(s.root, "Key", keys[i], 0f, SlotSize + 1f, SlotSize, 9f, 8, SlotKeyText).WithOutline(Px.Ink);
                s.stack = Px.Text(s.root, "Stack", "", 0f, 2f, SlotSize - 3f, 9f, 8, v.stackText, TextAnchor.MiddleRight).WithOutline(Px.Ink);
                v.slots[i] = s;
            }

            // 미니맵 판 + 그릴 자리(점·발판 선은 코드가)
            Px.Image(board, "MinimapBg", PxArt.Get("Hud/minimap_bg"), Minimap.x, Minimap.y, Minimap.width, Minimap.height);
            v.minimap = Px.Node(board, "Minimap", Minimap.x, Minimap.y, Minimap.width, Minimap.height);

            // 보스 판 — 상단 가운데. 막대 그림은 안쪽이 비어 있어(테두리 3도트) 잔상·빨강을 그 뒤에 깐다
            var b = v.bossRoot = Px.Node(board, "BossBar", BossBar.x, BossBar.y, BossBar.width, BossBar.height);
            Px.Image(b, "Bg", PxArt.Get("Hud/boss_hp_bg"), 0f, 0f, BossBar.width, BossBar.height);
            v.bossTrail = Px.Fill(b, "Trail", (Color)BossTrailColor, BossFill.x, BossFill.y, BossFill.width, BossFill.height);
            v.bossFill = Px.Fill(b, "Fill", (Color)HpRed, BossFill.x, BossFill.y, BossFill.width, BossFill.height);
            Px.Image(b, "Frame", PxArt.Get("Hud/boss_hp_bar"), BossBarFrame.x, BossBarFrame.y, BossBarFrame.width, BossBarFrame.height);
            v.bossName = Px.Text(b, "Name", "", 0f, 4f, BossBar.width, 16f, 8, Color.white).WithOutline(Px.Ink);
            v.bossSkullLeft = Px.Image(b, "SkullLeft", PxArt.Get("Hud/boss_skull"), 0f, 4f, BossSkullSize, BossSkullSize);
            v.bossSkullRight = Px.Image(b, "SkullRight", PxArt.Get("Hud/boss_skull"), 0f, 4f, BossSkullSize, BossSkullSize);

            // 맨 아래 경험치 바(테두리 1 + 채움 2)
            Px.Fill(board, "ExpBorder", ExpBorder, ExpBar.x, ExpBar.y, ExpBar.width, ExpBar.height);
            v.expBack = Px.Fill(board, "ExpBack", ExpBackColor, ExpBar.x + 1f, ExpBar.y + 1f, ExpBar.width - 2f, ExpBar.height - 2f);
            v.expFill = Px.Fill(board, "ExpFill", ExpFillColor, ExpBar.x + 1f, ExpBar.y + 1f, 0f, ExpBar.height - 2f);

            // 월드 팝업 글씨 견본(원본 `popDmg`, 2026-11-09) — 꺼 둔 채
            v.popupTemplate = Px.Text(board, "PopupTemplate", "", 0f, 0f, 80f, 12f, PopupSize, Color.white).WithOutline(Px.Ink);
            v.popupTemplate.gameObject.SetActive(false);
            return v;
        }
    }
}
