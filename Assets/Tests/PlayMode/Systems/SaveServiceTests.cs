using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 세이브/로드 — 원본 `saveMeta`/`loadMeta`(project_test.html:1213·:1216).
///
/// 개발자의 진짜 세이브를 건드리지 않게 **테스트 전용 경로**(`SaveService.PathOverride`, 실행마다 다른 임시 파일)를 쓴다
/// (2026-11-05 — 예전엔 진짜 세이브 경로에 쓰고 백업으로 되돌렸는데, 테스트가 중간에 죽으면 진행이 날아갈 수 있었다).
/// </summary>
public class SaveServiceTests
{
    string tempSave;

    [SetUp]
    public void Setup()
    {
        tempSave = Path.Combine(Path.GetTempPath(), $"ChronoSavior_SaveServiceTests_{System.Guid.NewGuid():N}.json");
        SaveService.PathOverride = tempSave;
        ProfileService.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        SaveService.PathOverride = TestSaveSandbox.Path;
        try { if (File.Exists(tempSave)) File.Delete(tempSave); } catch (IOException) { }
        ProfileService.Reset();
    }

    [Test]
    public void Tests_UseTheTestSavePath_NotTheRealOne() => Assert.AreEqual(tempSave, SaveService.SavePath);

    /// <summary>테스트 전체 안전장치(`TestSaveSandbox`)가 켜져 있다 — 세이브 경로가 진짜 위치(persistentDataPath)가 아니다.</summary>
    [Test]
    public void Sandbox_KeepsEveryTestAwayFromTheRealSave()
    {
        Assert.IsNotNull(TestSaveSandbox.Path, "SetUpFixture가 안 돌았다");
        StringAssert.DoesNotStartWith(Path.GetFullPath(Application.persistentDataPath), Path.GetFullPath(TestSaveSandbox.Path));
    }

