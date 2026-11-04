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
        "GunnerFieldLinks",
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
        var g = go.AddComponent<GunnerAttack>();
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
        var mage = new GameObject("TestMage").AddComponent<MageAttack>(); // 이동기가 없으면 순간이동만 건너뛴다
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
        var mage = go.AddComponent<MageAttack>();
        var gunner = go.AddComponent<GunnerAttack>();
        var rig = go.AddComponent<PlayerRig>(); // 키트를 전부 붙인 뒤에(BuildPartAScene과 같은 순서)
        yield return null;

        Assert.AreSame(mage, rig.CurrentKit as MageAttack);
        Assert.IsTrue(rig.CurrentKit is ISkillSlotSource, "마법사 키트가 슬롯 상태를 안 내준다");

        Assert.IsTrue(rig.Select(CharacterId.Gunner));
        Assert.AreSame(gunner, rig.CurrentKit as GunnerAttack, "캐릭터를 바꿨는데 HUD가 읽는 키트가 안 바뀌었다");
        Assert.IsTrue(rig.CurrentKit is ISkillSlotSource, "메카닉 키트가 슬롯 상태를 안 내준다");
    }

    /// <summary>
    /// HUD가 이 값들을 **실제로 읽는지** — 그리는 쪽은 IMGUI라 배치로 못 돌리므로, `docs/worksplit.md` "단골 함정 2"
    /// (정의만 하고 호출부를 안 만드는 실수)를 `OriginalFidelityTests.DefinedApis_AreActuallyCalledSomewhere`와 같은
    /// 방식(소스를 직접 훑기)으로 막는다. 그 목록은 공용 파일이라 병렬 작업 중 충돌을 피해 여기 따로 뒀다.
    /// </summary>
    [Test]
    public void GameHud_DrawsSlotsFromTheCurrentKit()
    {
        string hud = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "UI", "GameHud.cs"));
        // 단어 경계(\b)를 둔다 — `.SkillSlot`만 찾으면 `UiTheme.SkillSlotAttack`에도 걸려 검사가 헛돈다.
        foreach (var use in new[]
                 {
                     @"DrawSkillSlots\(\);", @"CurrentKit as ISkillSlotSource\b",
                     @"\.AttackSlot\b", @"\.SkillSlot\b", @"\.BuffText\b",
                     @"UiTheme\.SkillSlotAttack\b", @"UiTheme\.SkillSlotSkill\b", @"UiTheme\.SkillSlotJump\b",
                 })
            Assert.IsTrue(Regex.IsMatch(hud, use), $"GameHud가 `{use}`를 안 쓴다 — 슬롯이나 버프 줄이 화면에 안 뜬다");
    }

    /// <summary>
    /// 씬의 `UiTextures`에 Figma 슬롯 그림 3장이 꽂혀 있는지 — 빠지면 에러 없이 **도형 대체 그림**으로 조용히 바뀐다
    /// (눈으로만 잡히는 종류). 씬은 `BuildPartAScene`을 다시 돌리지 않고 참조 3줄을 직접 넣었다.
    /// </summary>
    [Test]
    public void Scene_WiresTheFigmaSlotArt()
    {
        string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes", "CombatCore.unity"));
        foreach (var (field, file) in new[]
                 {
                     ("skillSlotAttack", "skill_slot_attack"), ("skillSlotSkill", "skill_slot_skill"),
                     ("skillSlotJump", "skill_slot_jump"),
                 })
        {
            string meta = File.ReadAllText(Path.Combine(Application.dataPath, "Sprites", "UI", file + ".png.meta"));
            string guid = Regex.Match(meta, @"^guid: ([0-9a-f]{32})", RegexOptions.Multiline).Groups[1].Value;
            Assert.IsNotEmpty(guid, file + ".png.meta에 guid가 없다");
            StringAssert.Contains($"{field}: {{fileID: 2800000, guid: {guid}, type: 3}}", scene,
                $"씬의 UiTextures.{field}에 {file}.png가 안 꽂혀 있다 — 슬롯이 Figma 그림 대신 도형으로 그려진다");
        }
    }
}
}
