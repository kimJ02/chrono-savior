using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using ChronoSavior.Systems;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 에디터에서 Play를 누르면 **게임 씬(`20_Combat` 등)을 열어 두었어도 `00_Persistent`부터 시작한다**(2026-11-04 씬 분할).
    /// 게임은 `00_Persistent`(UI·흐름·카메라)가 먼저 떠야 돌아가는데, 평소처럼 `20_Combat`만 열고 Play하면 UI가 없다.
    /// 빌드에는 영향이 없다(에디터 전용). **열어 둔 씬으로 바로 들어간다**(2026-11-05 3-5) — 열어 둔 씬 이름을 `SessionState`(<see cref="GameFlow.EditorEntryKey"/>)에 적고,
    /// `GameFlow.Start`가 읽어 `10_Lobby`면 로비, `20_Combat`이면 사냥(날짜 안 씀), `00_Persistent`면 타이틀로 시작한다. 월드 씬은 `GameFlow`가 불러온다.
    ///
    /// ⚠️ **테스트 실행에는 걸지 않는다.** 시작 씬을 늘 지정해 두면 Test Runner도 그 씬으로 Play해서 실제 게임이 뜨고
    /// `GameBoot`가 개발자의 진짜 세이브를 읽는다(2026-11-04 실제로 겪음 — 배치 테스트가 멈췄다). Test Runner는 저장되지 않은
    /// 임시 씬에서 Play하므로, **Build Settings에 있는 게임 씬이 활성일 때만** 이번 Play에 한해 지정하고 끝나면 지운다.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromPersistent
    {
        static PlayFromPersistent()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                string active = SceneManager.GetActiveScene().path;
                bool gameScene = System.Array.Exists(EditorBuildSettings.scenes, s => s.path == active);
                EditorSceneManager.playModeStartScene = gameScene
                    ? AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneSetupValidator.PersistentPath)
                    : null;
                // 열어 둔 씬으로 바로 들어가게 — `GameFlow.Start`가 한 번 읽고 지운다(10_Lobby → 로비, 20_Combat → 사냥, 그 밖 → 타이틀).
                if (gameScene) SessionState.SetString(GameFlow.EditorEntryKey, SceneManager.GetActiveScene().name);
                else SessionState.EraseString(GameFlow.EditorEntryKey);
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorSceneManager.playModeStartScene = null;
                SessionState.EraseString(GameFlow.EditorEntryKey);
            }
        }
    }
}