    /// <summary>
    /// 프로필을 통째로 바꾸는 건 세이브 시스템만 한다(`HANDOFF.md` "상태 소유권", 2026-11-05). `Core.ProfileService`는 `Systems`를 몰라
    /// 컴파일러로 막을 수 없으니 실행 코드(`Assets/Scripts`)를 훑어 `SaveService.cs` 밖에서 `Replace`·`Reset`을 부르지 않는지 본다.
    /// **검사 범위**: `ProfileService.Replace(`·`ProfileService.Reset(` 형태의 직접 호출. 이걸 피해 가는 `using static`·별칭은 아예 금지해서 같이 잡는다.
    /// </summary>
    [Test]
    public void OnlySaveService_ReplacesOrResetsTheProfile()
    {
        var call = new Regex(@"ProfileService\.(Replace|Reset)\s*\(|using\s+static\s+ChronoSavior\.Core\.ProfileService|=\s*ChronoSavior\.Core\.ProfileService\s*;");
        var offenders = Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != "SaveService.cs" && Path.GetFileName(f) != "ProfileService.cs")
            .Where(f => call.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName).ToArray();
        CollectionAssert.IsEmpty(offenders, "세이브 시스템 밖에서 프로필을 통째로 바꾼다 — SaveService를 거칠 것");
    }

    /// <summary>
    /// 개칭(2026-09-16, 윤회 → 시간 회귀) **이전** 세이브를 읽어도 파편과 회귀 횟수가 살아나야 한다.
    ///
    /// `JsonUtility`는 필드 이름으로 값을 맞추므로 `rp`/`rebirths`/`rpEarned`를 바꾼 순간
    /// 옛 세이브의 그 값들이 **에러도 경고도 없이 0이 된다.** 그 조용한 손실을 막는 게
    /// `SaveService`의 마이그레이션이고, 이 테스트가 그것을 고정한다.
    /// </summary>
    [Test]
    public void Load_MigratesPreRenameSave()
    {
        // 개칭 전 형식 그대로 — 새 필드 이름(shards·regressions·shardsEarned)이 하나도 없다.
        const string legacy = @"{""level"":9,""gold"":500,""rp"":126,""rebirths"":3," +
                              @"""stats"":{""totalKills"":72178,""rpEarned"":340}}";
        File.WriteAllText(SaveService.SavePath, legacy);

        Assert.IsTrue(SaveService.Load(), "옛 세이브를 못 읽었다");
        var p = ProfileService.Current;

        Assert.AreEqual(126, p.shards, "옛 rp가 시간의 파편으로 안 옮겨졌다");
        Assert.AreEqual(3, p.regressions, "옛 rebirths가 회귀 횟수로 안 옮겨졌다");
        Assert.AreEqual(340, p.stats.shardsEarned, "옛 rpEarned가 누적 파편으로 안 옮겨졌다");

        // 개칭과 무관한 값은 원래대로 읽혀야 한다(마이그레이션이 다른 걸 건드리지 않았는지).
        Assert.AreEqual(9, p.level);
        Assert.AreEqual(500, p.gold);
        Assert.AreEqual(72178, p.stats.totalKills);
    }

    /// <summary>
    /// 새 이름으로 저장된 파일에는 옛 키가 없으므로 마이그레이션이 **끼어들지 않아야** 한다.
    /// 값이 0인 게 정상인 프로필(회귀 한 번도 안 함)을 옛 값으로 덮어쓰면 안 된다.
    /// </summary>
    [Test]
    public void Load_LeavesNewFormatAlone()
    {
        var p = ProfileService.Current;
        p.shards = 0;
        p.regressions = 0;
        Assert.IsTrue(SaveService.Save());

        Assert.IsTrue(SaveService.Load());
        Assert.AreEqual(0, ProfileService.Current.shards);
        Assert.AreEqual(0, ProfileService.Current.regressions);
    }

    /// <summary>저장했다가 다시 읽으면 진행이 그대로 살아나야 한다 — 이게 안 되면 로비 강화가 무의미하다.</summary>
    [Test]
    public void SaveThenLoad_RestoresProgress()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner; // 레벨·경험치·SP는 캐릭터별이라 캐릭터를 먼저 고른다(아래 값은 메카닉 칸)
        p.level = 7;
        p.exp = 123;
        p.gold = 4560;
        p.mageBranch = MageBranch.Gravity;
        p.mageTier = 3;
        p.spUsed = 6;
        p.upgrades.atk = 11;
        p.upgrades.crit = 4;
        p.regionBossCleared[0] = true;

        Assert.IsTrue(SaveService.Save());

        ProfileService.Reset(); // 껐다 켠 셈
        Assert.AreEqual(1, ProfileService.Current.level);

        Assert.IsTrue(SaveService.Load());
        var q = ProfileService.Current;

        Assert.AreEqual(7, q.level);
        Assert.AreEqual(123, q.exp);
        Assert.AreEqual(4560, q.gold);
        Assert.AreEqual(CharacterId.Gunner, q.character);
        Assert.AreEqual(MageBranch.Gravity, q.mageBranch);
        Assert.AreEqual(3, q.mageTier);
        Assert.AreEqual(6, q.spUsed);
        Assert.AreEqual(11, q.upgrades.atk);
        Assert.AreEqual(4, q.upgrades.crit);
        Assert.IsTrue(q.regionBossCleared[0]);
    }

    /// <summary>캐릭터별 성장이 저장·불러오기를 거쳐도 캐릭터마다 그대로 남는다(원본 `meta.charProgress`).</summary>
    [Test]
    public void SaveThenLoad_KeepsEachCharactersProgress()
    {
        var p = ProfileService.Current;
        p.level = 5; p.exp = 40; p.spUsed = 2; // 마법사
        p.character = CharacterId.Gunner;
        p.level = 3; p.spUsed = 1;             // 메카닉(마지막으로 고른 캐릭터)
        Assert.IsTrue(SaveService.Save());

        ProfileService.Reset();
        Assert.IsTrue(SaveService.Load());
        var q = ProfileService.Current;

        Assert.AreEqual(CharacterId.Gunner, q.character);
        Assert.AreEqual(3, q.level);
        Assert.AreEqual(1, q.spUsed);
        var mage = q.ProgressOf(CharacterId.Mage);
        Assert.AreEqual(5, mage.level, "다른 캐릭터(마법사) 레벨이 저장에서 빠졌다");
        Assert.AreEqual(40, mage.exp);
        Assert.AreEqual(2, mage.spUsed);
    }

    /// <summary>
    /// 캐릭터별 성장 **이전** 세이브(레벨·경험치·SP가 맨 위에 하나) — 원본 `loadMeta`(:1234)처럼 **저장 당시 고른
    /// 캐릭터**에게만 옮기고, 나머지는 1레벨부터. 안 옮기면 `JsonUtility`가 옛 값을 말없이 버려 레벨이 1로 돌아간다.
    /// </summary>
    [Test]
    public void Load_MigratesSharedProgressIntoSavedCharacter()
    {
        // character 1 = 메카닉(CharacterId 순서) — JsonUtility는 enum을 정수로 쓴다.
        File.WriteAllText(SaveService.SavePath,
            @"{""level"":8,""exp"":77,""spUsed"":6,""gold"":300,""character"":1}");

        Assert.IsTrue(SaveService.Load());
        var p = ProfileService.Current;

        Assert.AreEqual(CharacterId.Gunner, p.character);
        Assert.AreEqual(8, p.level, "옛 레벨이 저장 당시 캐릭터에게 안 옮겨졌다");
        Assert.AreEqual(77, p.exp);
        Assert.AreEqual(6, p.spUsed);
        Assert.AreEqual(1, p.ProgressOf(CharacterId.Mage).level, "다른 캐릭터는 1레벨부터 시작해야 한다(원본과 같게)");
        Assert.AreEqual(300, p.gold, "골드는 모든 캐릭터가 같이 쓰는 값이라 그대로 남아야 한다");
    }

    /// <summary>
    /// **옛 세이브 호환** — 원본 `Object.assign(defaultMeta(), d)`(:1219)와 같은 동작인지 본다.
    /// 나중에 필드를 추가해도 기존 세이브가 열려야 하고, 없는 필드는 기본값이 남아야 한다.
    /// 통째로 역직렬화하면 이 경우 새 필드가 0/null로 죽는다.
    /// </summary>
    [Test]
    public void Load_FillsMissingFieldsWithDefaults()
    {
        // 아주 옛날 세이브인 척 — level/gold만 들어 있다.
        File.WriteAllText(SaveService.SavePath, "{\"level\":5,\"gold\":900}");

        Assert.IsTrue(SaveService.Load());
        var p = ProfileService.Current;

        Assert.AreEqual(5, p.level);
        Assert.AreEqual(900, p.gold);
        Assert.IsNotNull(p.upgrades, "저장에 없던 필드가 null로 죽었다 — 기본값 위에 덮어써야 한다");
        Assert.AreEqual(0, p.upgrades.atk);
        Assert.IsNotNull(p.regionBossCleared);
        Assert.AreEqual(MageBranch.None, p.mageBranch);
    }

    /// <summary>깨진 파일이어도 게임이 죽지 않고 새 프로필로 시작해야 한다(원본도 try/catch로 삼킨다).</summary>
    [Test]
    public void Load_SurvivesCorruptFile()
    {
        File.WriteAllText(SaveService.SavePath, "이건 JSON이 아니다 {{{");

        LogAssert.ignoreFailingMessages = true; // 경고 로그가 테스트를 실패시키지 않게
        bool ok = SaveService.Load();
        LogAssert.ignoreFailingMessages = false;

        Assert.IsFalse(ok, "깨진 파일을 성공으로 보고하면 안 된다");
        Assert.AreEqual(1, ProfileService.Current.level, "깨진 세이브를 읽고 프로필이 오염됐다");
    }

    /// <summary>세이브가 없으면 조용히 false — 첫 실행이 그 경우다.</summary>
    [Test]
    public void Load_ReturnsFalseWhenNoSave()
    {
        if (SaveService.HasSave) File.Delete(SaveService.SavePath);
        Assert.IsFalse(SaveService.Load());
    }

    /// <summary>전체 초기화(원본 로비 버튼 :524) — 파일도 지우고 메모리 프로필도 기본값으로.</summary>
    [Test]
    public void DeleteSave_ClearsFileAndProfile()
    {
        ProfileService.Current.gold = 9999;
        SaveService.Save();
        Assert.IsTrue(SaveService.HasSave);

        SaveService.DeleteSave();

        Assert.IsFalse(SaveService.HasSave);
        Assert.AreEqual(0, ProfileService.Current.gold);
    }
}

}
