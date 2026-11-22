using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>유물 등급 — Figma 이름은 노말·레어·에픽·레전드.</summary>
    public enum ItemGrade { Common, Rare, Epic, Legend }

    /// <summary>
    /// 유물 종류. 2026-11-19 테스트 유물(`C:\dev\유물_테스트기획_초안.md`)은 전부 <c>Special</c>(발동형)이다.
    /// <c>Stat</c>은 예전 원본 35종의 스탯 유물 자리 — 집계 코드(`ItemInventory.Add/Mul`)가 남아 있어 그대로 둔다(지금 해당 유물 없음).
    /// </summary>
    public enum ItemKind { Stat, Special }

    /// <summary>능력치 유물이 올리는 스탯(2026-11-22 공용 능력치 유물 14종이 쓴다 — ItemInventory.Add/Mul이 집계).</summary>
    public enum ItemStat { None, Atk, Hp, Gold, Exp, Ms, AtkSpeed, Crit, Mob, Rate, CritDmg, Dmg, Cd, Dr }

    /// <summary>유물 주인 — 공용이거나 캐릭터 전용(그 캐릭터일 때만 켜진다).</summary>
    public enum RelicOwner { Common, Mage, Gunner, Blade }

    /// <summary>
    /// 유물이 켜지는 갈래. <c>Any</c> = 공용, <c>Base</c> = 그 캐릭터 기본공격(갈래 무관),
    /// 나머지는 그 갈래를 골랐을 때만 켜진다.
    /// </summary>
    public enum RelicBranch { Any, Base, Explosion, Gravity, Laser, Installer, Focus, Storm }

    /// <summary>유물 정의 한 줄.</summary>
    public class ItemDef
    {
        public string id;
        public string name;
        public string icon;
        public ItemGrade grade;
        public ItemKind kind = ItemKind.Special;
        public ItemStat stat = ItemStat.None;
        /// <summary>예전 원본식 "중첩 1개당 세기"(`ItemInventory.Pow`). 테스트 유물은 <see cref="values"/>를 쓴다.</summary>
        public float per;
        public bool multiplicative;
        public string description;

        public RelicOwner owner = RelicOwner.Common;
        public RelicBranch branch = RelicBranch.Any;
        /// <summary>세트 태그(공용 "연쇄"·"시간", 저주 표시 "저주"). 캐릭터 세트는 <see cref="owner"/>로 센다.</summary>
        public string[] tags = System.Array.Empty<string>();
        /// <summary>랭크별 수치 — <c>values[k][rank-1]</c>. 효과마다 k번째 값의 뜻이 다르다(<see cref="Relics"/>의 상수 참고).</summary>
        public float[][] values = System.Array.Empty<float[]>();
    }

    /// <summary>
    /// 유물 32종 — **2026-11-19 테스트 유물**(GPT와 3차 논의, 사용자 확정: 캐릭터 전용 15 + 공용 15, 같은 날 스폰 유물 2종 추가 → 공용 17).
    /// 팀원 기획서가 오면 이 목록을 갈아 끼운다. 원본(`project_test.html`) 35종은 쓰지 않는다(사용자 2026-11-19).
    ///
    /// - 보유 = 적용(장착 칸 없음). 같은 유물을 또 얻으면 **랭크**가 오른다 — 상한 일반·희귀 3, 영웅·전설 2.
    /// - 캐릭터 유물은 그 캐릭터일 때만, 갈래 유물은 그 갈래를 골랐을 때만 켜진다(<see cref="Relics.Rank"/>).
    /// - 등급 합계 N13/R12/E5/L3(33종 — 마법사 6종: 역행의 시계핵 추가). 뽑기 확률은 Figma 70/27/2/1%.
    /// </summary>
    public static class ItemDatabase
    {
        /// <summary>뽑기 등급 가중치 — Figma "시공 유물 소환"(32:17) 노말 70·레어 27·에픽 2·레전드 1%.</summary>
        public static int Weight(ItemGrade g) => g switch
        {
            ItemGrade.Common => 70,
            ItemGrade.Rare => 27,
            ItemGrade.Epic => 2,
            ItemGrade.Legend => 1,
            _ => 0,
        };

        /// <summary>랭크 상한 — 일반·희귀 3, 영웅·전설 2. 상한에 닿으면 각성.</summary>
        public static int Cap(ItemGrade g) => g switch
        {
            ItemGrade.Common => 3,
            ItemGrade.Rare => 3,
            ItemGrade.Epic => 2,
            ItemGrade.Legend => 2,
            _ => 0,
        };

        public static string GradeName(ItemGrade g) => g switch
        {
            ItemGrade.Common => "노말",
            ItemGrade.Rare => "레어",
            ItemGrade.Epic => "에픽",
            ItemGrade.Legend => "레전드",
            _ => "?",
        };

        public static Color GradeColor(ItemGrade g) => g switch
        {
            ItemGrade.Common => new Color(0.62f, 0.71f, 0.63f),
            ItemGrade.Rare => new Color(0.44f, 0.78f, 1f),
            ItemGrade.Epic => new Color(0.79f, 0.54f, 1f),
            ItemGrade.Legend => new Color(1f, 0.77f, 0.30f),
            _ => Color.white,
        };

        public static string OwnerName(RelicOwner o) => o switch
        {
            RelicOwner.Mage => "마법사",
            RelicOwner.Gunner => "메카닉",
            RelicOwner.Blade => "섬영",
            _ => "공용",
        };

        public static string BranchName(RelicBranch b) => b switch
        {
            RelicBranch.Base => "기본공격",
            RelicBranch.Explosion => "폭발",
            RelicBranch.Gravity => "중력",
            RelicBranch.Laser => "레이저",
            RelicBranch.Installer => "설치",
            RelicBranch.Focus => "집중",
            RelicBranch.Storm => "폭풍",
            _ => "",
        };

        static float[] V(params float[] a) => a;

        /// <summary>능력치 유물 — 공용, 랭크마다 <paramref name="per"/>씩 합연산(values = 랭크별 누적값, 화면·테스트용).</summary>
        static ItemDef S(string id, string nm, ItemGrade g, ItemStat stat, float per, string desc)
        {
            int cap = Cap(g);
            var vals = new float[cap];
            for (int i = 0; i < cap; i++) vals[i] = per * (i + 1);
            return new ItemDef { id = id, name = nm, owner = RelicOwner.Common, branch = RelicBranch.Any, grade = g, kind = ItemKind.Stat,
                                 stat = stat, per = per, multiplicative = false, description = desc, values = new[] { vals } };
        }

        static ItemDef R(string id, string nm, RelicOwner owner, RelicBranch branch, ItemGrade g, string desc, string[] tags, params float[][] values) =>
            new ItemDef { id = id, name = nm, owner = owner, branch = branch, grade = g, description = desc, tags = tags ?? System.Array.Empty<string>(), values = values };

        static readonly string[] Chain = { "연쇄" }, Time = { "시간" }, TimeCurse = { "시간", "저주" };
        const RelicOwner M = RelicOwner.Mage, G = RelicOwner.Gunner, B = RelicOwner.Blade, C = RelicOwner.Common;
        const ItemGrade N = ItemGrade.Common, Ra = ItemGrade.Rare, E = ItemGrade.Epic, L = ItemGrade.Legend;

        /// <summary>정의 순서 = 유물 화면 칸 순서(캐릭터 전용 15 → 공용 15).</summary>
        public static readonly ItemDef[] All =
        {
            // ── 마법사 ── values: 아래 주석 = values[0], [1], [2] …
            R("mageEcho", "잔광 탄환", M, RelicBranch.Base, Ra,
              "Z를 4/3/3번째 쏠 때마다 옆으로 갈라지는 작은 탄 2/2/3발(피해 30/40/45%).", null,
              V(4, 3, 3), V(2, 2, 3), V(0.30f, 0.40f, 0.45f)),                       // 간격, 발 수, 피해 배수
            R("emberLedger", "불씨 장부", M, RelicBranch.Explosion, N,
              "불타는 적이 쓰러지면 시체가 폭발(반경 1.2/1.5/1.8m, 공격력 40/50/60%).", null,
              V(1.2f, 1.5f, 1.8f), V(0.40f, 0.50f, 0.60f)),                           // 반경, 피해 배수
            R("flameCrown", "화염왕관", M, RelicBranch.Explosion, L,
              "완충 Z가 조준 지점에 떨어지는 화염 파편 3/5발(각 공격력 50%)로 바뀐다.", null,
              V(3, 5), V(0.50f, 0.50f)),                                              // 파편 수, 피해 배수
            R("gravitySpring", "중력 태엽", M, RelicBranch.Gravity, Ra,
              "중력 붕괴 뒤 작은 중력장이 0.8/1.2/1.6초 남아 적을 끌어당긴 뒤 폭발(공격력 40/50/60%).", null,
              V(0.8f, 1.2f, 1.6f), V(0.40f, 0.50f, 0.60f)),                           // 지속, 피해 배수
            R("rewindCore", "역행의 시계핵", M, RelicBranch.Gravity, Ra,
              "중력 붕괴(X) 쿨타임 -15/30/45%.", null,
              V(0.15f, 0.30f, 0.45f)),                                                // 쿨타임 감소(사용자 2026-11-19 추가)
            R("compressedSingularity", "압축 특이점", M, RelicBranch.Gravity, E,
              "중력점 3개 이상을 붕괴시키면 내파가 2/3번 더 터진다(공격력 35/45%).", null,
              V(2, 3), V(0.35f, 0.45f)),                                              // 내파 수, 피해 배수

            // ── 메카닉 ── 2026-11-22 재설계(사용자 "레이저로 쓸어버려야, 설치기가 사기가 돼야" — GPT와 정함): 시그니처를 키운다, Z에 피해를 더하지 않는다
            R("spareRounds", "잔탄 포켓", G, RelicBranch.Base, N,
              "Z가 5/4/3번 맞힐 때마다 레이저는 드론 충전 +1/2/3, 설치는 부품 적중 +2/3/5(10이면 부품 1개).", null,
              V(5, 4, 3), V(1, 2, 3), V(2, 3, 5)),                                   // 적중 간격, 드론 충전, 부품 적중
            R("prismBore", "프리즘 구경", G, RelicBranch.Laser, Ra,
              "레이저 광선 사거리 +0.45/0.8/1.2m, 굵기 +0.08/0.14/0.22m, 틱 피해 +5/8/12%p.", null,
              V(0.45f, 0.80f, 1.20f), V(0.08f, 0.14f, 0.22f), V(0.05f, 0.08f, 0.12f)), // 사거리, 굵기, 틱 피해 배수
            R("autoTurret", "자율 포좌", G, RelicBranch.Laser, L,
              "레이저 광선으로 3/2마리 처치할 때마다 가까운 적 3/5마리에게 광선이 튄다(공격력 65/100%).", null,
              V(3, 2), V(3, 5), V(0.65f, 1.00f)),                                     // 처치 간격, 대상 수, 피해 배수
            R("triangleDischarge", "삼각 방전", G, RelicBranch.Installer, N,
              "설치기를 1/2/3개 더 놓을 수 있다(장판 꼭짓점도 늘어난다).", null,
              V(1, 2, 3)),                                                            // 설치 한도 증가
            R("crossCurrent", "교차 전류", G, RelicBranch.Installer, E,
              "설치기 연결 거리 +25/50%, 연결선 굵기 +0.10/0.18m, 장판 피해 +25/45%p, 장판 틱 간격 -15/30%.", null,
              V(0.25f, 0.50f), V(0.10f, 0.18f), V(0.25f, 0.45f), V(0.15f, 0.30f)),   // 연결 거리, 선 굵기, 장판 피해, 틱 간격 감소
            R("siegeNetwork", "공성 연결망", G, RelicBranch.Installer, L,
              "장판이 적을 맞힐 때마다 설치기 일제 사격으로 그 적들에게 추가 피해(공격력 60/90%).", null,
              V(0.60f, 0.90f)),                                                       // 추가 피해 배수(사용자 2026-11-22 — 6/6/6)

            // ── 섬영 ── 2026-11-22 재설계(사용자 "직업의 재미를 살려라, 섬영은 이펙트도 구리다" — GPT와 정함): 집중·칼날폭풍을 키운다, Z에 피해를 더하지 않는다
            R("afterEdge", "잔영 칼끝", B, RelicBranch.Base, N,
              "X(집중·칼날폭풍) 쿨타임 -10/18/25%.", null,
              V(0.10f, 0.18f, 0.25f)),                                                // 쿨타임 감소
            R("focusHeart", "집중의 심장", B, RelicBranch.Focus, Ra,
              "집중 궤적 피해 ×1.15/1.25/1.4, 반경 +0.04/0.08/0.12m. 집중 중 흡혈 회전베기마다 초승달 1/2/3발(공격력 35/50/70%).", null,
              V(1.15f, 1.25f, 1.40f), V(0.04f, 0.08f, 0.12f), V(1, 2, 3), V(0.35f, 0.50f, 0.70f)), // 궤적 피해, 반경, 초승달 수, 피해 배수
            R("dashRipper", "질주 찢개", B, RelicBranch.Focus, E,
              "집중이 끝나면 달린 궤적을 바로 찢는다 — 찢기 피해 ×1.5/2, 반경 ×1.25/1.5.", null,
              V(1.50f, 2.00f), V(1.25f, 1.50f)),                                      // 찢기 피해, 찢기 반경
            R("echoPursuit", "잔영 추격", B, RelicBranch.Focus, L,
              "궤적·찢기로 적을 쓰러뜨리면 사방으로 초승달 2/4발(공격력 65/100%).", null,
              V(2, 4), V(0.65f, 1.00f)),                                              // 초승달 수, 피해 배수(사용자 2026-11-22 — 6/6/6)
            R("stormSheath", "폭풍 칼집", B, RelicBranch.Storm, Ra,
              "칼날폭풍(유지 베기·착지·낙하)으로 4/3/2마리 처치할 때마다 초승달 3/5/7발 부채꼴(공격력 65/85/110%).", null,
              V(4, 3, 2), V(3, 5, 7), V(0.65f, 0.85f, 1.10f)),                        // 처치 간격, 초승달 수, 피해 배수
            R("endlessDance", "무한 검무", B, RelicBranch.Storm, L,
              "칼날폭풍으로 적을 붙잡을 때마다 검령 3/5개 저장, 칼날폭풍이 끝나면 넓은 부채꼴로 발사(공격력 90/125%).", null,
              V(3, 5), V(0.90f, 1.25f)),                                              // 검령 수, 피해 배수

            // ── 공용 ──
            R("lantern", "등불", C, RelicBranch.Any, N,
              "처치 8/7/6번마다 가장 가까운 무리에 빛 파동(공격력 70/85/100%).", Chain,
              V(8, 7, 6), V(0.70f, 0.85f, 1.00f)),                                    // 처치 간격, 피해 배수
            R("letter", "편지", C, RelicBranch.Any, N,
              "연쇄 처치 때마다 가까운 적 1/2/3마리에 폭발 편지, 1초 뒤 폭발(공격력 40/45/50%).", Chain,
              V(1, 2, 3), V(0.40f, 0.45f, 0.50f)),                                    // 편지 수, 피해 배수
            R("chainHourglass", "연쇄 모래시계", C, RelicBranch.Any, Ra,
              "연쇄 처치 4/3/2번마다 바닥 충격파(공격력 90/110/130%).", Chain,
              V(4, 3, 2), V(0.90f, 1.10f, 1.30f)),                                    // 연쇄 간격, 피해 배수
            R("comboTerminal", "콤보 종착역", C, RelicBranch.Any, E,
              "콤보 50/35마다 다음 공격이 화면을 가르는 시간 베기로(공격력 100/140%).", Chain,
              V(50, 35), V(1.00f, 1.40f)),                                            // 콤보 간격, 피해 배수
            R("eliteBell", "정예 사냥종", C, RelicBranch.Any, N,
              "엘리트 처치 시 충격파(반경 2/3/4m), 살아남은 적은 취약 0.8/1.2/1.6초.", Chain,
              V(2f, 3f, 4f), V(0.8f, 1.2f, 1.6f), V(0.8f, 0.8f, 0.8f)),               // 반경, 취약 시간, 피해 배수
            R("moonMeteor", "월하의 별똥", C, RelicBranch.Any, N,
              "처치 12/10/8번마다 가장 밀집한 곳에 운석(공격력 100/120/140%).", Chain,
              V(12, 10, 8), V(1.00f, 1.20f, 1.40f)),                                  // 처치 간격, 피해 배수
            R("paperBirds", "종이새 떼", C, RelicBranch.Any, Ra,
              "처치 10/8/6번마다 종이새 떼가 적을 한데 몰고 폭발(공격력 40/55/70%).", Chain,
              V(10, 8, 6), V(0.40f, 0.55f, 0.70f)),                                   // 처치 간격, 피해 배수
            // 향로(성소 파괴 시 폭발)는 2026-11-22 성소를 끄면서 발동할 수 없게 돼 같은 id로 공격력 유물로 바꿨다(보유 기록 유지, GPT와 정함)
            S("censer", "멸망 전의 잔불", N, ItemStat.Atk, 1.5f,
              "공격력 +1.5/3/4.5."),
            R("timelineReboot", "시간선 재기동", C, RelicBranch.Any, E,
              "쓰러지면 사냥당 1번, 시간이 되감기며 체력 60/75% 회복 + 화면 전체 폭발(공격력 150/200%).", Time,
              V(0.60f, 0.75f), V(1.50f, 2.00f)),                                      // 회복 비율, 피해 배수
            R("doomBell", "종언의 종", C, RelicBranch.Any, Ra,
              "처치 40/30/20번마다 화면 전체 폭발(공격력 180/220/260%). 대신 적 공격력 +10/15/20%.", TimeCurse,
              V(40, 30, 20), V(1.80f, 2.20f, 2.60f), V(0.10f, 0.15f, 0.20f)),         // 처치 간격, 피해 배수, 적 공격력
            R("sevenSands", "칠일의 모래", C, RelicBranch.Any, N,
              "스테이지에 들어가면 바닥을 쓸어가는 파동(공격력 80/100/120%), 3랭크는 적을 1초 늦춘다.", Time,
              V(0.80f, 1.00f, 1.20f), V(0f, 0f, 1f)),                                 // 피해 배수, 둔화 시간
            R("seventhCrown", "일곱째 날의 왕관", C, RelicBranch.Any, Ra,
              "회귀 전에 쓴 날짜가 다음 회차 봉인으로(최대 3/5/7개), 연쇄 처치 때 하나씩 써서 화면 베기(공격력 100/130/160%).", Time,
              V(3, 5, 7), V(1.00f, 1.30f, 1.60f)),                                    // 봉인 상한, 피해 배수
            R("glassOath", "유리 서약", C, RelicBranch.Any, Ra,
              "처치 시 4초간 공격력 +25/40/60%, 받는 피해도 +15/25/40%. 맞으면 유리 파동(공격력 100/125/150%) 후 끝.", TimeCurse,
              V(0.25f, 0.40f, 0.60f), V(0.15f, 0.25f, 0.40f), V(1.00f, 1.25f, 1.50f)), // 공격력, 받는 피해, 파동 배수
            R("regressionPostcard", "회귀 엽서", C, RelicBranch.Any, N,
              "회귀한 뒤 각 스테이지 첫 엘리트를 때리면 0.2초 뒤 메아리 공격(공격력 30/45/60%).", Time,
              V(0.30f, 0.45f, 0.60f)),                                                // 피해 배수
            R("thresholdClock", "문턱의 시계", C, RelicBranch.Any, N,
              "스테이지에 들어가면 첫 무리가 0.6/0.9/1.2초 멈추고 파동(공격력 40/50/60%).", Time,
              V(0.6f, 0.9f, 1.2f), V(0.40f, 0.50f, 0.60f)),                           // 정지 시간, 피해 배수

            // ── 공용: 몬스터 스폰(2026-11-19 사용자 추가 — "몬스터 최대 스폰량·스폰 빈도를 늘리는 유물이 있어야 한다") ──
            // 둘 다 각성(3랭크)이면 필드 상한 2배 + 웨이브 2배 빨라짐 = 쓸어버릴 몹이 2배.
            R("spawnCap", "요기 응집석", C, RelicBranch.Any, N,
              "필드 최대 몬스터 수와 한 번에 나오는 수 +30/60/100%.", null,
              V(0.30f, 0.60f, 1.00f)),                                                // 상한·웨이브 크기 증가
            R("spawnRate", "황천의 문", C, RelicBranch.Any, Ra,
              "몬스터가 나오는 빈도 +30/60/100%.", null,
              V(0.30f, 0.60f, 1.00f)),                                                // 웨이브 빈도 증가

            // ── 공용: 능력치(2026-11-22 사용자 "공용 유물을 늘려라, 일반 능력치를 올리는 유물도" — GPT와 정함, 공용 30종) ──
            // 랭크 = 보유 수, 효과 = 랭크 × 1랭크 값(합연산). PlayerStatCalculator가 items.Add로 이미 읽는다 → 전투력에도 들어간다.
            S("ruinHeartbeat", "폐허의 맥박", N, ItemStat.Hp, 10f,
              "최대 체력 +10/20/30."),
            S("stoppedMinute", "멎은 초침", N, ItemStat.Ms, 0.015f,
              "이동 속도 +1.5/3/4.5%."),
            S("lastMarchCog", "마지막 행군의 태엽", N, ItemStat.AtkSpeed, 0.015f,
              "공격 속도 +1.5/3/4.5%."),
            S("seventhNightGlimmer", "일곱째 밤의 잔광", N, ItemStat.Crit, 0.005f,
              "치명타 확률 +0.5/1/1.5%p."),
            S("moonlessEdge", "달 없는 칼날", N, ItemStat.CritDmg, 0.06f,
              "치명타 피해 배율 +0.06/0.12/0.18."),
            S("crackedHourglassSeal", "금 간 시계의 봉인", N, ItemStat.Cd, 0.02f,
              "스킬 쿨타임 -2/4/6%."),
            S("ashWall", "잿빛 방벽", N, ItemStat.Dr, 0.01f,
              "받는 피해 -1/2/3%."),
            S("sevenDayWhetstone", "칠일의 숫돌", Ra, ItemStat.Atk, 3f,
              "공격력 +3/6/9."),
            S("lastWardenHeart", "마지막 수호자의 심장", Ra, ItemStat.Hp, 18f,
              "최대 체력 +18/36/54."),
            S("extinctionLens", "멸망을 보는 렌즈", Ra, ItemStat.Crit, 0.01f,
              "치명타 확률 +1/2/3%p."),
            S("crackedAegis", "갈라진 아이기스", Ra, ItemStat.Dr, 0.015f,
              "받는 피해 -1.5/3/4.5%."),
            S("zeroPointGear", "영점의 기어", E, ItemStat.AtkSpeed, 0.03f,
              "공격 속도 +3/6%."),
            S("demonCastleClockwork", "마왕성의 시계태엽", L, ItemStat.Cd, 0.05f,
              "스킬 쿨타임 -5/10%."),
        };

        static Dictionary<string, ItemDef> byId;

        public static ItemDef Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, ItemDef>(All.Length);
                foreach (var d in All) byId[d.id] = d;
            }
            return id != null && byId.TryGetValue(id, out var def) ? def : null;
        }

        public static int CapOf(string id)
        {
            var d = Get(id);
            return d != null ? Cap(d.grade) : 0;
        }
    }
}
