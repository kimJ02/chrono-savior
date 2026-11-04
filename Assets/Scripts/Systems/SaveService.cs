using System.IO;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 세이브/로드 — 원본 `saveMeta()`/`loadMeta()`(project_test.html:1213·:1216).
    /// 원본은 localStorage에 `meta`를 통째로 JSON으로 넣는다. 우리도 같은 구조로
    /// <see cref="PlayerProfile"/> 하나를 파일로 쓴다.
    ///
    /// CLAUDE.md 폴더 규칙: **데이터 형태는 `Core`, 실제 파일 입출력은 `Systems`.**
    /// 그래서 `PlayerProfile`은 Core에 있고 읽고 쓰는 건 여기다.
    ///
    /// ## 기본값 위에 덮어쓰는 이유
    /// 원본 `loadMeta`는 `Object.assign(defaultMeta(), d)`로 **기본값에서 시작해 저장분을 덮는다**
    /// (`:1219`). 그래야 나중에 필드가 추가돼도 옛 세이브가 그대로 열린다 — 없는 필드는 기본값이 남는다.
    /// `JsonUtility.FromJsonOverwrite`가 정확히 같은 동작이라 그대로 썼다.
    /// 통째로 역직렬화(`FromJson`)하면 옛 세이브에서 새 필드가 0/null로 죽는다.
    ///
    /// ## 자동 저장은 여기 없다
    /// 이 클래스는 **부르면 하는 일만** 한다. 자동 저장/로드는 <see cref="AutoSave"/>가 맡는다 —
    /// 그래야 PlayMode 테스트가 실제 세이브 파일을 건드리지 않는다(테스트는 이 컴포넌트를 안 붙인다).
    /// </summary>
    public static class SaveService
    {
        const string FileName = "profile.json";

        /// <summary>
        /// 저장 위치. `Application.persistentDataPath`는 플랫폼마다 다른 "이 앱의 저장 폴더"다
        /// (에디터/윈도우는 `%USERPROFILE%/AppData/LocalLow/<회사>/<제품>`).
        /// 프로젝트 폴더에 쓰면 git에 섞이고 빌드본에선 쓰기 권한이 없을 수 있다.
        /// </summary>
        public static string SavePath => PathOverride ?? Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// 테스트 전용 저장 위치. 실제 게임 씬을 띄우는 통합 테스트(`GameBootTests`)가 개발자의 진짜 세이브를 건드리지 않게
        /// 임시 파일을 가리킨다. 게임 코드에선 쓰지 않는다(null = 기본 위치).
        /// </summary>
        public static string PathOverride;

        public static bool HasSave => File.Exists(SavePath);

        /// <summary>현재 프로필을 파일로 쓴다. 실패해도 게임은 계속 돌아야 하므로 삼키고 경고만 남긴다.</summary>
        public static bool Save()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(ProfileService.Current, true));
                return true;
            }
            catch (System.Exception e)
            {
                // 원본도 `try { ... } catch (e) {}`로 조용히 넘어간다(:1214) — 저장 실패로 게임이 멈추면 안 된다.
                Debug.LogWarning($"[SaveService] 저장 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>저장분을 읽어 <see cref="ProfileService.Current"/>를 교체한다. 파일이 없으면 false.</summary>
        public static bool Load()
        {
            if (!HasSave) return false;
            try
            {
                string json = File.ReadAllText(SavePath);
                var profile = new PlayerProfile(); // 기본값에서 시작 — 위 "기본값 위에 덮어쓰는 이유" 참고
                JsonUtility.FromJsonOverwrite(json, profile);
                MigrateLegacyRebirthFields(json, profile);
                MigrateSharedProgress(json, profile);
                ProfileService.Current = profile;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveService] 불러오기 실패, 새 프로필로 시작: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 2026-09-16 개칭(윤회 → 시간 회귀 / 윤회 포인트 → 시간의 파편) **이전** 세이브를 살린다.
        ///
        /// `JsonUtility`는 **필드 이름으로** 값을 맞추므로, `rp`/`rebirths`/`rpEarned`를
        /// `shards`/`regressions`/`shardsEarned`로 바꾼 순간 옛 세이브의 그 값들이 조용히 0이 된다 —
        /// 에러도 경고도 없이 파편과 회귀 횟수만 사라지는, **알아채기 어려운 손실**이다.
        /// 그래서 옛 이름으로 한 번 더 읽어 새 필드가 비어 있을 때만 옮겨 담는다.
        ///
        /// 새 이름으로 저장된 파일에는 옛 키가 없으므로 이 함수는 아무 일도 하지 않는다.
        /// 한 번 저장되면 옛 키는 파일에서 사라지니, 다음 개칭 때 이 코드는 지워도 된다.
        /// </summary>
        static void MigrateLegacyRebirthFields(string json, PlayerProfile profile)
        {
            var legacy = JsonUtility.FromJson<LegacyProfile>(json);
            if (legacy == null) return;

            if (profile.shards == 0 && legacy.rp != 0) profile.shards = legacy.rp;
            if (profile.regressions == 0 && legacy.rebirths != 0) profile.regressions = legacy.rebirths;
            if (profile.stats != null && profile.stats.shardsEarned == 0 && legacy.stats != null
                && legacy.stats.rpEarned != 0)
                profile.stats.shardsEarned = legacy.stats.rpEarned;
        }

        /// <summary>
        /// 캐릭터별 성장(2026-09-30) **이전** 세이브를 살린다 — 그땐 레벨·경험치·쓴 SP가 맨 위에 하나씩만 있었다.
        /// 지금은 그 이름이 프로퍼티라 `JsonUtility`가 옛 값을 **말없이 버린다**(위 개칭 때와 같은 종류의 손실).
        ///
        /// 원본 `loadMeta`와 같게(`if (!d.charProgress && d.level) meta.charProgress[지금 캐릭터] = {...}` :1234)
        /// **저장할 때 고른 캐릭터**에게 옮기고, 나머지 캐릭터는 1레벨부터 시작한다. 새 형식(`charProgress`가 있는
        /// 파일)에는 옛 키가 없으니 아무 일도 안 한다.
        /// </summary>
        static void MigrateSharedProgress(string json, PlayerProfile profile)
        {
            if (json.Contains("\"charProgress\"")) return; // 새 형식이면 그걸 믿는다
            var legacy = JsonUtility.FromJson<LegacyProfile>(json);
            if (legacy == null || legacy.level <= 0) return; // 옛 레벨 키도 없다 — 옮길 게 없다

            var cp = profile.ProgressOf(profile.character);
            cp.level = legacy.level;
            cp.exp = legacy.exp;
            cp.spUsed = legacy.spUsed;
        }

        /// <summary>
        /// 옛 필드 이름만 담은 읽기 전용 형태 — 개칭 전(<see cref="MigrateLegacyRebirthFields"/>)과 캐릭터별 성장
        /// 전(<see cref="MigrateSharedProgress"/>) 세이브용.
        /// </summary>
        [System.Serializable]
        class LegacyProfile
        {
            public int rp;
            public int rebirths;
            public LegacyStats stats;
            public int level; // 0이면 키가 없었다(옛 세이브는 늘 1 이상)
            public int exp;
            public int spUsed;

            [System.Serializable]
            public class LegacyStats { public int rpEarned; }
        }

        /// <summary>전체 초기화(원본 로비의 "전체 초기화" 버튼, project_test.html:524).</summary>
        public static void DeleteSave()
        {
            try { if (HasSave) File.Delete(SavePath); }
            catch (System.Exception e) { Debug.LogWarning($"[SaveService] 삭제 실패: {e.Message}"); }
            ProfileService.Reset();
        }
    }
}
