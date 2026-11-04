using System.IO;
using NUnit.Framework;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{
    /// <summary>
    /// **PlayMode 테스트 전체가 진짜 세이브를 못 건드리게 하는 안전장치**(2026-11-05). 이 네임스페이스의 테스트가 하나라도 돌기 전에
    /// 세이브 경로를 임시 파일로 돌려놓고(`SaveService.PathOverride`), 전부 끝나면 지운다.
    ///
    /// 예전엔 테스트마다 따로 막았다 — 진짜 세이브를 백업했다가 되돌리거나(테스트가 중간에 죽으면 진행이 날아간다) 자기 임시 경로를 썼다.
    /// 화면 버튼·캐릭터 선택처럼 **안에서 저장을 부르는 코드**를 테스트할 때 하나만 빠뜨려도 개발자의 진행을 덮어쓴다.
    /// 자기 임시 파일이 필요한 테스트는 끝날 때 <see cref="Path"/>로 되돌린다(null로 두면 진짜 경로가 된다).
    /// </summary>
    [SetUpFixture]
    public class TestSaveSandbox
    {
        /// <summary>테스트 전체가 같이 쓰는 임시 세이브 경로.</summary>
        public static string Path { get; private set; }

        [OneTimeSetUp]
        public void Begin()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ChronoSavior_TestSave_{System.Guid.NewGuid():N}.json");
            SaveService.PathOverride = Path;
        }

        [OneTimeTearDown]
        public void End()
        {
            SaveService.PathOverride = null;
            try { if (File.Exists(Path)) File.Delete(Path); } catch (IOException) { }
        }
    }
}
