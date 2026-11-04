using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 설정 값(화면 모드·해상도·음량) — 기기마다 따로라 세이브(`profile.json`)가 아니라 PlayerPrefs에 둔다(2026-10-24).
    /// 원본엔 설정 화면이 없다. Figma에도 설정 창 그림은 없어서, 사용자가 고른 항목(화면 모드·해상도·음량·조작키 안내)만 넣었다.
    /// 소리는 아직 게임에 없어서 음량은 값만 저장한다 — 효과음·배경음을 넣을 때 여기서 읽는다.
    /// </summary>
    public static class GameSettings
    {
        const string KeyFullscreen = "settings.fullscreen";
        const string KeyWidth = "settings.width";
        const string KeyHeight = "settings.height";
        const string KeyBgm = "settings.bgm";
        const string KeySfx = "settings.sfx";

        public const int VolumeMax = 10;

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); ApplyScreen(); }
        }

        public static int BgmVolume
        {
            get => PlayerPrefs.GetInt(KeyBgm, 8);
            set => PlayerPrefs.SetInt(KeyBgm, Mathf.Clamp(value, 0, VolumeMax));
        }

        public static int SfxVolume
        {
            get => PlayerPrefs.GetInt(KeySfx, 8);
            set => PlayerPrefs.SetInt(KeySfx, Mathf.Clamp(value, 0, VolumeMax));
        }

        /// <summary>고를 수 있는 해상도 — 640×360의 정수배(UI가 도트 그대로 커지는 크기)만, 모니터보다 크지 않게.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            var max = Screen.currentResolution;
            for (int k = 2; k <= 6; k++)
            {
                int w = 640 * k, h = 360 * k;
                if (k > 2 && (w > max.width || h > max.height)) break;
                list.Add(new Vector2Int(w, h));
            }
            return list;
        }

        public static Vector2Int Resolution
        {
            get => new Vector2Int(PlayerPrefs.GetInt(KeyWidth, Screen.width), PlayerPrefs.GetInt(KeyHeight, Screen.height));
            set
            {
                PlayerPrefs.SetInt(KeyWidth, value.x);
                PlayerPrefs.SetInt(KeyHeight, value.y);
                ApplyScreen();
            }
        }

        static void ApplyScreen()
        {
            PlayerPrefs.Save();
            if (Application.isEditor) return; // 에디터 Game 창 크기는 에디터가 정한다
            var r = Resolution;
            Screen.SetResolution(r.x, r.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
