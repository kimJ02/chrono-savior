using System.Globalization;
using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// HUD 스킬 슬롯 한 칸(Z·X)의 상태 — 원본 `setSlotCooldown(sl, frac, remain)`(project_test.html:6248)의
    /// 두 인자와, X 칸 오른쪽 위 스택 글자(`.stk`)·자원 없음 표시(`.noStack`)(`:6354`~`:6366`).
    ///
    /// 비율과 남은 초를 따로 들고 있는 건 원본이 그렇게 나눠 쓰기 때문이다 — 드루이드 X 칸은 쿨다운이 아니라
    /// 마나 부족분을 덮개로만 보여주고 숫자는 안 띄운다(`setSlotCooldown(ultSlot, lack, 0)`, `:6350`).
    /// </summary>
    public readonly struct SkillSlotState
    {
        /// <summary>어두운 덮개가 아래에서부터 차오르는 비율 0~1 — 원본 `.cd { height: frac% }`.</summary>
        public readonly float Fraction;
        /// <summary>남은 초 — 원본 `.cdnum`에 들어가는 값(<see cref="CooldownText"/>).</summary>
        public readonly float Remaining;
        /// <summary>스택 수 — <see cref="StackMax"/>가 0이면 스택 글자가 없는 칸이다.</summary>
        public readonly int Stacks;
        public readonly int StackMax;
        /// <summary>원본 `.noStack` — 스킬에 쓸 자원이 없다(설치기 부품 0개). 아이콘이 흐려지고 스택 글자가 붉어진다.</summary>
        public readonly bool NoStack;

        public SkillSlotState(float fraction, float remaining, int stacks = 0, int stackMax = 0, bool noStack = false)
        {
            Fraction = Mathf.Clamp01(fraction);
            Remaining = Mathf.Max(0f, remaining);
            Stacks = stacks;
            StackMax = stackMax;
            NoStack = noStack;
        }

        /// <summary>
        /// 쿨다운 칸 — 원본 `clamp(cd / cdMax, 0, 1)`과 `cd`. <paramref name="max"/>는 쿨다운을 **건 순간**의
        /// 최대치다(원본 `p.bowCdMax`·`p.gunnerCdMax`·`p.ultCdMax`가 발사·시전 때 정해지는 것과 같다).
        /// </summary>
        public static SkillSlotState Cooldown(float remaining, float max,
                                              int stacks = 0, int stackMax = 0, bool noStack = false)
        {
            float r = Mathf.Max(0f, remaining);
            return new SkillSlotState(max > 0f ? r / max : 0f, r, stacks, stackMax, noStack);
        }

        public bool HasStack => StackMax > 0;

        /// <summary>
        /// 쿨다운 숫자가 떠 있는가 — 원본 `sl.classList.toggle('cooling', !!txt)`. 이때 아이콘이 흐려진다
        /// (`.slot.cooling .icon { opacity:.34 }`).
        /// </summary>
        public bool Cooling => Remaining > CooldownTextMin;

        /// <summary>원본 `fmtCooldown(t)`(`:6244`)의 결과 — 숫자가 없으면 빈 문자열.</summary>
        public string CooldownText => FormatCooldown(Remaining);

        /// <summary>이 이하로 남으면 숫자를 안 띄운다 — 원본 `if (t <= 0.05) return ''`.</summary>
        const float CooldownTextMin = 0.05f;

        /// <summary>
        /// 원본 `fmtCooldown(t)` 그대로 — 0.05초 이하면 빈 문자열, 1초 이상이면 올림한 정수, 그 사이는
        /// 소수 첫째 자리(`t.toFixed(1)`). 원본의 `t >= 10 ? ceil : t >= 1 ? ceil : …`은 두 갈래가 같다.
        /// </summary>
        public static string FormatCooldown(float t)
        {
            if (t <= CooldownTextMin) return "";
            return t >= 1f
                ? Mathf.CeilToInt(t).ToString(CultureInfo.InvariantCulture)
                : t.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 캐릭터 키트가 전투 HUD(<c>UI.Screens.CombatHud</c>)의 스킬 슬롯·버프 줄에 자기 상태를 알려주는 계약 — 원본 `syncHUD()`의 슬롯 부분
    /// (`:6319`~`:6367`)이 무기별로 직접 읽던 값들(`p.atkCds[무기]`/`…CdMax`, `p.ultCd`/`p.ultCdMax`, 메카닉 스택)을
    /// 키트가 내준다. HUD가 키트 안쪽 타이머를 알 필요가 없어진다.
    ///
    /// <see cref="ICharacterKit"/>에 넣지 않고 따로 둔 이유: 공용 계약을 바꾸면 **이미 있는 키트가 전부 컴파일
    /// 에러**가 난다. 따로 두면 구현하지 않은 키트도 그대로 돌고 슬롯에는 아이콘만 보인다.
    /// 지금은 세 키트(마법사·메카닉·섬영) 모두 구현했다 — 섬영은 2026-11-04에 붙였다.
    /// </summary>
    /// <remarks>2026-11-05 `Characters`에서 `Core`로 옮겼다 — 제공은 `Characters`(키트), 읽기는 `Systems`(전투 화면 창구)·`UI`라 아래층 계약이어야 한다.</remarks>
    public interface ISkillSlotSource
    {
        /// <summary>Z 칸 — 기본공격 쿨다운.</summary>
        SkillSlotState AttackSlot { get; }

        /// <summary>X 칸 — 전문화 스킬 쿨다운(메카닉은 충전 스택·부품도 같이).</summary>
        SkillSlotState SkillSlot { get; }

        /// <summary>
        /// 원본 `#buffs` 줄에 캐릭터별로 덧붙는 글(`:6291`~`:6318` — 차지율·충전 스택·부품 등). 없으면 빈 문자열.
        /// </summary>
        string BuffText { get; }
    }
}
