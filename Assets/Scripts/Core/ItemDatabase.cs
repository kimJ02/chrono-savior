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

    /// <summary>스탯 유물 축(예전 원본 35종). 지금 테스트 유물엔 해당 없음 — 집계 코드 호환용.</summary>
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
    /// 유물 30종 — **2026-11-19 테스트 유물**(GPT와 3차 논의, 사용자 확정: 캐릭터 전용 15 + 공용 15).
    /// 팀원 기획서가 오면 이 목록을 갈아 끼운다. 원본(`project_test.html`) 35종은 쓰지 않는다(사용자 2026-11-19).
    ///
    /// - 보유 = 적용(장착 칸 없음). 같은 유물을 또 얻으면 **랭크**가 오른다 — 상한 일반·희귀 3, 영웅·전설 2.
    /// - 캐릭터 유물은 그 캐릭터일 때만, 갈래 유물은 그 갈래를 골랐을 때만 켜진다(<see cref="Relics.Rank"/>).
    /// - 등급 합계 N12/R10/E5/L3. 뽑기 확률은 Figma 70/27/2/1%.
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
            R("compressedSingularity", "압축 특이점", M, RelicBranch.Gravity, E,
              "중력점 3개 이상을 붕괴시키면 내파가 2/3번 더 터진다(공격력 35/45%).", null,
              V(2, 3), V(0.35f, 0.45f)),                                              // 내파 수, 피해 배수

            // ── 메카닉 ──
            R("spareRounds", "잔탄 포켓", G, RelicBranch.Base, N,
              "Z가 5/4/3번 맞힐 때마다 유도 파편탄 1발(공격력 40/50/60%).", null,
              V(5, 4, 3), V(0.40f, 0.50f, 0.60f)),                                    // 적중 간격, 피해 배수
            R("prismBore", "프리즘 구경", G, RelicBranch.Laser, Ra,
              "레이저가 4/3/3번 맞힐 때마다 옆으로 갈라지는 광선 2/3/3줄(각 공격력 35/35/50%).", null,
              V(4, 3, 3), V(2, 3, 3), V(0.35f, 0.35f, 0.50f)),                        // 적중 간격, 줄 수, 피해 배수
            R("autoTurret", "자율 포좌", G, RelicBranch.Laser, L,
              "레이저 드론이 처치할 때마다 유도탄 3/4발(각 공격력 30/40%).", null,
              V(3, 4), V(0.30f, 0.40f)),                                              // 유도탄 수, 피해 배수
            R("triangleDischarge", "삼각 방전", G, RelicBranch.Installer, N,
              "설치 장 안에서 적이 5/4/3번 맞을 때마다 번개가 2/3/4마리에게 튄다(공격력 35/40/45%).", null,
              V(5, 4, 3), V(2, 3, 4), V(0.35f, 0.40f, 0.45f)),                        // 적중 간격, 대상 수, 피해 배수
            R("crossCurrent", "교차 전류", G, RelicBranch.Installer, E,
              "설치 장 안에서 적이 4/3번 맞을 때마다 설치기 사이를 잇는 교차 광선(공격력 60/80%).", null,
              V(4, 3), V(0.60f, 0.80f)),                                              // 적중 간격, 피해 배수

            // ── 섬영 ──
            R("afterEdge", "잔영 칼끝", B, RelicBranch.Base, N,
              "회전 베기 5/4/3번마다 앞으로 날아가는 초승달 칼날(공격력 35/45/55%).", null,
              V(5, 4, 3), V(0.35f, 0.45f, 0.55f)),                                    // 간격, 피해 배수
            R("focusHeart", "집중의 심장", B, RelicBranch.Focus, Ra,
              "회전 베기 흡혈이 터질 때마다 칼날 파동 1/1/2발(공격력 35/50/35%, 3랭크는 돌아오는 칼날).", null,
              V(1, 1, 2), V(0.35f, 0.50f, 0.35f)),                                    // 발 수, 피해 배수
            R("dashRipper", "질주 찢개", B, RelicBranch.Focus, E,
              "집중이 끝날 때 달린 길이 찢어지며 베기 1/2줄(공격력 80%).", null,
              V(1, 2), V(0.80f, 0.80f)),                                              // 줄 수, 피해 배수
            R("stormSheath", "폭풍 칼집", B, RelicBranch.Storm, Ra,
              "회전 베기로 5/4/3마리 처치할 때마다 충전, 3번 차면 칼날 3/4/5발 부채꼴 발사(공격력 40/50/50%).", null,
              V(5, 4, 3), V(3, 4, 5), V(0.40f, 0.50f, 0.50f)),                        // 처치 간격, 칼날 수, 피해 배수
            R("endlessDance", "무한 검무", B, RelicBranch.Storm, L,
              "칼날폭풍으로 적을 붙잡을 때마다 검령 3/5개 저장, 칼날폭풍이 끝나면 넓은 부채꼴로 발사(공격력 45/55%).", null,
              V(3, 5), V(0.45f, 0.55f)),                                              // 검령 수, 피해 배수

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
            R("censer", "향로", C, RelicBranch.Any, N,
              "성소를 부수면 폭발(공격력 70/85/100%)과 불길 장판(반경 3/4/5m, 1/1.5/2초).", Chain,
              V(0.70f, 0.85f, 1.00f), V(3f, 4f, 5f), V(1f, 1.5f, 2f)),                // 피해 배수, 반경, 지속
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
