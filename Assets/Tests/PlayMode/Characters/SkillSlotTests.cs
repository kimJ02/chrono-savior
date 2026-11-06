using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// HUD 스킬 슬롯이 읽는 값(<see cref="ISkillSlotSource"/>) — 2026-09-29 사용자 발견: "레이저 찍고 X를 쓰면 쿨타임이
/// 안 보인다". 원본 `setSlotCooldown(sl, frac, remain)`(project_test.html:6248)·`fmtCooldown`(:6244)·syncHUD 슬롯
/// 부분(:6319~:6366)대로 키트가 비율·남은 초·스택을 내주는지 본다. 그리는 쪽(`UI.GameHud`)은 IMGUI라 배치
/// 테스트로 못 돌리므로, HUD가 그대로 옮겨 그리는 이 값들을 검사한다.
/// X 키 입력은 시뮬레이트할 수 없어 다른 키트 테스트와 같은 관례로 private `TryUseSkill`을 리플렉션으로 부른다.
/// </summary>
public class SkillSlotTests
{
    static readonly string[] SpawnedNames =
    {
        "TestGunner", "TestMage", "TestPlayer", "GunnerBullet", "GunnerDrone", "GunnerTurret", "GunnerBeam",
        "GunnerFieldLinks", "TestBlade",
    };

    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            foreach (var n in SpawnedNames)
                if (go.name == n) { Object.DestroyImmediate(go); break; }
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        ProfileService.Reset();
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunTransient.Reset();
    }

    static GunnerAttack NewGunner(GunnerBranch branch, int tier)
    {
        ProfileService.Current.character = CharacterId.Gunner;
        ProfileService.Current.gunnerBranch = branch;
        ProfileService.Current.gunnerTier = tier;

        var go = new GameObject("TestGunner");
        go.transform.position = new Vector3(5f, 0.5f, 0f);
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterMover2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        var g = go.AddComponent<GunnerAttack>().Equipped();
        g.ResetForRun(); // 사냥 시작처럼 — 설치기는 부품 1개를 쥐고 시작한다
        return g;
    }

    static void Call(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, method + "()가 없다");
        m.Invoke(obj, args);
    }

    // ───────────────────────── 숫자·비율 규칙 ─────────────────────────

    [Test]
    public void FormatCooldown_FollowsOriginalFmtCooldown()
    {
        // 원본: t <= 0.05면 '', 1 이상이면 올림한 정수, 그 사이는 t.toFixed(1)
        Assert.AreEqual("", SkillSlotState.FormatCooldown(0f));
        Assert.AreEqual("", SkillSlotState.FormatCooldown(0.05f), "0.05초 이하는 숫자가 안 뜬다");
        Assert.AreEqual("0.1", SkillSlotState.FormatCooldown(0.06f));
        Assert.AreEqual("0.3", SkillSlotState.FormatCooldown(0.34f));
        Assert.AreEqual("1.0", SkillSlotState.FormatCooldown(0.99f), "1 미만은 반올림한 소수 한 자리(toFixed)");
        Assert.AreEqual("1", SkillSlotState.FormatCooldown(1f));
        Assert.AreEqual("2", SkillSlotState.FormatCooldown(1.01f), "1 이상은 올림");
        Assert.AreEqual("11", SkillSlotState.FormatCooldown(10.2f));
    }

    [Test]
    public void CooldownState_FractionIsRemainingOverMax_Clamped()
    {
        var half = SkillSlotState.Cooldown(5f, 10f);
        Assert.AreEqual(0.5f, half.Fraction, 1e-5f);
        Assert.AreEqual(5f, half.Remaining, 1e-5f);
        Assert.IsTrue(half.Cooling);

        var done = SkillSlotState.Cooldown(-0.3f, 10f); // 키트 타이머는 0 아래로 계속 내려간다
        Assert.AreEqual(0f, done.Fraction);
        Assert.AreEqual(0f, done.Remaining);
        Assert.IsFalse(done.Cooling);
        Assert.AreEqual("", done.CooldownText);

        Assert.AreEqual(1f, SkillSlotState.Cooldown(12f, 10f).Fraction, "도중에 최대치가 줄어도 덮개는 칸을 넘지 않는다");
        Assert.AreEqual(0f, SkillSlotState.Cooldown(1f, 0f).Fraction, "최대치가 없으면 덮개도 없다");

        SkillSlotState empty = default; // 점프 칸 · 슬롯을 안 내주는 키트
        Assert.IsFalse(empty.Cooling);
        Assert.IsFalse(empty.HasStack);
        Assert.AreEqual(0f, empty.Fraction);
    }

    // ───────────────────────── 메카닉 ─────────────────────────

    /// <summary>사용자가 발견한 바로 그 상황 — 레이저 빌드에서 X(드론)를 쓰면 X 칸에 쿨다운이 떠서 줄어들어야 한다.</summary>
    [UnityTest]
    public IEnumerator LaserDroneUlt_ShowsCooldownOnXSlot_AndCountsDown()
    {
        var g = NewGunner(GunnerBranch.Laser, 1);
        yield return null;
        g.AddStack(4);

        var before = g.SkillSlot;
        Assert.IsFalse(before.Cooling, "쓰기 전인데 쿨다운이 떠 있다");
        Assert.IsTrue(before.HasStack, "레이저 빌드 X 칸엔 충전 스택이 붙는다(원본 .stk)");
        Assert.AreEqual(4, before.Stacks);
        Assert.AreEqual(9, before.StackMax, "1층 충전 최대치 = 6+1×3");
        Assert.AreEqual("충전 4/9", g.BuffText, "원본 버프 줄 `충전 n/최대`");

        Call(g, "TryUseSkill");
        float t0 = Time.time;
        float max = 30f * 0.34f; // 원본 드론 쿨다운 CONFIG.ult.cd × 0.34

        var cast = g.SkillSlot;
        Assert.IsTrue(cast.Cooling, "X(드론)를 썼는데 X 칸에 쿨다운이 안 뜬다");
        Assert.AreEqual(1f, cast.Fraction, 1e-4f, "막 쓴 순간 덮개가 칸을 가득 채워야 한다");
        Assert.AreEqual(max, cast.Remaining, 0.01f);
        Assert.AreEqual("11", cast.CooldownText, "10.2초는 올림해서 11");
        Assert.AreEqual(0, cast.Stacks, "드론 소환은 충전 스택을 전부 쓴다");

        yield return new WaitForSeconds(0.5f);
        float expected = max - (Time.time - t0);
        var later = g.SkillSlot;
        Assert.AreEqual(expected, later.Remaining, 0.02f, "쿨다운 숫자가 시간에 맞춰 안 줄어든다");
        Assert.AreEqual(expected / max, later.Fraction, 0.005f,
            "덮개 비율의 분모가 쓴 순간의 쿨다운(원본 p.ultCdMax)이 아니다");
    }

    [UnityTest]
    public IEnumerator InstallerUlt_ShowsParts_AndNoStackWhenPartsRunOut()
    {
        var g = NewGunner(GunnerBranch.Installer, 1);
        yield return null;

        var ready = g.SkillSlot;
        Assert.IsTrue(ready.HasStack);
        Assert.AreEqual(1, ready.Stacks, "설치기 빌드는 부품 1개를 쥐고 시작한다");
        Assert.AreEqual(3, ready.StackMax, "부품 최대치 = 설치기 한도(1층 3)");
        Assert.IsFalse(ready.NoStack);

        Call(g, "TryUseSkill");

        var placed = g.SkillSlot;
        Assert.AreEqual(1, GunnerTurret.Active.Count, "설치기가 안 놓였다");
        Assert.AreEqual(0, placed.Stacks);
        Assert.IsTrue(placed.NoStack, "부품이 0개면 원본처럼 noStack(아이콘 흐리게·스택 글자 빨갛게)");
        Assert.IsTrue(placed.Cooling);
        Assert.AreEqual(1f, placed.Fraction, 1e-4f);
        Assert.AreEqual(0.55f - 0.03f, placed.Remaining, 0.01f, "설치 쿨다운 = 0.55 − 층×0.03");
        Assert.AreEqual("0.5", placed.CooldownText);
        Assert.AreEqual("부품 0/3 · 적중 0/10 · 설치기 1/3 · 링크 370", g.BuffText,
            "원본 버프 줄 — 링크는 원본이 보여주는 px 숫자(300+1×70)");
    }

    [UnityTest]
    public IEnumerator ZeroTierGunner_XSlotHasNoStackAndNoCooldown()
    {
        var g = NewGunner(GunnerBranch.None, 0);
        yield return null;
        Call(g, "TryUseSkill"); // 빌드가 없으면 X는 아무것도 안 한다

        var s = g.SkillSlot;
        Assert.IsFalse(s.Cooling);
        Assert.IsFalse(s.HasStack, "갈래가 없으면 원본도 .stk를 숨긴다");
        Assert.AreEqual("", g.BuffText);
    }

    // ───────────────────────── 마법사 ─────────────────────────

    [UnityTest]
    public IEnumerator MageUlt_ShowsCooldownOnXSlot()
    {
        var profile = ProfileService.Current;
        profile.mageBranch = MageBranch.Explosion;
        profile.mageTier = 1;
        var mage = new GameObject("TestMage").AddComponent<MageAttack>().Equipped(); // 이동기가 없으면 순간이동만 건너뛴다
        yield return null;

        Assert.IsFalse(mage.SkillSlot.Cooling);
        Call(mage, "TryUseSkill", profile, MageBranch.Explosion, 1);

        var s = mage.SkillSlot;
        Assert.IsTrue(s.Cooling, "마법사 X를 썼는데 X 칸에 쿨다운이 안 뜬다");
        Assert.AreEqual(1f, s.Fraction, 1e-4f);
        Assert.AreEqual(30f * 0.34f, s.Remaining, 0.01f, "폭발 빌드 X 쿨다운 = 30×0.34(원본 :2177)");
        Assert.IsFalse(s.HasStack, "마법사 X 칸엔 스택 글자가 없다");
        Assert.AreEqual("폭발탄 · 불길 이동", mage.BuffText, "원본 버프 줄 — 폭발 빌드 표시(:6292)");
    }

    // ───────────────────────── 섬영 ─────────────────────────

    static void SetField(object obj, string name, object value)
    {
        var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, name + " 필드가 없다");
        f.SetValue(obj, value);
    }

    /// <summary>
    /// 섬영 Z·X 칸과 버프 줄 — 원본 syncHUD(:6293~:6301, :6322, :6341~:6345). 프레임을 넘기지 않고 같은 프레임에 읽는다
    /// (Update가 타이머를 깎고 초과 이속을 다시 계산하므로).
    /// </summary>
    [Test]
    public void Blade_SlotsAndBuffLine_FollowOriginal()
    {
        var go = new GameObject("TestBlade");
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        var blade = go.AddComponent<BladeCombat>();
        Assert.IsTrue(blade is ISkillSlotSource, "섬영 키트가 슬롯 상태를 안 내준다 — HUD 칸·버프 줄이 빈다");

        // Z — 원본 분모는 기본 쿨다운 CONFIG.blade.spin.cd(0.13) 고정(:6322).
        SetField(blade, "cdTimer", 0.065f);
        Assert.AreEqual(0.5f, blade.AttackSlot.Fraction, 1e-4f);
        Assert.AreEqual(0.065f, blade.AttackSlot.Remaining, 1e-4f);

        // X — 갈래가 없거나 집중이면 focusCd/11, 칼날폭풍이면 stormCd/8(:6341~:6345).
        SetField(blade, "focusCd", 5.5f);
        SetField(blade, "stormCd", 2f);
        Assert.AreEqual(0.5f, blade.SkillSlot.Fraction, 1e-4f, "갈래 없음 — 원본처럼 집중 쿨다운을 읽는다");
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        Assert.AreEqual(5.5f, blade.SkillSlot.Remaining, 1e-4f);
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        Assert.AreEqual(0.25f, blade.SkillSlot.Fraction, 1e-4f, "칼날폭풍 갈래는 stormCd / S.cd(8)");
        Assert.IsFalse(blade.SkillSlot.HasStack, "섬영 X 칸엔 스택 글자가 없다");

        // 버프 줄 — 멈춰 있으면 속도 0%·피해 ×1.00만.
        Assert.AreEqual("속도 0% · 피해 ×1.00", blade.BuffText);

        // 칼날폭풍 중 최고 속도 + 초과 이속 + 집중·궤적 — 피해 = 1 + 1×(1.4 + 0.2×1.4) = 2.68(bladeDmgMult :2439).
        SetField(blade, "storming", true);
        SetField(blade, "stormSpeedRatio", 1f);
        SetField(blade, "msSurplus", 0.2f);
        SetField(blade, "focusT", 1.5f);
        SetField(blade, "trailT", 2f);
        Assert.AreEqual("속도 100% · 피해 ×2.68 (초과 이속 +20% → 화력) 【최고 속도】  집중 1.5초  궤적 2.0초  칼날폭풍",
                        blade.BuffText);
    }

    // ───────────────────────── HUD가 읽는 경로 ─────────────────────────

    /// <summary>HUD는 <see cref="PlayerRig.CurrentKit"/>에서 슬롯을 읽는다 — 캐릭터를 바꾸면 그 키트로 따라가야 한다.</summary>
    [UnityTest]
    public IEnumerator CurrentKit_IsTheSelectedKit_AndReportsSlots()
    {
        var go = new GameObject("TestPlayer");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = EntitySizeConfig.PlayerRadius;
        var rb = go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterMover2D>();
        rb.gravityScale = 0f;
        var mage = go.AddComponent<MageAttack>().Equipped();
        var gunner = go.AddComponent<GunnerAttack>().Equipped();
        var rig = go.AddComponent<PlayerRig>(); // 키트를 전부 붙인 뒤에(씬 `20_Combat`의 Player와 같은 순서)
        yield return null;

        Assert.AreSame(mage, rig.CurrentKit as MageAttack);
        Assert.IsTrue(rig.CurrentKit is ISkillSlotSource, "마법사 키트가 슬롯 상태를 안 내준다");

        ProfileService.Current.character = CharacterId.Gunner; // 캐릭터를 바꾸는 길은 프로필 하나(PlayerRig가 따라간다)
        yield return null;
        Assert.AreSame(gunner, rig.CurrentKit as GunnerAttack, "캐릭터를 바꿨는데 HUD가 읽는 키트가 안 바뀌었다");
        Assert.IsTrue(rig.CurrentKit is ISkillSlotSource, "메카닉 키트가 슬롯 상태를 안 내준다");
    }

    /// <summary>
    /// 전투 HUD(`UI/Screens/CombatHud`, Figma)가 이 값들을 **실제로 읽는지** — `docs/worksplit.md` "단골 함정 2"
    /// (정의만 하고 호출부를 안 만드는 실수)를 `OriginalFidelityTests.DefinedApis_AreActuallyCalledSomewhere`와 같은
    /// 방식(소스를 직접 훑기)으로 막는다. 예전엔 옛 IMGUI HUD(`GameHud`)를 검사했는데 2026-11-04 그 화면을 지웠다
    /// (새 HUD가 그리고 있어 이미 아무것도 안 그리던 코드). 버프 줄(`BuffText`)도 같이 본다(2026-11-04 추가).
    /// </summary>
    [Test]
    public void CombatHud_DrawsSlotsFromTheCurrentKit()
    {
        string hud = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "UI", "Screens", "CombatHud.cs"));
        // 2026-11-05 2b: HUD는 화면 창구(`WorldView.Combat.PlayerKit`)에서 키트를 받는다 — 키트를 내주는 길(Director → PlayerRig.CurrentSkillSlots)은
        // `Systems/WorldViewTests.Director_ReportsPlayerPortalBossAndEnemies`가 실제로 확인한다.
        foreach (var use in new[] { @"\.PlayerKit\b", @"\.AttackSlot\b", @"\.SkillSlot\b", @"\.BuffText\b" })
            Assert.IsTrue(Regex.IsMatch(hud, use), $"CombatHud가 `{use}`를 안 쓴다 — 슬롯이 화면에 안 뜬다");
    }
}
}
