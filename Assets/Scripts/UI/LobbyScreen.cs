using UnityEngine;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 로비 — 원본 `#lobby`(project_test.html:512)와 `renderLobby()`(`:6378`)에 대응한다.
    /// 탭 구성은 원본과 같다: **캐릭터 선택 / 스테이지 / 강화 / 전문화 / 시간 회귀 / 기원 / 업적**.
    ///
    /// ## 왜 배경을 판으로 덮나
    /// 로비는 사냥터를 **가리지 않고 그 위에 뜬다**(원본도 캔버스는 계속 그리고 `#lobby`를 겹친다).
    /// 그래서 판 없이 글자만 얹으면 몬스터·발판 위에서 흰 글씨가 그대로 묻힌다. 원본은 읽어야 할
    /// 본문을 `rgba(14,11,20,.92)` 판으로 덮고, 판 밖의 헤더에만 `text-shadow`를 준다 —
    /// 색과 치수는 전부 <see cref="UiTheme"/>에 옮겨 놨다.
    ///
    /// ## 왜 IMGUI(OnGUI)인가
    /// 이 프로젝트는 **렌더링·비주얼 전부가 명시적으로 범위 밖**이고(원형 스프라이트 유지, 사용자 확인),
    /// 지금 필요한 건 "로비로 나가서 캐릭터를 고르고 강화를 찍고 다시 들어간다"는 **동작**이다.
    /// uGUI로 만들면 같은 기능에 씬 빌더 코드가 수백 줄 붙는데, 그 코드는 나중에 실제 아트가 들어올 때
    /// 어차피 전부 갈린다. UI 층은 아래 층을 읽고 공개 메서드를 부르기만 하므로(README 참고)
    /// 나중에 uGUI로 바꿔도 **이 층 밖은 하나도 안 바뀐다.**
    ///
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyScreen : MonoBehaviour
    {
        enum Tab { Character, Stage, Upgrade, Spec, Regression, Gacha, Achieve }

        static readonly string[] TabNames = { "캐릭터 선택", "스테이지", "강화", "전문화", "시간 회귀 ◈", "기원 🎴", "업적 🏆" };

        Tab tab = Tab.Character;
        RunController run;
        PlayerRig rig;
        Vector2 scroll;
        string toast = "";
        float toastLeft;
        bool confirmingReset;
        RegressionWindow regression;

        void Awake()
        {
            run = Object.FindFirstObjectByType<RunController>();
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) rig = player.GetComponent<PlayerRig>();
        }

        void Update()
        {
            // 로비는 `Time.timeScale = 0`에서 돌아간다(RunController 설계) — 스케일된 시간은 흐르지 않는다.
            if (toastLeft > 0f) toastLeft -= Time.unscaledDeltaTime;
        }

        // 원본 레이아웃 그대로(project_test.html:85 `#lobbyHead`, `:177` `#lobbyTabs`, `:180` `#lobbyBody`).
        // CSS의 padding/margin을 그대로 픽셀로 옮긴 값이라 숫자에 의미가 있다.
        const float Pad = 22f;          // 좌우 여백 — 헤더·탭·본문이 같은 선에 선다
        const float TabsY = 68f;
        const float TabHeight = 34f;
        const float TabGap = 6f;        // 원본 `#lobbyTabs { gap:6px }`
        const float BodyTop = TabsY + TabHeight;
        const float BodyBottom = 16f;   // 원본 `#lobbyBody { margin:0 22px 16px }`

        void OnGUI()
        {
            if (GameState.Current != GameScene.Lobby) return;
            if (PxScreens.Has<LobbyHud>()) return; // 걸어 다니는 Figma 로비가 대신한다(2026-10-24)
            if (run == null) run = Object.FindFirstObjectByType<RunController>();

            using var scaled = UiTheme.Scaled(); // 원본 fitScreen()과 같은 1280×720 좌표계

            var profile = ProfileService.Current;

            DrawHeader(profile);
            DrawTabs();

            // ⚠️ 가독성의 핵심 — 원본 `#lobbyBody { background:rgba(14,11,20,.92) }`(`:180`).
            // 로비는 사냥터 **위에** 뜬다. 이 판이 없으면 글자가 몬스터·발판과 섞여서 안 읽힌다
            // (원본도 헤더/탭만 배경 위에 얹고, 정작 읽어야 할 본문은 거의 불투명한 판으로 덮는다).
            var body = new Rect(Pad, BodyTop, UiTheme.DesignWidth - Pad * 2f,
                                UiTheme.DesignHeight - BodyTop - BodyBottom);
            GUI.Box(body, GUIContent.none, UiTheme.Panel);

            var inner = new Rect(body.x + 18f, body.y + 14f, body.width - 36f, body.height - 28f);
            GUILayout.BeginArea(inner);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case Tab.Character: DrawCharacterTab(profile); break;
                case Tab.Stage: DrawStageTab(profile); break;
                case Tab.Upgrade: DrawUpgradeTab(profile); break;
                case Tab.Spec: DrawSpecTab(profile); break;
                case Tab.Regression: DrawRegressionTab(profile); break;
                case Tab.Gacha: DrawGachaTab(profile); break;
                case Tab.Achieve: DrawAchieveTab(profile); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawToast();
        }

        /// <summary>
        /// 원본 `#lobbyHead`(project_test.html:85). **판 위가 아니라 배경 위에 그대로 얹히는** 줄이라
        /// 원본도 여기에만 `text-shadow`를 준다 — 그림자 말고는 대비를 만들 수단이 없다.
        /// </summary>
        void DrawHeader(PlayerProfile profile)
        {
            UiTheme.ShadowLabel(new Rect(Pad, 10f, 240f, 38f), "Chrono Savior", UiTheme.Title);

            float x = Pad + 175f;
            // 레벨·SP는 지금 고른 캐릭터 것이다(캐릭터마다 따로, PlayerProfile.charProgress) — 누구 것인지 이름을 붙인다.
            UiTheme.ShadowLabel(new Rect(x, 12f, 900f, 22f),
                $"{CharacterLabel(profile.character)} Lv.<b>{profile.level}</b>    골드 <b>{profile.gold}</b> G    " +
                $"SP <b>{profile.SpAvailable}</b> (총 {profile.SpTotal})    " +
                $"시간 회귀 <b>{profile.regressions}</b>회    ◈ <b>{profile.shards}</b>",
                UiTheme.HeadStat);

            // 원본 `#lobbyExpbar` 180×10(`:90`).
            long need = PlayerProfile.RequiredExp(profile.level);
            UiTheme.Bar(new Rect(x, 40f, 180f, 10f),
                        need > 0 ? profile.exp / (float)need : 0f, UiTheme.ExpFill);
            UiTheme.ShadowLabel(new Rect(x + 190f, 34f, 400f, 20f),
                $"경험치 {profile.exp} / {need}", UiTheme.HeadStat);

            // 원본 로비 헤더의 "전체 초기화"(`:524`). 실수로 누르면 큰일이라 두 번 묻는다
            // — 원본은 confirm() 대화상자를 쓰는데 IMGUI엔 그게 없어서 버튼 상태로 대신한다.
            float right = UiTheme.DesignWidth - Pad;
            if (!confirmingReset)
            {
                if (GUI.Button(new Rect(right - 100f, 14f, 100f, 28f), "전체 초기화")) confirmingReset = true;
            }
            else
            {
                UiTheme.ShadowLabel(new Rect(right - 320f, 18f, 120f, 22f), "정말 지울까?", UiTheme.HeadStat);
                if (GUI.Button(new Rect(right - 190f, 14f, 110f, 28f), "네, 지웁니다"))
                {
                    SaveService.DeleteSave();
                    confirmingReset = false;
                    ShowToast("전체 초기화 완료");
                }
                if (GUI.Button(new Rect(right - 70f, 14f, 70f, 28f), "취소")) confirmingReset = false;
            }
        }

        /// <summary>
        /// 원본 `.tabbtn`(project_test.html:178). `GUILayout.Toolbar`를 안 쓰는 이유는 그게 폭을
        /// 균등 분할하기 때문이다 — 원본 탭은 **글자 폭만큼**이고 왼쪽에 몰려 있어서, 균등 분할하면
        /// 짧은 탭에 빈 공간이 크게 남아 모양이 달라진다.
        /// </summary>
        void DrawTabs()
        {
            float x = Pad;
            for (int i = 0; i < TabNames.Length; i++)
            {
                var content = new GUIContent(TabNames[i]);
                float w = UiTheme.Tab.CalcSize(content).x;
                bool on = (int)tab == i;

                if (GUI.Toggle(new Rect(x, TabsY, w, TabHeight), on, content, UiTheme.Tab) && !on)
                {
                    tab = (Tab)i;
                    scroll = Vector2.zero;
                }
                x += w + TabGap;
            }
        }

        /// <summary>원본 `#toast`(project_test.html:230) — 화면 위쪽 가운데에 잠깐 뜬다.</summary>
        void DrawToast()
        {
            if (toastLeft <= 0f) return;

            var content = new GUIContent(toast);
            Vector2 size = UiTheme.Toast.CalcSize(content);
            GUI.Box(new Rect((UiTheme.DesignWidth - size.x) * 0.5f, 150f, size.x, size.y),
                    content, UiTheme.Toast);
        }

        // ────────────────────────── 캐릭터 선택 ──────────────────────────

        /// <summary>
        /// 원본 `renderCharacterSelect()`(project_test.html:6389 경로). 고르면 곧바로
        /// <see cref="PlayerRig.Select"/>로 키트가 교체된다 — **여기서 게임 상태를 직접 만지지 않는다.**
        /// </summary>
        void DrawCharacterTab(PlayerProfile profile)
        {
            GUILayout.Label("이 캐릭터로 사냥에 나간다. 캐릭터마다 이동 방식과 Z 기본공격이 다르다.");
            GUILayout.Space(6f);

            foreach (CharacterId id in System.Enum.GetValues(typeof(CharacterId)))
            {
                bool implemented = rig != null && rig.HasKit(id);
                bool current = profile.character == id;

                GUILayout.BeginHorizontal();
                // 원본 캐릭터 카드처럼 캐릭터마다 자기 레벨을 보인다(`renderCharacterSelect` :6418 `'Lv.' + p.level`,
                // SP는 정보 패널의 `cp.level - 1 - cp.spUsed` :6424). 레벨·SP가 캐릭터별이라 여기서 봐야 다른 캐릭터 진행을 안다.
                var cp = profile.ProgressOf(id);
                string label = CharacterLabel(id)
                               + (implemented ? $"   Lv.{cp.level} · SP {cp.level - 1 - cp.spUsed}" : "")
                               + (current ? "   ← 선택됨" : "");
                if (!implemented) label += "   (미구현)";

                GUI.enabled = implemented && !current;
                if (GUILayout.Button(label, GUILayout.Height(30f), GUILayout.Width(360f)))
                {
                    if (rig.Select(id)) ShowToast($"{CharacterLabel(id)} 선택");
                }
                GUI.enabled = true;

                GUILayout.Label(CharacterHint(id));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            GUILayout.Label("드루이드는 기획에서 빠져 고를 수 없다. 전문화(스킬트리)는 지금 고른 캐릭터 것이 전문화 탭에 뜬다.");
            GUILayout.Label("레벨·경험치·SP는 캐릭터마다 따로 오른다. 골드·골드 강화·시간의 파편·유물은 모든 캐릭터가 같이 쓴다.");
        }

        static string CharacterLabel(CharacterId id) => id switch
        {
            CharacterId.Mage => "마법사",
            CharacterId.Gunner => "메카닉",
            CharacterId.Blade => "섬영",
            CharacterId.Druid => "드루이드",
            _ => id.ToString(),
        };

        static string CharacterHint(CharacterId id) => id switch
        {
            CharacterId.Mage => "Z 차지 마법탄 · X 전문화 스킬",
            CharacterId.Gunner => "Z 에너지 탄(레이저 빌드는 광선) · X 전문화 스킬",
            CharacterId.Blade => "관성 이동 — 달릴수록 강해진다",
            CharacterId.Druid => "클로 3타 콤보 · 마나",
            _ => "",
        };

        // ────────────────────────── 스테이지 ──────────────────────────

        /// <summary>원본 `renderStageTab()`(project_test.html:6441).</summary>
        void DrawStageTab(PlayerProfile profile)
        {
            GUILayout.Label($"제한시간 {RunState.NormalTime:0}초 동안 몬스터를 잡아 재화를 모은다. " +
                            "지역이 높을수록 강한 종이 나오고 몹 체력·피해가 지수로 오른다.");
            GUILayout.Label("<b>보스는 로비에서 고르지 않는다</b> — 필드 우측 끝의 포탈로 들어간다. " +
                            $"포탈은 그 지역 누적 토벌 {RunState.RegionKillTarget}마리를 채우면 열린다.");
            GUILayout.Space(6f);

            for (int r = 1; r <= RegionConfig.Count; r++)
            {
                bool open = profile.IsRegionOpen(r);
                int fee = RegionConfig.EntryFee(r);
                bool canPay = profile.gold >= fee;
                bool bossReady = profile.IsBossUnlocked(r);

                GUILayout.BeginHorizontal();

                // **버튼이 지역마다 하나다.** 원본은 일반/보스 버튼을 나란히 뒀지만(:6464),
                // 사용자 지시(2026-09-16)로 보스는 필드 안의 포탈로 들어가게 바뀌었다 —
                // 스테이지가 늘어날 때 로비가 버튼 벽이 되는 것을 막기 위한 변경이다.
                GUI.enabled = open && canPay;
                if (GUILayout.Button($"{r}. {RegionConfig.NameOf(r)}", GUILayout.Height(28f), GUILayout.Width(200f)))
                    EnterRegion(profile, r, fee, RunMode.Normal);
                GUI.enabled = true;

                string info = $"권장 Lv.{RegionConfig.RecommendedLevel(r)}+   입장료 {(fee == 0 ? "무료" : fee + " G")}" +
                              $"   토벌 {profile.RegionKills(r)} / {RunState.RegionKillTarget}";
                if (profile.IsBossCleared(r)) info += "   ✔ 격파";
                else if (bossReady) info += "   🌀 포탈 열림";
                if (!open) info += "   🔒 이전 지역 보스를 잡아야 열린다";
                else if (!canPay) info += "   ← 골드 부족";
                GUILayout.Label(info);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            GUILayout.Label($"지역에서 {RunState.RegionKillTarget}마리를 토벌하면 <b>필드 우측 끝의 포탈</b>이 열린다. " +
                            "포탈 앞에서 <b>F</b>를 누르면 보스 필드로 넘어가고, 제한시간이 " +
                            $"{RunState.BossTime:0}초로 새로 시작한다(처치 수·살기·번 재화는 그대로 유지된다). " +
                            "보스를 잡으면 다음 지역이 열린다.");
        }

        /// <summary>원본 `startRun`의 입장료 처리(project_test.html:4297~4303).</summary>
        void EnterRegion(PlayerProfile profile, int region, int fee, RunMode mode)
        {
            if (fee > 0)
            {
                if (profile.gold < fee) { ShowToast($"입장료 부족 — {fee} G 필요"); return; }
                profile.gold -= fee;
                ShowToast($"입장료 {fee} G 지불");
            }
            run?.StartRun(region, mode);
        }

        // ────────────────────────── 강화 ──────────────────────────

        /// <summary>
        /// 원본 `renderUpgradeTab()`(project_test.html:6974) — **이게 원래 강화가 있어야 할 자리**다.
        /// 인게임 숫자키 강화(`PlayerDebugController`)는 원본에 없는 임시방편이라 곧 삭제된다.
        /// </summary>
        void DrawUpgradeTab(PlayerProfile profile)
        {
            GUILayout.Label("골드로 영구 강화를 산다. **효과는 선형인데 비용은 지수로 오른다** — 원본 그대로다.");
            GUILayout.Space(6f);

            foreach (UpgradeStat stat in System.Enum.GetValues(typeof(UpgradeStat)))
            {
                int level = profile.GetUpgradeLevel(stat);
                int cost = GoldUpgradeConfig.Cost(stat, level);
                bool canBuy = profile.gold >= cost;

                GUILayout.BeginHorizontal();
                GUI.enabled = canBuy;
                if (GUILayout.Button($"{UpgradeLabel(stat)}  +{level}   →   {cost} G",
                                     GUILayout.Height(28f), GUILayout.Width(320f)))
                {
                    // 골드 차감과 단계 상승은 프로필이 원자적으로 처리한다(원본 `buyGoldUpgrade` :7042).
                    if (profile.TryBuyUpgrade(stat))
                    {
                        ShowToast($"{UpgradeLabel(stat)} +{level + 1}");
                        SaveService.Save(); // 원본도 구매 직후 `saveMeta()`(:7097)
                    }
                }
                GUI.enabled = true;
                GUILayout.Label(UpgradeHint(stat));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            GUILayout.Label("체력 강화는 **다음 사냥에 입장할 때** 반영된다 — 원본도 최대체력을 레벨업과 런 시작에만 " +
                            "다시 계산한다(:1850·:1515). 로비에서 산 즉시 최대체력이 오르지 않는 게 정상이다.");
        }

        static string UpgradeLabel(UpgradeStat stat) => stat switch
        {
            UpgradeStat.Atk => "공격력",
            UpgradeStat.Hp => "체력",
            UpgradeStat.Ms => "이동속도",
            UpgradeStat.AtkSpeed => "공격속도",
            UpgradeStat.Crit => "치명타",
            _ => stat.ToString(),
        };

        static string UpgradeHint(UpgradeStat stat) => stat switch
        {
            UpgradeStat.Atk => "모든 피해에 곱해진다",
            UpgradeStat.Hp => "최대 체력",
            UpgradeStat.Ms => "이동 속도",
            UpgradeStat.AtkSpeed => "공격 쿨다운이 짧아진다",
            UpgradeStat.Crit => "치명타 확률",
            _ => "",
        };

        // ────────────────────────── 전문화 ──────────────────────────

        /// <summary>
        /// 원본 `renderSpecTab()`(project_test.html:6971) — SP로 빌드를 **찍는** 곳. **지금 고른 캐릭터**의
        /// 트리를 보여준다(원본 `specWeapon = meta.weapon`). 갈래는 처음 찍을 때 고정되고 되돌릴 수 없으며,
        /// 반드시 1→5 순서로만 올라간다(판정은 전부 `PlayerProfile.TryLearn*Tier`가 한다 — UI는 부르기만).
        /// </summary>
        void DrawSpecTab(PlayerProfile profile)
        {
            switch (profile.character)
            {
                case CharacterId.Mage: DrawMageSpec(profile); break;
                case CharacterId.Gunner: DrawGunnerSpec(profile); break;
                case CharacterId.Blade: DrawBladeSpec(profile); break;
                default:
                    GUILayout.Label($"{CharacterLabel(profile.character)}의 전문화는 아직 구현되지 않았다. " +
                                    "현재 스킬트리가 있는 캐릭터는 마법사·메카닉·섬영이다.");
                    break;
            }
        }

        void DrawSpecHeader(PlayerProfile profile, string branchNow, int tier)
        {
            GUILayout.Label($"{CharacterLabel(profile.character)} SP {profile.SpAvailable}개 사용 가능 (레벨 1당 1개, 캐릭터마다 따로). " +
                            "**한 갈래만 선택**하며, 각 층에서 Z 공격과 X 스킬이 함께 강화된다.");
#if UNITY_EDITOR
            DrawTestSpButton(profile);
#endif
            GUILayout.Space(4f);
            GUILayout.Label($"현재: {branchNow} · {tier}층");
            if (tier == 0)
                GUILayout.Label("⚠️ 0층에서는 X 스킬이 나가지 않는다 — 원본도 `tier < 1`이면 막는다(:2169).");
            GUILayout.Space(8f);
        }

#if UNITY_EDITOR
        /// <summary>
        /// **에디터 전용 테스트 버튼**(사용자 요청 2026-09-29 — 스킬을 한 층씩 찍어 보며 테스트하려고). 누를 때마다 SP 1개.
        ///
        /// SP는 원본처럼 레벨에서 나오므로(`spTotal() = level - 1`, project_test.html:1258) **레벨을 1 올린다** —
        /// SP만 따로 더하는 칸을 세이브에 만들지 않으려는 것. 경험치는 그대로 두므로 다음 레벨 요구치 아래에 남는다.
        /// 레벨이 오르면 몹 레벨 페널티가 줄고 경험치 구슬이 커지니, 밸런스를 볼 때는 쓰지 말 것.
        ///
        /// `#if UNITY_EDITOR`라 빌드에는 들어가지 않는다. CLAUDE.md의 "원본에 없던 임시 디버그 키를 되살리지 말 것"은
        /// 실제 게임에 들어가는 것을 막는 규칙이라, 빌드에서 빠지는 에디터 전용 도구(`Editor/MechanicTestMenu`와 같은
        /// 취급)는 해당하지 않는다. 전문화 탭 머리말(<see cref="DrawSpecHeader"/>)에 붙어서 트리가 있는 캐릭터마다 뜬다.
        /// </summary>
        void DrawTestSpButton(PlayerProfile profile)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SP +1 (테스트)", GUILayout.Height(24f), GUILayout.Width(140f)))
            {
                profile.level++;
                SaveService.Save(); // 스킬을 찍을 때처럼 바로 저장 — 에디터를 끄기 전에 자동 저장이 안 돌아도 남는다
                ShowToast($"SP +1 — 레벨 {profile.level}, 사용 가능 SP {profile.SpAvailable}");
            }
            GUILayout.Label("에디터 전용 — 레벨이 1 오른다(빌드한 게임엔 없다).");
            GUILayout.EndHorizontal();
        }
#endif

        void DrawMageSpec(PlayerProfile profile)
        {
            var b = profile.mageBranch;
            string branchNow = b == MageBranch.None ? "아직 안 정함" : (b == MageBranch.Explosion ? "폭발 계열" : "중력 계열");
            DrawSpecHeader(profile, branchNow, profile.mageTier);

            DrawBranchRows(profile, "폭발 계열", SpecTexts.ExplosionTiers, b == MageBranch.Explosion,
                b != MageBranch.None && b != MageBranch.Explosion, profile.mageTier,
                () => profile.TryLearnMageTier(MageBranch.Explosion));
            GUILayout.Space(10f);
            DrawBranchRows(profile, "중력 계열", SpecTexts.GravityTiers, b == MageBranch.Gravity,
                b != MageBranch.None && b != MageBranch.Gravity, profile.mageTier,
                () => profile.TryLearnMageTier(MageBranch.Gravity));
        }

        /// <summary>메카닉 — 원본 `SPEC.gunner`(project_test.html:915): 캐릭터 레이저 빌드(move) / 설치기 빌드(conv).</summary>
        void DrawGunnerSpec(PlayerProfile profile)
        {
            var b = profile.gunnerBranch;
            string branchNow = b == GunnerBranch.None ? "아직 안 정함" : (b == GunnerBranch.Laser ? "레이저 빌드" : "설치기 빌드");
            DrawSpecHeader(profile, branchNow, profile.gunnerTier);

            DrawBranchRows(profile, "레이저 빌드", SpecTexts.LaserTiers, b == GunnerBranch.Laser,
                b != GunnerBranch.None && b != GunnerBranch.Laser, profile.gunnerTier,
                () => profile.TryLearnGunnerTier(GunnerBranch.Laser));
            GUILayout.Space(10f);
            DrawBranchRows(profile, "설치기 빌드", SpecTexts.InstallerTiers, b == GunnerBranch.Installer,
                b != GunnerBranch.None && b != GunnerBranch.Installer, profile.gunnerTier,
                () => profile.TryLearnGunnerTier(GunnerBranch.Installer));
        }

        /// <summary>
        /// 섬영 — 원본 `SPEC.blade`(project_test.html:933): 집중 빌드(move) / 칼날폭풍 빌드(conv). 2026-10-14 병합 때 추가 —
        /// 그전엔 섬영 1~5층이 다 있는데 게임 안에서 배울 방법이 없었다. SP는 섬영 칸(`PlayerProfile.BladeSpAvailable`)이고,
        /// 이 탭은 섬영을 고른 동안에만 보이니 머리말의 `SpAvailable`(지금 캐릭터 몫)과 같은 값이다.
        /// </summary>
        void DrawBladeSpec(PlayerProfile profile)
        {
            var b = profile.bladeBranch;
            string branchNow = b == BladeBranch.None ? "아직 안 정함" : (b == BladeBranch.Focus ? "집중 빌드" : "칼날폭풍 빌드");
            DrawSpecHeader(profile, branchNow, profile.bladeTier);

            DrawBranchRows(profile, "집중 빌드", SpecTexts.FocusTiers, b == BladeBranch.Focus,
                b != BladeBranch.None && b != BladeBranch.Focus, profile.bladeTier,
                () => profile.TryLearnBladeTier(BladeBranch.Focus));
            GUILayout.Space(10f);
            DrawBranchRows(profile, "칼날폭풍 빌드", SpecTexts.StormTiers, b == BladeBranch.Storm,
                b != BladeBranch.None && b != BladeBranch.Storm, profile.bladeTier,
                () => profile.TryLearnBladeTier(BladeBranch.Storm));
        }



        /// <summary>갈래 하나의 1~5층 줄. 세 캐릭터 모두 층 비용이 1,2,3,4,5다(원본 `SPEC.*.tiers[].cost`).</summary>
        void DrawBranchRows(PlayerProfile profile, string title, string[] tiers, bool thisBranch, bool otherPicked,
            int tier, System.Func<bool> tryLearn)
        {
            GUILayout.Label($"<b>{title}</b>{(otherPicked ? "   (다른 갈래를 골라서 잠김)" : "")}", RichLabel());

            for (int t = 1; t <= tiers.Length; t++)
            {
                bool learned = thisBranch && tier >= t;
                bool isNext = !otherPicked && tier == t - 1;
                int cost = t;

                GUILayout.BeginHorizontal();
                GUI.enabled = isNext && profile.SpAvailable >= cost;
                string mark = learned ? "✔" : (isNext ? "▶" : "·");
                if (GUILayout.Button($"{mark} {t}층   (SP {cost})", GUILayout.Height(26f), GUILayout.Width(180f)))
                {
                    if (tryLearn())
                    {
                        ShowToast($"{title} {t}층 습득");
                        SaveService.Save(); // 원본 `learnSkill` 직후 저장(:7020)
                    }
                }
                GUI.enabled = true;
                GUILayout.Label(tiers[t - 1]);
                GUILayout.EndHorizontal();
            }
        }





        // ────────────────────────── 시간 회귀 ──────────────────────────

        /// <summary>
        /// 원본 시간 회귀 탭(project_test.html:6495~). **이 게임의 유일한 영구 성장 축**이다 —
        /// 입장료와 몹 체력이 지역당 지수로 올라서 한 생의 강화만으로는 반드시 벽에 부딪히고,
        /// 그 벽을 넘는 수단이 시간 회귀뿐이다.
        /// </summary>
        void DrawRegressionTab(PlayerProfile profile)
        {
            int gain = profile.ShardPreview();
            int cleared = profile.ClearedRegionCount();

            GUILayout.Label("회귀하면 이번 생을 처음부터 다시 시작하는 대신 **시간의 파편**를 얻는다. " +
                            "포인트는 뽑기(아이템)에 쓰고, 회귀 횟수 자체가 상위 지역의 벽을 낮춘다.");
            GUILayout.Space(6f);
            GUILayout.Label($"이번 생 정복한 지역   {cleared} / {RegionConfig.Count}");
            GUILayout.Label($"회귀하면 받을 포인트   ◈ {gain}");
            GUILayout.Label($"지금까지 회귀   {profile.regressions}회   ·   보유 포인트   ◈ {profile.shards}");
            GUILayout.Space(4f);
            GUILayout.Label("보상은 **어디까지 뚫었나**로만 정해진다 — 오래 플레이한다고 늘지 않는다.");

            GUILayout.Space(10f);
            GUILayout.Label("<b>초기화</b>: 레벨 · 경험치 · 골드 · 골드 강화 · 전문화(SP) · 지역 진행", RichLabel());
            GUILayout.Label("<b>유지</b>: 시간의 파편 · 회귀 횟수", RichLabel());

            GUILayout.Space(10f);
            if (gain < 1)
            {
                GUILayout.Label("아직 정복한 지역이 없어서 회귀할 수 없다 — 보스를 하나라도 잡아야 한다.");
            }
            else
            {
                // 확인은 **디자인이 있는 전용 창**이 받는다(Figma "시간 회귀창" 31:4,
                // `UI/RegressionWindow`). 예전엔 이 자리에서 버튼 두 개로 물었는데, 되돌릴 수 없는
                // 조작을 탭 본문 한구석에서 처리하는 것보다 화면을 덮는 창이 맞다.
                if (GUILayout.Button($"◈ 시간 회귀 (+{gain})", GUILayout.Height(32f), GUILayout.Width(220f)))
                {
                    if (regression == null) regression = Object.FindFirstObjectByType<RegressionWindow>();
                    if (regression != null) regression.Open();
                    else ShowToast("회귀창을 찾을 수 없다 — 씬을 다시 빌드할 것");
                }
            }

            GUILayout.Space(14f);
            DrawRegressionWallTable(profile);
        }

        /// <summary>
        /// 지역별 권장 회귀와 지금 걸리는 벽. 원본은 스테이지 탭에 "시간 회귀 N회"로 적어두는데,
        /// **얼마나 불리해지는지**는 안 보여준다 — 숫자를 직접 보여주는 편이 판단에 도움이 된다.
        /// </summary>
        void DrawRegressionWallTable(PlayerProfile profile)
        {
            GUILayout.Label("<b>지역별 회귀 장벽</b>  (권장에 모자라면 몹이 단단해지고 내 피해가 줄어든다)", RichLabel());
            for (int r = 3; r <= RegionConfig.Count; r++) // 1·2지역은 권장 0회라 벽이 없다
            {
                int req = RegressionConfig.RequiredRegressions(r);
                int gap = RegressionConfig.Gap(r, profile.regressions);
                string line = $"{r}. {RegionConfig.NameOf(r)}   권장 회귀 {req}회";
                if (gap == 0) line += "   ✔ 벽 없음";
                else line += $"   ⚠ {gap}회 부족 → 몹 체력 ×{RegressionConfig.WallEnemyHp(r, profile.regressions):0.0}" +
                             $" · 내 피해 ×{RegressionConfig.WallPlayerDamage(r, profile.regressions):0.00}";
                GUILayout.Label(line);
            }
        }

        // ────────────────────────── 기원(가챠) ──────────────────────────

        /// <summary>
        /// 원본 가챠 탭(project_test.html:6538~). 시간의 파편로 아이템을 뽑는다 —
        /// **아이템이 이 게임의 유일한 영구 성장 축**이라(원본 `:746`) 시간 회귀 → 뽑기 → 더 깊은 지역이
        /// 하나의 고리를 이룬다.
        /// </summary>
        void DrawGachaTab(PlayerProfile profile)
        {
            var inv = profile.items;
            GUILayout.Label($"보유 시간의 파편   ◈ {profile.shards}     1회 {GachaService.Cost}◈");
            GUILayout.Label($"천장   {inv.pity} / {GachaService.PityAt}" +
                            "   (이만큼 연속으로 영웅+가 안 나오면 다음은 영웅 이상 확정)");
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            GUI.enabled = profile.shards >= GachaService.Cost;
            if (GUILayout.Button("1회 기원", GUILayout.Height(30f), GUILayout.Width(120f))) Pull(profile, 1);
            GUI.enabled = profile.shards >= GachaService.Cost * 10;
            if (GUILayout.Button("10회 기원", GUILayout.Height(30f), GUILayout.Width(120f))) Pull(profile, 10);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (lastPulls.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("<b>이번 결과</b>", RichLabel());
                foreach (var d in lastPulls)
                    GUILayout.Label($"{d.icon} [{ItemDatabase.GradeName(d.grade)}] {d.name}  —  {d.description}");
            }

            GUILayout.Space(12f);
            GUILayout.Label("<b>보유 아이템</b>  (등급이 높을수록 개당 효과가 크지만 중복 상한이 낮다)", RichLabel());
            foreach (var def in ItemDatabase.All)
            {
                int n = inv.Count(def.id);
                if (n <= 0) continue;
                int cap = ItemDatabase.CapOf(def.id);
                string full = n >= cap ? "  ★MAX" : "";
                GUILayout.Label($"{def.icon} [{ItemDatabase.GradeName(def.grade)}] {def.name}  {n}/{cap}{full}" +
                                $"   →  {def.description}");
            }
            if (inv.stacks.Count == 0) GUILayout.Label("아직 없다 — 회귀해서 포인트를 모으고 뽑아 보자.");
        }

        void Pull(PlayerProfile profile, int n)
        {
            lastPulls = GachaService.Pull(profile, n);
            if (lastPulls.Count == 0) { ShowToast("뽑을 수 있는 게 없다"); return; }
            ShowToast($"{lastPulls.Count}개 획득");
            SaveService.Save();
        }

        System.Collections.Generic.List<ItemDef> lastPulls = new System.Collections.Generic.List<ItemDef>();

        // ────────────────────────── 업적 ──────────────────────────

        /// <summary>
        /// 원본 업적 탭(project_test.html:6888). 보상은 없고 **어디까지 왔는지를 보여주는 지표**다 —
        /// 원본 주석 그대로 "현재 캐릭터/시간 회귀/강화 루프에 맞춘 진행 목표".
        /// </summary>
        void DrawAchieveTab(PlayerProfile profile)
        {
            int done = Achievements.Count(profile);
            GUILayout.Label($"달성   {done} / {Achievements.All.Length}");
            GUILayout.Space(4f);
            GUILayout.Label($"누적 처치 {profile.stats.totalKills}   최고 콤보 {profile.stats.maxCombo}   " +
                            $"엘리트 {profile.stats.elites}   성소 {profile.stats.shrines}   보스 {profile.stats.bosses}");
            GUILayout.Label($"누적 골드 {profile.stats.goldEarned}   누적 시간의 파편 {profile.stats.shardsEarned}   " +
                            $"뽑기 {profile.stats.pulls}회");
            GUILayout.Space(8f);

            foreach (var a in Achievements.All)
            {
                bool got = profile.achieved.Contains(a.id);
                GUILayout.Label($"{(got ? "🏆" : "▫")} {a.name}  —  {a.description}");
            }

            GUILayout.Space(10f);
            GUILayout.Label("메카닉·섬영 빌드 5층 업적은 그 스킬트리가 아직 없어서 목록에서 뺐다 — " +
                            "항상 달성 불가인 줄이 섞여 있으면 버그인지 미구현인지 구분이 안 된다.");
        }

        // ────────────────────────── 잡동사니 ──────────────────────────

        void ShowToast(string message)
        {
            toast = message;
            toastLeft = 2f;
        }

        /// <summary>
        /// 예전엔 `richText`를 켠 스타일을 따로 만들었지만, 지금은 <see cref="UiTheme"/>의 기본
        /// 라벨이 이미 켜져 있다. 호출부가 많아 이름만 남겨 뒀다 — 새 코드는 그냥 `GUILayout.Label`을 쓰면 된다.
        /// </summary>
        static GUIStyle RichLabel() => GUI.skin.label;
    }
}
