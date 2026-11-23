using System.IO;
using UnityEditor;
using UnityEngine;
using ChronoSavior.UI;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 화면 모양 프리팹을 **Unity 에디터 API로** 만든다(2026-11-22~ 프리팹 UI 점진 전환 — 1호 일시정지, 2호 공용 팝업, 3호 타이틀, 4호 시간 회귀, 5호 설정).
    /// 예전 코드 모양(각 View의 <c>CreateFromCode</c>, Figma 좌표)을 잠깐 지어 그대로 프리팹으로 저장한다 —
    /// 그래서 처음 만든 프리팹은 예전 화면과 같다. 프리팹 YAML을 손으로 쓰지 않는다(`HANDOFF.md`의 fileID 사고 참고).
    ///
    /// **이미 있으면 덮어쓰지 않는다** — 에디터에서 고친 모양이 코드 모양으로 되돌아가지 않게. 메뉴에선 물어보고, 배치에선 그냥 둔다.
    ///
    /// 배치 실행(없는 것만 만든다):
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod ChronoSavior.Editor.BuildUiViewPrefabs.RunBatch -logFile build_ui_prefabs.log -quit
    /// </summary>
    public static class BuildUiViewPrefabs
    {
        /// <summary>화면 하나 — 이름(안내용), 프리팹 경로, 코드 모양을 짓는 함수.</summary>
        struct ViewSpec
        {
            public string Label, AssetPath;
            public System.Func<Transform, Component> Build;
            /// <summary>그림 없이 색만 칠하는 칸(막·여백·반투명 판) — 그림 빠짐 검사에서 뺀다.</summary>
            public string[] ColorOnly;
        }

        static readonly ViewSpec Pause = new ViewSpec
        {
            Label = "일시정지 화면", AssetPath = PauseMenuView.AssetPath, Build = PauseMenuView.CreateFromCode,
            ColorOnly = new[] { "Backdrop" },
        };

        static readonly ViewSpec Popup = new ViewSpec
        {
            Label = "공용 팝업", AssetPath = CommonPopupView.AssetPath, Build = CommonPopupView.CreateFromCode,
            ColorOnly = new[] { "Backdrop" },
        };

        static readonly ViewSpec Title = new ViewSpec
        {
            Label = "타이틀 화면", AssetPath = TitleMenuView.AssetPath, Build = TitleMenuView.CreateFromCode,
            ColorOnly = new[] { "Letterbox", "Menu0", "Menu1", "Menu2", "Menu3" }, // 검은 여백 · 반투명 메뉴 판
        };

        static readonly ViewSpec Regression = new ViewSpec
        {
            Label = "시간 회귀 창", AssetPath = RegressionScreenView.AssetPath, Build = RegressionScreenView.CreateFromCode,
            ColorOnly = new[] { "Backdrop" },
        };

        static readonly ViewSpec Settings = new ViewSpec
        {
            Label = "설정 창", AssetPath = SettingsWindowView.AssetPath, Build = SettingsWindowView.CreateFromCode,
            ColorOnly = new[] { "Backdrop", "VolumeHit", "Divider" }, // 막 · 음량 막대 누르기 칸(투명) · 나눔선
        };

        static readonly ViewSpec[] All = { Pause, Popup, Title, Regression, Settings };

        [MenuItem("ChronoSavior/UI 프리팹/일시정지 화면 만들기")]
        public static void BuildPauseMenuFromMenu() => BuildFromMenu(Pause);

        [MenuItem("ChronoSavior/UI 프리팹/공용 팝업 만들기")]
        public static void BuildCommonPopupFromMenu() => BuildFromMenu(Popup);

        [MenuItem("ChronoSavior/UI 프리팹/타이틀 화면 만들기")]
        public static void BuildTitleMenuFromMenu() => BuildFromMenu(Title);

        [MenuItem("ChronoSavior/UI 프리팹/시간 회귀 창 만들기")]
        public static void BuildRegressionFromMenu() => BuildFromMenu(Regression);

        [MenuItem("ChronoSavior/UI 프리팹/설정 창 만들기")]
        public static void BuildSettingsFromMenu() => BuildFromMenu(Settings);

        /// <summary>배치용 — 없는 것만 만든다. 하나라도 실패하면 종료 코드 1.</summary>
        public static void RunBatch()
        {
            bool ok = true;
            foreach (var spec in All)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath) != null)
                {
                    Debug.Log("[BuildUiViewPrefabs] " + spec.Label + " 프리팹 있음: " + spec.AssetPath);
                    continue;
                }
                ok &= Create(spec);
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static void BuildFromMenu(ViewSpec spec)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath) != null &&
                !EditorUtility.DisplayDialog(spec.Label + " 프리팹",
                    spec.AssetPath + "이 이미 있다.\n코드 모양으로 다시 만들면 에디터에서 고친 위치·크기·그림이 사라진다. 덮어쓸까?",
                    "덮어쓰기", "취소"))
                return;
            if (Create(spec)) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath));
        }

        static bool Create(ViewSpec spec)
        {
            EnsureFolder(Path.GetDirectoryName(spec.AssetPath).Replace('\\', '/'));

            var temp = new GameObject("BuildUiViewPrefabs_Temp", typeof(RectTransform));
            try
            {
                var view = spec.Build(temp.transform);
                if (!CheckArt(view, spec.ColorOnly)) return false;
                view.transform.SetParent(null, false);
                var saved = PrefabUtility.SaveAsPrefabAsset(view.gameObject, spec.AssetPath, out bool success);
                Object.DestroyImmediate(view.gameObject);
                if (!success || saved == null)
                {
                    Debug.LogError("[BuildUiViewPrefabs] 프리팹 저장 실패: " + spec.AssetPath);
                    return false;
                }
                Debug.Log("[BuildUiViewPrefabs] " + spec.Label + " 프리팹을 만들었다: " + spec.AssetPath);
                return true;
            }
            finally
            {
                if (temp != null) Object.DestroyImmediate(temp);
            }
        }

        /// <summary>그림이 빠진 채(분홍 칸) 프리팹이 되지 않게 — 그림 자리(이름 있는 Image)는 전부 Figma 그림이 있어야 한다.</summary>
        static bool CheckArt(Component view, string[] colorOnly)
        {
            bool ok = true;
            foreach (var img in view.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (colorOnly != null && System.Array.IndexOf(colorOnly, img.name) >= 0) continue; // 색만 칠하는 칸 — 그림 없음이 정상
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
