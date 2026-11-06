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
/// 주석·문자열은 세지 않는다, `SourceScan`). 대입·`+=`·`++`까지 쓰기로 센다. `Core/GameState.cs`도 대상이다(정의 안에서는 `GameState.` 없이 부르므로 걸리지 않는다). 에디터 도구·테스트는 대상이 아니다.
/// </summary>
public class StateOwnershipTests
{
    const string Owner = "Systems/GameFlow.cs";

    static readonly Regex StateWrite = new Regex(@"\bTime\s*\.\s*timeScale\s*(?:[-+*/]?=(?!=)|\+\+|--)|(?:\+\+|--)\s*Time\s*\.\s*timeScale\b|\bGameState\s*\.\s*(Set|SetTransitioning)\s*\(");

    static int CountWrites(string source) => StateWrite.Matches(SourceScan.CodeOnly(source)).Count; // 주석·문자열을 뺀 코드 전체

    [Test]
    public void OnlyGameFlow_ChangesTheScreenStateAndTimeScale()
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        var problems = new List<string>();
        int ownerWrites = 0;
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string rel = file.Substring(root.Length + 1).Replace('\\', '/');
            int n = CountWrites(File.ReadAllText(file));
            if (rel == Owner) { ownerWrites = n; continue; }
            if (n > 0) problems.Add($"{rel}: 화면 상태·timeScale을 {n}곳에서 바꾼다 — GameFlow에 요청하거나(이벤트) GameFlow로 옮길 것");
        }
        CollectionAssert.IsEmpty(problems);
        Assert.Greater(ownerWrites, 0, "검사식이 GameFlow의 대입도 못 찾으면 검사 자체가 고장난 것");
    }
}

}
