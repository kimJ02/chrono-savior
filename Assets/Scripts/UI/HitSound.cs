using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 적이 타격을 받을 때 나는 소리 — 2026-10-25 사용자가 받은 Kenney Impact Sounds의 `footstep_carpet_002`.
    /// 직접 타격(`CombatEvents.EnemyDamaged`)과 레이저·포탑·궤적 같은 무기 틱(`EnemyTickDamaged` — 화상 틱은 빠짐)에 난다.
    /// 소리 크기는 설정 창의 효과음 값. 섬영·레이저처럼 아주 자주 맞히는 공격에서 소리가 다닥다닥 붙지 않게, 마지막 소리에서
    /// <see cref="MinGap"/>초가 안 지났으면 건너뛴다(사용자 "히트 사운드 최소 간격을 정해서"). 음높이를 조금씩 흔든다.
    /// 판(`PixelCanvas`)에 붙어 게임 씬이 열릴 때 같이 생긴다.
    /// </summary>
    public class HitSound : MonoBehaviour
    {
        public const string ClipPath = "Audio/Sfx/enemy_hit";
        /// <summary>타격 소리 사이 최소 간격(초) — 이보다 가까운 소리는 건너뛴다. 섬영 회전베기(0.13초)마다 한 번쯤 나는 정도.</summary>
        public const float MinGap = 0.12f;
        const float BaseVolume = 0.7f;

        AudioSource source;
        AudioClip clip;
        float lastTime = -10f;

        /// <summary>지금까지 낸 소리 수(테스트용).</summary>
        public int Played { get; private set; }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            clip = Resources.Load<AudioClip>(ClipPath);
        }

        void OnEnable()
        {
            CombatEvents.EnemyDamaged += OnHit;
            CombatEvents.EnemyTickDamaged += OnHit;
        }

        void OnDisable()
        {
            CombatEvents.EnemyDamaged -= OnHit;
            CombatEvents.EnemyTickDamaged -= OnHit;
        }

        void OnHit(GameObject enemy) => Play();

        public void Play()
        {
            float now = Time.unscaledTime; // 히트스톱 중엔 timeScale이 0이다
            if (clip == null || now - lastTime < MinGap) return;
            float vol = BaseVolume * GameSettings.SfxVolume / GameSettings.VolumeMax;
            if (vol <= 0f) return;
            lastTime = now;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.PlayOneShot(clip, vol);
            Played++;
        }
    }
}
