using UnityEditor;
using UnityEngine;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 메카닉 스킬트리 **수동 테스트용** 에디터 메뉴(`ChronoSavior/메카닉 테스트/…`) — Play 중에만 동작한다.
    /// 로비에서 SP를 모아 1층부터 찍어 올리지 않아도 원하는 갈래·층을 바로 세팅해서 눈으로 확인할 수 있게 한다.
    ///
    /// 게임 코드·씬은 건드리지 않는다. CLAUDE.md의 "원본에 없던 임시 디버그 키를 되살리지 말 것"은 **게임 안 입력**
    /// 얘기라, 빌드에 들어가지 않는 에디터 메뉴는 해당하지 않는다. 세팅은 진짜 세이브(`ProfileService.Current`)에
    /// 쓰므로 로비 전문화 탭에도 그대로 보이고, 로비로 돌아가거나 결과 화면이 뜨면 자동 저장된다 —
    /// 테스트가 끝나면 로비의 "전체 초기화"로 지우면 된다.
    /// </summary>
    public static class MechanicTestMenu
    {
        const string Root = "ChronoSavior/메카닉 테스트/";

        [MenuItem(Root + "레이저 1층", priority = 1)] static void Laser1() => Set(GunnerBranch.Laser, 1);
        [MenuItem(Root + "레이저 2층", priority = 2)] static void Laser2() => Set(GunnerBranch.Laser, 2);
        [MenuItem(Root + "레이저 3층", priority = 3)] static void Laser3() => Set(GunnerBranch.Laser, 3);
        [MenuItem(Root + "레이저 4층", priority = 4)] static void Laser4() => Set(GunnerBranch.Laser, 4);
        [MenuItem(Root + "레이저 5층", priority = 5)] static void Laser5() => Set(GunnerBranch.Laser, 5);

        [MenuItem(Root + "설치기 1층", priority = 21)] static void Installer1() => Set(GunnerBranch.Installer, 1);
        [MenuItem(Root + "설치기 2층", priority = 22)] static void Installer2() => Set(GunnerBranch.Installer, 2);
        [MenuItem(Root + "설치기 3층", priority = 23)] static void Installer3() => Set(GunnerBranch.Installer, 3);
        [MenuItem(Root + "설치기 4층", priority = 24)] static void Installer4() => Set(GunnerBranch.Installer, 4);
        [MenuItem(Root + "설치기 5층", priority = 25)] static void Installer5() => Set(GunnerBranch.Installer, 5);

        [MenuItem(Root + "0차로 되돌리기", priority = 41)] static void Zero() => Set(GunnerBranch.None, 0);

        [MenuItem(Root + "SP 넉넉히 (레벨 16 = SP 15)", priority = 61)]
        static void PlentySp()
        {
            if (!RequirePlaying()) return;
            var p = ProfileService.Current;
            p.level = Mathf.Max(p.level, 16); // 한 갈래 1~5층 전부(1+2+3+4+5 = 15)를 로비에서 직접 찍어 볼 수 있다
            Debug.Log($"[메카닉 테스트] 레벨 {p.level} — 사용 가능 SP {p.SpAvailable}");
        }

        /// <summary>
        /// 메카닉으로 바꾸고 빌드를 세팅한다. **메카닉의** SP 장부도 맞춘다(원본 층 비용 1+…+N만큼 쓴 것으로 치고,
        /// 레벨이 모자라면 올린다 — SP는 캐릭터마다 따로라 마법사 몫은 안 건드린다). 안 맞추면 전문화 탭 SP가 음수로 보인다.
        /// 충전 스택·부품·X 쿨다운은 사냥 시작처럼 새로 시작한다(설치기는 부품 1개, 원본 `resetPlayerForRun`).
        /// </summary>
        static void Set(GunnerBranch branch, int tier)
        {
            if (!RequirePlaying()) return;
            var p = ProfileService.Current;
            p.character = CharacterId.Gunner; // PlayerRig가 다음 프레임에 메카닉 키트로 바꾼다
            p.gunnerBranch = tier >= 1 ? branch : GunnerBranch.None;
            p.gunnerTier = tier;

            // 위에서 캐릭터를 메카닉으로 바꿨으므로 level·spUsed는 메카닉 칸이다(PlayerProfile.Progress).
            int spent = TierCostSum(tier);
            p.spUsed = spent;
            p.level = Mathf.Max(p.level, spent + 1);

            var gunner = Object.FindFirstObjectByType<GunnerAttack>(FindObjectsInactive.Include);
            if (gunner != null) gunner.ResetForRun();

            string build = tier >= 1 ? $"{(branch == GunnerBranch.Laser ? "레이저" : "설치기")} {tier}층" : "0차";
            Debug.Log($"[메카닉 테스트] 메카닉 · {build} (레벨 {p.level}, 남은 SP {p.SpAvailable})");
        }

        /// <summary>원본 두 캐릭터 모두 층 비용이 1,2,3,4,5라 N층까지 누적 비용은 1+…+N이다.</summary>
        static int TierCostSum(int tier) => tier * (tier + 1) / 2;

        static bool RequirePlaying()
        {
            if (EditorApplication.isPlaying) return true;
            EditorUtility.DisplayDialog("메카닉 테스트", "Play 중에만 쓸 수 있어요. ▶를 누른 뒤 다시 골라 주세요.", "확인");
            return false;
        }
    }
}
