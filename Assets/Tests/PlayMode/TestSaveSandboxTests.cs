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
        // 폴더 경계까지 비교한다(이름이 같은 접두어로 시작하는 옆 폴더를 진짜 위치로 오인하지 않게)
        string real = Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringAssert.DoesNotStartWith(real, Path.GetFullPath(SaveService.SavePath), "세이브가 진짜 위치로 간다");
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
                var bad = values.Where(v => Regex.IsMatch(v, @"\bnull\b")).Select(_ => $"{Path.GetFileName(f)}: PathOverride = null(진짜 경로가 된다 — TestSaveSandbox.Path로 되돌릴 것)").ToList();
                if (values.Length > 0 && !values.Contains("TestSaveSandbox.Path"))
                    bad.Add($"{Path.GetFileName(f)}: 세이브 경로를 바꾸고 안전장치 경로로 되돌리지 않는다");
                return bad;
            })
            .ToArray();
        CollectionAssert.IsEmpty(problems);
    }

    /// <summary>
    /// 안전장치(`TestSaveSandbox`, NUnit `SetUpFixture`)는 **그 네임스페이스의 테스트에만** 걸린다 — 모든 테스트 파일이 같은 네임스페이스에 있어야
    /// 새 테스트가 안전장치 밖에서 진짜 세이브를 건드리지 않는다(2026-11-07 최종 점검).
    /// </summary>
    [Test]
    public void EveryTestFile_IsInTheSandboxedNamespace()
    {
        string root = Path.Combine(Application.dataPath, "Tests");
        string ns = typeof(TestSaveSandbox).Namespace;
        var outside = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !AllTypesInside(SourceScan.CodeOnly(File.ReadAllText(f)), ns))
            .Select(Path.GetFileName)
            .ToArray();
        CollectionAssert.IsEmpty(outside, $"이 파일들은 {ns} 밖이라 세이브 안전장치가 안 걸린다");
    }

    /// <summary>파일의 namespace 선언이 전부 <paramref name="ns"/>이고, 그 첫 선언보다 앞에 형식 선언이 없는지(주석·문자열은 뺀 코드로).</summary>
    static bool AllTypesInside(string code, string ns)
    {
        var decls = Regex.Matches(code, @"\bnamespace\s+([\w.]+)");
        if (decls.Count == 0) return false;
        if (decls.Cast<Match>().Any(m => m.Groups[1].Value != ns)) return false;
        string head = code.Substring(0, decls[0].Index);
        return !Regex.IsMatch(head, @"\b(class|struct|interface|enum|record)\s+\w+");
    }
}

}
