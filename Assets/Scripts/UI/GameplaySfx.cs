using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 캐릭터 액션 효과음 — <see cref="SfxEvents"/>(+ 사망은 <see cref="CombatEvents.PlayerDied"/>)를 구독해서
    /// 실제로 재생한다. <see cref="HitSound"/>와 같은 자리(`00_Persistent`의 `Audio`)에 둔다 — 씬에서
    /// 그 오브젝트에 이 컴포넌트만 하나 추가하면 된다(2026-11-10 사운드 적용 스프린트).
    ///
    /// 클립은 `HitSound`처럼 씬에 직렬화해 두지 않고 <see cref="Resources.Load"/>로 이름으로 읽는다
    /// (`Assets/Resources/Audio/Sfx/&lt;키&gt;` — 확장자 없이) — 새 사운드를 받을 때마다 씬을 열어 하나씩
    /// 끌어넣지 않아도 되고, **아직 음원이 없는 키(마법사·메카닉 기본공격, 칼날폭풍)도 미리 걸어 둘 수
    /// 있다**(없으면 `Resources.Load`가 null을 주고 조용히 넘어간다). `VfxFrames`가 쓰는 것과 같은 패턴.
    ///
    /// ## AudioSource 두 개로 나눈 이유(2026-11-10 정정)
    /// 원샷(`source`)과 루프(`loopSource`)를 분리했다 — `AudioSource.pitch`는 `PlayOneShot`과
    /// `.clip`/`.Play()` 루프가 **같은 소스**를 쓰면 서로 간섭한다(원샷 피치를 바꾸는 순간 그때 돌고 있는
    /// 루프 피치까지 같이 틀어진다). 루프가 필요한 효과(레이저 유지음·집중 긴장음)는 전부 `loopSource` 하나를
    /// 같이 쓰는데, **한 번에 한 캐릭터만 조작 가능**이라 레이저(메카닉)와 집중(살수) 루프가 실제로
    /// 겹칠 일이 없다는 전제를 깔고 있다 — 나중에 그 전제가 깨지면(예: 설치기 가동음도 루프로 바뀌면) 이 샘플소스를
    /// 더 늘려야 한다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class GameplaySfx : MonoBehaviour
    {
        const string Root = "Audio/Sfx/";
        const float BaseVolume = 0.8f;

        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        readonly System.Random pitchRandom = new System.Random();

        AudioSource source;     // 원샷 전용
        AudioSource loopSource; // 루프 전용(레이저 유지음 · 집중 긴장음) — 코드로 직접 추가, 씬 연결 불필요
        Coroutine loopStopRoutine;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = false;
        }

        void OnEnable()
        {
            SfxEvents.Jumped += OnJumped;
            SfxEvents.PlayerDamaged += OnPlayerDamaged;
            SfxEvents.BossRoomEntered += OnBossRoomEntered;
            SfxEvents.MageBasicAttack += OnMageBasicAttack;
            SfxEvents.MageFireAttack += OnMageFireAttack;
            SfxEvents.MageFireSkill += OnMageFireSkill;
            SfxEvents.MageGravityAttack += OnMageGravityAttack;
            SfxEvents.MageGravitySkill += OnMageGravitySkill;
            SfxEvents.MechanicBasicAttack += OnMechanicBasicAttack;
            SfxEvents.MechanicLaserActive += OnMechanicLaserActive;
            SfxEvents.MechanicDroneFired += OnMechanicDroneFired;
            SfxEvents.MechanicTurretSet += OnMechanicTurretSet;
            SfxEvents.MechanicTurretOperating += OnMechanicTurretOperating;
            SfxEvents.MechanicSkillReady += OnMechanicSkillReady;
            SfxEvents.AssassinBasicAttack += OnAssassinBasicAttack;
            SfxEvents.AssassinFocusActive += OnAssassinFocusActive;
            SfxEvents.AssassinSpinAttack += OnAssassinSpinAttack;
            CombatEvents.PlayerDied += OnPlayerDied;
        }

        void OnDisable()
        {
            SfxEvents.Jumped -= OnJumped;
            SfxEvents.PlayerDamaged -= OnPlayerDamaged;
            SfxEvents.BossRoomEntered -= OnBossRoomEntered;
            SfxEvents.MageBasicAttack -= OnMageBasicAttack;
            SfxEvents.MageFireAttack -= OnMageFireAttack;
            SfxEvents.MageFireSkill -= OnMageFireSkill;
            SfxEvents.MageGravityAttack -= OnMageGravityAttack;
            SfxEvents.MageGravitySkill -= OnMageGravitySkill;
            SfxEvents.MechanicBasicAttack -= OnMechanicBasicAttack;
            SfxEvents.MechanicLaserActive -= OnMechanicLaserActive;
            SfxEvents.MechanicDroneFired -= OnMechanicDroneFired;
            SfxEvents.MechanicTurretSet -= OnMechanicTurretSet;
            SfxEvents.MechanicTurretOperating -= OnMechanicTurretOperating;
            SfxEvents.MechanicSkillReady -= OnMechanicSkillReady;
            SfxEvents.AssassinBasicAttack -= OnAssassinBasicAttack;
            SfxEvents.AssassinFocusActive -= OnAssassinFocusActive;
            SfxEvents.AssassinSpinAttack -= OnAssassinSpinAttack;
            CombatEvents.PlayerDied -= OnPlayerDied;
            StopLoopImmediate();
        }

        // ── 공통 ──
        void OnJumped(CharacterId id)
        {
            switch (id)
            {
                case CharacterId.Mage: Play("mage_jump"); break;
                case CharacterId.Gunner: Play("mechanic_jump"); break;
                case CharacterId.Blade: Play("assassin_jump"); break;
                // Druid(4번째 슬롯)는 이번 학기 작업 대상이 아니라 아직 전용 점프음이 없다.
            }
        }
        void OnPlayerDamaged() => Play("player_attacked");
        void OnPlayerDied() => Play("maybe_can_use_player_dead");
        void OnBossRoomEntered() => Play("enter_bossroom");

        // ── 마법사 ──
        void OnMageBasicAttack() => Play("mage_basic_attack");   // 음원 없음 — 나중에 추가되면 자동으로 남
        void OnMageFireAttack() => Play("fire_attack");
        void OnMageFireSkill() => Play("fire_skill");
        void OnMageGravityAttack() => Play("gravity_attack");
        void OnMageGravitySkill() => Play("gravity_skill");

        // ── 메카닉 ──
        void OnMechanicBasicAttack() => Play("mechanic_basic_attack"); // 음원 없음 — 레이저 투자 전 + 설치기 빌드 공용(GunnerAttack.Fire() 쪽에서 이미 그 두 경우에만 올린다)

        /// <summary>
        /// 2026-11-10 정정 — 레이저는 한 번이 아니라 누르고 있는 동안 계속 들려야 한다. `loopSource`에
        /// `mechanic_lazor_roop`을 켜고/끄고 한다(On/Off 전이에서만 호출되므로 매 프레임 재시작 걱정 없음).
        /// </summary>
        void OnMechanicLaserActive(bool active) => SetLoop(active ? "mechanic_lazor_roop" : null, 1f);

        void OnMechanicDroneFired() => Play(PickOne("mechanic_drone_bullet1", "mechanic_drone_bullet2", "mechanic_drone_bullet3"));
        void OnMechanicTurretSet() => Play("mechanic_machine_set");
        void OnMechanicTurretOperating() => Play("mechanic_machine_operating"); // 2026-11-10 정정 — 이제 놓는 순간이 아니라 GunnerAttack.UpdateField가 2개 이상 링크된 동안 ~4.5초마다 올린다
        void OnMechanicSkillReady() => Play("both_mechanic_skill_ready");

        // ── 살수 ──
        /// <summary>
        /// 2026-11-10 정정 — 더 이상 랜덤으로 고르지 않는다. <see cref="ChronoSavior.Characters.BladeSlashVfx"/>가
        /// 베기 그림(attack1~3)에 쓰는 것과 같은 번갈이 숫자를 그대로 받아 그 번호 그대로 재생한다.
        /// </summary>
        void OnAssassinBasicAttack(int index)
        {
            int clamped = Mathf.Clamp(index, 1, 3);
            Play("assassin_attack" + clamped, pitchVary: true);
        }

        /// <summary>
        /// 2026-11-10 정정 — slowtime(시작 신호, 원샷)과 heartbeat(긴장감, 루프)로 나눴다. 루프를 더 이상
        /// 고정 시간으로 추측해서 끊지 않는다 — 켜질 때 한 번(slowtime + 루프 시작), 실제 지속시간이 끝나는
        /// 순간(티어별로 다르다, BladeCombat.UpdateFocus) 정확히 꺼지도록 On/Off 신호를 그대로 따른다.
        /// </summary>
        void OnAssassinFocusActive(bool active)
        {
            if (active) Play("assassin_slowtime");
            SetLoop(active ? "assassin_slowtime_heartbeat" : null, 0.6f);
        }
        void OnAssassinSpinAttack() => Play("assassin_spin"); // 칼날폭풍 — 음원 없음

        string PickOne(params string[] keys) => keys[pitchRandom.Next(keys.Length)];

        AudioClip GetClip(string key)
        {
            if (key == null) return null;
            if (!cache.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AudioClip>(Root + key);
                cache[key] = clip; // 없어도(null) 캐시해서 매번 다시 Resources.Load를 시도하지 않는다
            }
            return clip;
        }

        float ScaledVolume(float volumeScale)
        {
            float vol = BaseVolume * volumeScale * GameSettings.SfxVolume / GameSettings.VolumeMax;
            return Mathf.Max(0f, vol);
        }

        void Play(string key, float volumeScale = 1f, bool pitchVary = false)
        {
            var clip = GetClip(key);
            if (clip == null) return;
            float vol = ScaledVolume(volumeScale);
            if (vol <= 0f) return;
            source.pitch = pitchVary ? 0.94f + (float)pitchRandom.NextDouble() * 0.12f : 1f;
            source.PlayOneShot(clip, vol);
        }

        /// <summary>
        /// 루프 채널 켜고/끄기. `key`가 null이면 끈다(레이저 Off). `durationSeconds`를 주면(집중처럼
        /// 지속시간이 정해진 경우) 그 시간 뒤 자동으로 끈다 — 안 주면(레이저처럼 On/Off를 호출부가 직접
        /// 알려주는 경우) 다음 `SetLoop(null, ...)` 호출까지 계속 돈다.
        /// </summary>
        void SetLoop(string key, float volumeScale, float? durationSeconds = null)
        {
            if (loopStopRoutine != null) { StopCoroutine(loopStopRoutine); loopStopRoutine = null; }

            if (key == null) { StopLoopImmediate(); return; }

            var clip = GetClip(key);
            if (clip == null) return;
            float vol = ScaledVolume(volumeScale);
            if (vol <= 0f) return;

            loopSource.pitch = 1f;
            loopSource.clip = clip;
            loopSource.loop = true;
            loopSource.volume = vol;
            loopSource.Play();

            if (durationSeconds.HasValue)
                loopStopRoutine = StartCoroutine(StopLoopAfter(durationSeconds.Value));
        }

        IEnumerator StopLoopAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            loopStopRoutine = null;
            StopLoopImmediate();
        }

        void StopLoopImmediate()
        {
            if (!loopSource.isPlaying && loopSource.clip == null) return;
            loopSource.Stop();
            loopSource.loop = false;
            loopSource.clip = null;
        }
    }
}
