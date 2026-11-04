using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 테스트 세이브 안전장치(`TestSaveSandbox`)가 **실제로 쓰이는지** — 이 클래스는 자기 임시 경로를 따로 두지 않으므로, 지금 세이브가 가는 곳이
/// 곧 다른 테스트들이 저장을 부를 때 가는 곳이다. 그게 안전장치 경로이고 진짜 세이브 위치(persistentDataPath) 밖이어야 한다.
/// 테스트 순서는 보장되지 않으므로 이 검사만으로는 '다른 테스트가 도는 동안'을 보장하지 못한다 — 그래서 아래 소스 검사가 같이 막는다:
/// 테스트 코드 어디에서도 세이브 경로를 진짜 경로(null)로 되돌리지 않고(안전장치 자신만 예외), 경로를 바꾼 테스트는 반드시 안전장치 경로로 되돌린다.
/// </summary>
public class TestSaveSandboxTests
{
    [Test]
    public void ActiveSavePath_IsTheSandbox_NotTheRealSave()
    {
        Assert.IsNotNull(TestSaveSandbox.Path, "SetUpFixture가 안 돌았다");
        Assert.AreEqual(TestSaveSandbox.Path, SaveService.SavePath, "지금 세이브 경로가 안전장치 경로가 아니다");
        StringAssert.DoesNotStartWith(Path.GetFullPath(Application.persistentDataPath), Path.GetFullPath(SaveService.SavePath),
            "세이브가 진짜 위치로 간다");
    }

    [Test]
    public void NoTest_PointsTheSaveBackAtTheRealPath()
    {
        string root = Path.Combine(Application.dataPath, "Tests");
        var assign = new Regex(@"PathOverride\s*=(?!=)\s*([^;]+);");
        var problems = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != "TestSaveSandbox.cs" && Path.GetFileName(f) != "TestSaveSandboxTests.cs") // 안전장치 자신과 이 검사(문구에 예시가 있다)
            .SelectMany(f =>
            {
                var values = assign.Matches(File.ReadAllText(f)).Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToArray();
                var bad = values.Where(v => v == "null").Select(_ => $"{Path.GetFileName(f)}: PathOverride = null(진짜 경로가 된다 — TestSaveSandbox.Path로 되돌릴 것)").ToList();
                if (values.Length > 0 && !values.Contains("TestSaveSandbox.Path"))
                    bad.Add($"{Path.GetFileName(f)}: 세이브 경로를 바꾸고 안전장치 경로로 되돌리지 않는다");
                return bad;
            })
            .ToArray();
        CollectionAssert.IsEmpty(problems);
    }
}

}
