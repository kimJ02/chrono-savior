using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 키(<see cref="VfxKeys"/>) → 이펙트 프레임 — `Resources/Vfx/<키>/`의 그림을 **이름순**(frame_000, frame_001…)으로 한 번 읽어 캐시한다.
    /// 그림이 없으면(팩을 안 가져온 클론) **빈 배열** — 쓰는 쪽은 지금 임시 그림으로 그린다.
    ///
    /// 라이선스 그림이라 git 밖에 두고 씬·프리팹이 GUID로 참조하지 않는다(`HANDOFF.md` 0-3) — 월드 그림을 직렬화로 잇는 원칙
    /// (`docs/asset-swap-points.md`)의 예외이고, UI 그림(`PxArt.Get("키")`)과 같은 모양이다. 씬을 뒤지는 것이 아니라 에셋을 키로 읽는다.
    /// </summary>
    public static class VfxFrames
    {
        public const string Root = "Vfx/";
        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        /// <summary>그 키의 프레임(이름순). 없으면 빈 배열(null이 아니다).</summary>
        public static Sprite[] Get(string key)
        {
            if (cache.TryGetValue(key, out var frames)) return frames;
            frames = Resources.LoadAll<Sprite>(Root + key);
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            cache[key] = frames;
            return frames;
        }

        /// <summary>그 키의 그림이 들어와 있는지.</summary>
        public static bool Has(string key) => Get(key).Length > 0;

        /// <summary>캐시를 비운다 — 에디터에서 팩을 새로 가져온 뒤, 테스트 격리용.</summary>
        public static void ClearCache() => cache.Clear();
    }
}
