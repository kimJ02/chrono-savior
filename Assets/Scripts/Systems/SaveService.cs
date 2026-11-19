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
        public static string SavePath => Slot > 0 ? SlotPath(Slot) : PathOverride ?? Path.Combine(Application.persistentDataPath, FileName);

        // ── 세이브 슬롯(2026-11-19, 교수님 지시 — 플레이테스트용 시나리오 3개) ──
        // 슬롯 3개는 고정 시나리오(<see cref="SavePresets"/>): 1 새 출발 · 2 성장 · 3 중력 법사 풀세팅. 슬롯마다 진행이 따로 저장되고,
        // 초기화하면 그 시나리오 처음으로 돌아간다. 슬롯을 고르기 전(타이틀)엔 아무 파일도 읽지 않는다 — 예전 profile.json은 건드리지 않고 남겨 둔다.

        public const int SlotCount = 3;

        /// <summary>지금 쓰는 슬롯(1~3). 0이면 슬롯을 아직 안 고름 — 그때 저장은 예전 profile.json으로 간다(테스트·옛 흐름).</summary>
        public static int Slot { get; private set; }

        public static string SlotPath(int slot) => Path.Combine(SlotDirOverride ?? Application.persistentDataPath, $"slot_{slot}.json");

        /// <summary>테스트용 — 슬롯 파일을 둘 폴더(실제 세이브 폴더를 안 건드리게).</summary>
        public static string SlotDirOverride;
        public static bool SlotExists(int slot) => File.Exists(SlotPath(slot));

        /// <summary>없는 슬롯은 시나리오 처음 상태로 만든다(게임 시작 때 — 빈 슬롯이 없게).</summary>
        public static void EnsureSlots()
        {
            for (int i = 1; i <= SlotCount; i++)
                if (!SlotExists(i)) ResetSlot(i);
        }

        /// <summary>그 슬롯을 시나리오 처음 상태로 덮어쓴다. 지금 쓰는 슬롯이면 메모리 프로필도 바꾼다.</summary>
        public static bool ResetSlot(int slot)
        {
            var profile = SavePresets.Build(slot);
            try { File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(profile, true)); }
            catch (System.Exception e) { Debug.LogWarning($"[SaveService] 슬롯 {slot} 초기화 실패: {e.Message}"); return false; }
            if (Slot == slot) ProfileService.Replace(profile);
            return true;
        }

        /// <summary>그 슬롯을 지금 슬롯으로 정하고 불러온다. 파일이 깨졌으면 시나리오 처음 상태로 되살린다.</summary>
        public static bool SelectSlot(int slot)
        {
            Slot = slot;
            if (!SlotExists(slot)) ResetSlot(slot);
            if (Load()) return true;
            Debug.LogWarning($"[SaveService] 슬롯 {slot}을 못 읽어 시나리오 처음으로 되돌린다");
            return ResetSlot(slot) && Load();
        }

        /// <summary>슬롯 화면이 보여 줄 요약 — 파일을 읽기만 한다(지금 프로필은 안 바뀐다). 못 읽으면 null.</summary>
        public static PlayerProfile Peek(int slot)
        {
            try
            {
                if (!SlotExists(slot)) return null;
                var p = new PlayerProfile();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(SlotPath(slot)), p);
                return p;
            }
            catch { return null; }
        }

        /// <summary>슬롯 파일을 마지막으로 쓴 시각(없으면 null).</summary>
        public static System.DateTime? SlotTime(int slot) => SlotExists(slot) ? File.GetLastWriteTime(SlotPath(slot)) : (System.DateTime?)null;

        /// <summary>테스트용 — 슬롯 선택을 지운다.</summary>
        public static void ClearSlotForTests() => Slot = 0;

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
                ProfileService.Replace(profile);
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

        /// <summary>
        /// 세이브를 지우고 새 프로필로 — 원본 로비의 "전체 초기화" 버튼(project_test.html:524). 지금은 타이틀의 "새 원정"이
        /// `GameFlow.StartNewExpedition`을 거쳐 부른다.
        /// </summary>
        public static void DeleteSave()
        {
            try { if (HasSave) File.Delete(SavePath); }
            catch (System.Exception e) { Debug.LogWarning($"[SaveService] 삭제 실패: {e.Message}"); }
            ProfileService.Reset();
        }
    }
}
