using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 설정 값(화면 모드·해상도·음량) — 기기마다 따로라 세이브(`profile.json`)가 아니라 PlayerPrefs에 둔다(2026-10-24).
    /// 원본엔 설정 화면이 없다. 2026-11-16 Figma 새 설정창에 맞춰 마스터 볼륨이 생겼다(효과음·배경음에 곱한다).
    /// 배경음은 음원이 아직 없어 값만 저장한다 — 넣을 때 <see cref="BgmScale"/>을 곱한다.
    ///
    /// 값은 처음 한 번 PlayerPrefs에서 읽어 들고 있다(Gemini 검토 2026-11-16 — 설정창은 매 프레임, 효과음은 재생마다 읽어서
    /// 그때마다 PlayerPrefs를 뒤지지 않게). 쓰기는 이 클래스로만 하므로 들고 있는 값이 늘 PlayerPrefs와 같다.
    /// 음량은 바꿀 때 PlayerPrefs에만 쓰고 디스크 저장은 설정창이 닫힐 때(<see cref="Save"/>) — 화면 값은 바로 저장한다.
    /// </summary>
    public static class GameSettings
    {
        const string KeyFullscreen = "settings.fullscreen";
        const string KeyWidth = "settings.width";
        const string KeyHeight = "settings.height";
        const string KeyBgm = "settings.bgm";
        const string KeySfx = "settings.sfx";
        const string KeyMaster = "settings.master";

        public const int VolumeMax = 10;

        static bool loaded, fullscreen;
        static int width, height, master, bgm, sfx;

        /// <summary>음량(마스터·배경음·효과음) 중 하나가 바뀌었다 — 이미 울리고 있는 루프 소리가 따라 바꾼다.</summary>
        public static event System.Action VolumeChanged;

        /// <summary>에디터에서 도메인 리로드 없이 Play를 다시 눌러도 PlayerPrefs를 다시 읽게(Gemini 검토 2026-11-16).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            VolumeChanged = null;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
            width = PlayerPrefs.GetInt(KeyWidth, Screen.width);
            height = PlayerPrefs.GetInt(KeyHeight, Screen.height);
            master = Mathf.Clamp(PlayerPrefs.GetInt(KeyMaster, VolumeMax), 0, VolumeMax);
            bgm = Mathf.Clamp(PlayerPrefs.GetInt(KeyBgm, 8), 0, VolumeMax);
            sfx = Mathf.Clamp(PlayerPrefs.GetInt(KeySfx, 8), 0, VolumeMax);
        }

        public static bool Fullscreen
        {
            get { EnsureLoaded(); return fullscreen; }
            set { EnsureLoaded(); fullscreen = value; PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); ApplyScreen(); }
        }

        /// <summary>전체 음량 0~10(2026-11-16 Figma 새 설정창 "마스터 볼륨") — 효과음·배경음 음량에 곱해진다.</summary>
        public static int MasterVolume
        {
            get { EnsureLoaded(); return master; }
            set => SetVolume(ref master, KeyMaster, value);
        }

        public static int BgmVolume
        {
            get { EnsureLoaded(); return bgm; }
            set => SetVolume(ref bgm, KeyBgm, value);
        }

        public static int SfxVolume
        {
            get { EnsureLoaded(); return sfx; }
            set => SetVolume(ref sfx, KeySfx, value);
        }

        static void SetVolume(ref int field, string key, int value)
        {
            EnsureLoaded();
            int v = Mathf.Clamp(value, 0, VolumeMax);
            if (field == v) return;
            field = v;
            PlayerPrefs.SetInt(key, v);
            VolumeChanged?.Invoke();
        }

        /// <summary>전체 음량 배율 0~1.</summary>
        public static float MasterScale => MasterVolume / (float)VolumeMax;
        /// <summary>효과음 최종 배율 = 효과음 × 마스터(0~1) — 소리를 내는 곳은 이것만 곱한다.</summary>
        public static float SfxScale => SfxVolume / (float)VolumeMax * MasterScale;
        /// <summary>배경음 최종 배율 = 배경음 × 마스터(0~1) — 배경음을 넣으면 이것을 곱한다.</summary>
        public static float BgmScale => BgmVolume / (float)VolumeMax * MasterScale;

        /// <summary>바꾼 값을 디스크에 쓴다(설정창을 닫을 때).</summary>
        public static void Save() => PlayerPrefs.Save();

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
            get { EnsureLoaded(); return new Vector2Int(width, height); }
            set
            {
                EnsureLoaded();
                width = value.x;
                height = value.y;
                PlayerPrefs.SetInt(KeyWidth, value.x);
                PlayerPrefs.SetInt(KeyHeight, value.y);
                ApplyScreen();
            }
        }

        static void ApplyScreen()
        {
            PlayerPrefs.Save();
            if (Application.isEditor) return; // 에디터 Game 창 크기는 에디터가 정한다
            Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
    }
}
