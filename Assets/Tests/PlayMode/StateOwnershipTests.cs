using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// **화면 상태(`GameState.Set`·`SetTransitioning`)와 시간(`Time.timeScale` 대입)은 `Systems/GameFlow`만 바꾼다**(`HANDOFF.md` "상태 소유권", 2026-11-05 씬 분할 2차 4단계 마감).
/// 다른 곳이 바꾸면 일시정지·결과·히트스톱·화면 전환이 서로 덮어써서 게임이 안 멈추거나 안 풀린다. 소스를 직접 훑어 막는다(`WorldLookupTests`와 같은 방식 —
/// 줄 전체가 주석인 줄은 세지 않는다). 정의하는 `Core/GameState.cs`는 대상이 아니다. 에디터 도구·테스트도 대상이 아니다.
/// </summary>
public class StateOwnershipTests
{
    const string Owner = "Systems/GameFlow.cs";
    const string Definition = "Core/GameState.cs";

    static readonly Regex StateWrite = new Regex(@"\bTime\.timeScale\s*=(?!=)|\bGameState\.(Set|SetTransitioning)\s*\(");

    static int CountWrites(string source) =>
        source.Split('\n').Where(l => !l.TrimStart().StartsWith("//")).Sum(l => StateWrite.Matches(l).Count);

    [Test]
    public void OnlyGameFlow_ChangesTheScreenStateAndTimeScale()
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        var problems = new List<string>();
        int ownerWrites = 0;
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string rel = file.Substring(root.Length + 1).Replace('\\', '/');
            if (rel == Definition) continue;
            int n = CountWrites(File.ReadAllText(file));
            if (rel == Owner) { ownerWrites = n; continue; }
            if (n > 0) problems.Add($"{rel}: 화면 상태·timeScale을 {n}곳에서 바꾼다 — GameFlow에 요청하거나(이벤트) GameFlow로 옮길 것");
        }
        CollectionAssert.IsEmpty(problems);
        Assert.Greater(ownerWrites, 0, "검사식이 GameFlow의 대입도 못 찾으면 검사 자체가 고장난 것");
    }
}

}
