using System.IO;
using UnityEditor;
using UnityEngine;
using ChronoSavior.UI;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 화면 모양 프리팹을 **Unity 에디터 API로** 만든다(2026-11-22 프리팹 UI 시범 — 지금은 일시정지 하나).
    /// 예전 코드 모양(<see cref="PauseMenuView.CreateFromCode"/>, Figma 좌표)을 잠깐 지어 그대로 프리팹으로 저장한다 —
    /// 그래서 처음 만든 프리팹은 예전 화면과 같다. 프리팹 YAML을 손으로 쓰지 않는다(`HANDOFF.md`의 fileID 사고 참고).
    ///
    /// **이미 있으면 덮어쓰지 않는다** — 에디터에서 고친 모양이 코드 모양으로 되돌아가지 않게. 메뉴에선 물어보고, 배치에선 그냥 둔다.
    ///
    /// 배치 실행:
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod ChronoSavior.Editor.BuildUiViewPrefabs.RunBatch -logFile build_ui_prefabs.log -quit
    /// </summary>
    public static class BuildUiViewPrefabs
    {
        [MenuItem("ChronoSavior/UI 프리팹/일시정지 화면 만들기")]
        public static void BuildPauseMenuFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuView.AssetPath) != null &&
                !EditorUtility.DisplayDialog("일시정지 화면 프리팹",
                    PauseMenuView.AssetPath + "이 이미 있다.\n코드 모양으로 다시 만들면 에디터에서 고친 위치·크기·그림이 사라진다. 덮어쓸까?",
                    "덮어쓰기", "취소"))
                return;
            if (CreatePauseMenu()) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuView.AssetPath));
        }

        /// <summary>배치용 — 없을 때만 만든다. 실패하면 종료 코드 1.</summary>
        public static void RunBatch()
        {
            bool ok = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuView.AssetPath) != null || CreatePauseMenu();
            if (ok) Debug.Log("[BuildUiViewPrefabs] 일시정지 화면 프리팹 있음: " + PauseMenuView.AssetPath);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool CreatePauseMenu()
        {
            string path = PauseMenuView.AssetPath;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            var temp = new GameObject("BuildUiViewPrefabs_Temp", typeof(RectTransform));
            try
            {
                var view = PauseMenuView.CreateFromCode(temp.transform);
                if (!CheckArt(view)) return false;
                view.transform.SetParent(null, false);
                var saved = PrefabUtility.SaveAsPrefabAsset(view.gameObject, path, out bool success);
                Object.DestroyImmediate(view.gameObject);
                if (!success || saved == null)
                {
                    Debug.LogError("[BuildUiViewPrefabs] 프리팹 저장 실패: " + path);
                    return false;
                }
                Debug.Log("[BuildUiViewPrefabs] 일시정지 화면 프리팹을 만들었다: " + path);
                return true;
            }
            finally
            {
                if (temp != null) Object.DestroyImmediate(temp);
            }
        }

        /// <summary>그림이 빠진 채(분홍 칸) 프리팹이 되지 않게 — 그림 자리(이름 있는 Image)는 전부 Figma 그림이 있어야 한다.</summary>
        static bool CheckArt(PauseMenuView view)
        {
            bool ok = true;
            foreach (var img in view.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (img.name == "Backdrop") continue; // 한 색 막 — 그림 없음이 정상
                if (img.sprite == null)
                {
                    Debug.LogError("[BuildUiViewPrefabs] 그림이 없다: " + img.name + " — Resources/Art/Figma/UI 확인", img);
                    ok = false;
                }
            }
            return ok;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
