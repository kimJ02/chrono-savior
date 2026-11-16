using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 캐릭터 애니메이션 "발판작업" 다음 단계 — `Assets/Resources/Art/Characters/{Shared,Mage,Gunner,Blade}/`의
    /// 프레임(PROGRESS.md 로그 (104)(105), import 설정은 (105)에서 끝냄)으로 **AnimationClip·AnimatorController를
    /// 에디터가 직접 만들게** 한다(손으로 쓴 YAML이 아니라 유니티 API로 생성 — 포맷이 틀릴 위험이 없다).
    ///
    /// 만드는 것:
    /// - 공용 클립 4개(Idle·Move·JumpFall·Hit, `Shared/` 프레임) — Idle·Move·JumpFall은 루프, Hit은 1회.
    /// - 캐릭터별 공격 클립 3개(Mage·Gunner·Blade, 각 폴더의 Attack 프레임만 — 마법사의 기/구체 이펙트는
    ///   몸 그림이 아니라 별도 VFX 레이어 몫이라 여기서 빼고, 기존 `MageChargeVfx`/`MageProjectile` 같은
    ///   패턴으로 나중에 따로 붙인다) — 1회.
    /// - 베이스 `AnimatorController` 하나(Idle/Move/JumpFall/Hit/Attack 상태, 파라미터 Moving·Grounded(bool)·
    ///   Attack·Hit(trigger)) + 캐릭터별 `AnimatorOverrideController` 3개(Attack 클립만 그 캐릭터 것으로 교체).
    ///
    /// **아직 안 하는 것(다음 단계)**: `CharacterArtSet`에 이 오버라이드 컨트롤러를 연결하는 필드, `PlayerRig`·
    /// `LobbyAvatar`가 그 필드를 보고 Animator를 붙이는 런타임 코드, 이동/접지/공격/피격 시점에
    /// Moving·Grounded·Attack·Hit 파라미터를 실제로 세팅하는 코드. 이건 기존 코드(PlayerRig·LobbyAvatar·
    /// MageAttack·GunnerAttack·BladeCombat·PlayerHealth)를 건드리는 작업이라 에셋 생성과 분리해서 다음에 한다.
    ///
    /// **전이(트랜지션) 규칙은 1차 기본값이다** — 실제 느낌은 Animator 창에서 열어 보고 팀이 다듬을 것
    /// (예: Attack 도중 Hit이 끼어들지, JumpFall이 Attack을 끊을지 등 — 지금은 AnyState 우선순위로
    /// Hit·Attack을 Grounded보다 먼저 보게만 해 뒀다).
    ///
    /// 실행: 메뉴 `ChronoSavior/캐릭터 애니메이션 만들기`.
    /// </summary>
    public static class BuildCharacterAnimations
    {
        const string ArtRoot = "Assets/Resources/Art/Characters";
        const string ClipRoot = "Assets/Data/Characters/Animations";
        const string ControllerRoot = "Assets/Data/Characters/Animators";

        const float IdleFps = 8f;
        const float MoveFps = 12f;
        const float JumpFallFps = 10f;
        const float HitFps = 12f;
        const float AttackFps = 12f;

        [MenuItem("ChronoSavior/캐릭터 애니메이션 만들기")]
        public static void Build()
        {
            EnsureFolder(ClipRoot);
            EnsureFolder(ControllerRoot);

            // ── 공용 클립 ──
            var idle = BuildClip("Character_Idle", LoadFrames("Shared", "Character_Idle"), IdleFps, loop: true);
            var move = BuildClip("Character_Move", LoadFrames("Shared", "Character_Move"), MoveFps, loop: true);
            var jumpFall = BuildClip("Character_JumpFall", LoadFrames("Shared", "Character_JumpFall"), JumpFallFps, loop: true);
            var hit = BuildClip("Character_Hit", LoadFrames("Shared", "Character_Hit"), HitFps, loop: false);

            // ── 캐릭터별 공격 클립(몸 그림만 — 이펙트는 별도 VFX 레이어 몫) ──
            var mageAttack = BuildClip("Character_Mage_Attack", LoadFrames("Mage", "Character_Mage_Attack_Body"), AttackFps, loop: false);
            var gunnerAttack = BuildClip("Character_Gunner_Attack", LoadFrames("Gunner", "Character_Gunner_Attack"), AttackFps, loop: false);
            var bladeAttack = BuildClip("Character_Blade_Attack", LoadFrames("Blade", "Character_Blade_Attack"), AttackFps, loop: false);

            if (idle == null || move == null || jumpFall == null || hit == null
                || mageAttack == null || gunnerAttack == null || bladeAttack == null)
            {
                Debug.LogError("[BuildCharacterAnimations] 프레임을 못 찾아서 중단 — 위 경고를 확인할 것.");
                return;
            }

            // ── 베이스 컨트롤러(Attack 상태는 일단 마법사 공격을 기본으로 — 캐릭터별 오버라이드가 실제로 쓸 값으로 바꾼다) ──
            var baseController = BuildBaseController(idle, move, jumpFall, hit, mageAttack);

            // ── 캐릭터별 오버라이드(Attack 클립만 교체) ──
            BuildOverride("CharacterAnim_Mage", baseController, mageAttack, mageAttack);
            BuildOverride("CharacterAnim_Gunner", baseController, mageAttack, gunnerAttack);
            BuildOverride("CharacterAnim_Blade", baseController, mageAttack, bladeAttack);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BuildCharacterAnimations] 클립 7개 + 베이스 컨트롤러 1개 + 캐릭터별 오버라이드 3개(Mage/Gunner/Blade) 완료 → "
                + ClipRoot + ", " + ControllerRoot);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>`Assets/Resources/Art/Characters/{group}/{prefix}_NN.png`를 번호 순서로 불러온다.</summary>
        static List<Sprite> LoadFrames(string group, string prefix)
        {
            string folder = $"{ArtRoot}/{group}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[BuildCharacterAnimations] 폴더 없음: {folder}");
                return null;
            }

            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var frames = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(prefix + "_"))
                .OrderBy(p => Path.GetFileNameWithoutExtension(p), System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s != null)
                .ToList();

            if (frames.Count == 0)
            {
                Debug.LogWarning($"[BuildCharacterAnimations] {folder}에서 '{prefix}_NN' 프레임을 못 찾음.");
                return null;
            }

            return frames;
        }

        static AnimationClip BuildClip(string name, List<Sprite> frames, float fps, bool loop)
        {
            if (frames == null || frames.Count == 0) return null;

            var clip = new AnimationClip { frameRate = fps };
            var binding = new EditorCurveBinding
            {
                path = string.Empty, // Animator가 붙은 오브젝트 자신의 SpriteRenderer
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite",
            };

            var keys = new ObjectReferenceKeyframe[frames.Count];
            for (int i = 0; i < frames.Count; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string path = $"{ClipRoot}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AnimatorController BuildBaseController(AnimationClip idle, AnimationClip move, AnimationClip jumpFall,
            AnimationClip hit, AnimationClip attack)
        {
            string path = $"{ControllerRoot}/CharacterBase.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            // 기본값은 "서서 땅 위" — 셋 다 안 세팅돼도 Idle로 보이게.
            controller.parameters = controller.parameters.Select(p =>
            {
                if (p.name == "Grounded") p.defaultBool = true;
                return p;
            }).ToArray();
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

            var root = controller.layers[0].stateMachine;
            var idleState = root.AddState("Idle");
            idleState.motion = idle;
            var moveState = root.AddState("Move");
            moveState.motion = move;
            var jumpFallState = root.AddState("JumpFall");
            jumpFallState.motion = jumpFall;
            var hitState = root.AddState("Hit");
            hitState.motion = hit;
            var attackState = root.AddState("Attack");
            attackState.motion = attack;

            root.defaultState = idleState;

            // Idle <-> Move (Grounded일 때만 뜻이 있지만, 조건은 단순하게 Moving만 본다 — JumpFall이 Grounded==false로 가로챈다)
            var idleToMove = idleState.AddTransition(moveState);
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0f;
            idleToMove.AddCondition(AnimatorConditionMode.If, 0, "Moving");

            var moveToIdle = moveState.AddTransition(idleState);
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0f;
            moveToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");

            // AnyState -> Hit / Attack (트리거) — Grounded 조건보다 먼저 보게 이 둘을 먼저 추가한다(AnyState는 목록 순서로 평가).
            var toHit = root.AddAnyStateTransition(hitState);
            toHit.hasExitTime = false;
            toHit.duration = 0f;
            toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");

            var toAttack = root.AddAnyStateTransition(attackState);
            toAttack.hasExitTime = false;
            toAttack.duration = 0f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            // AnyState -> JumpFall (Grounded == false)
            var toJumpFall = root.AddAnyStateTransition(jumpFallState);
            toJumpFall.hasExitTime = false;
            toJumpFall.duration = 0f;
            toJumpFall.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");

            // Hit / Attack 끝나면 Idle로(클립이 루프가 아니라 끝(1.0)에서 빠져나간다)
            var hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 1f;
            hitToIdle.duration = 0f;

            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 1f;
            attackToIdle.duration = 0f;

            // JumpFall -> Idle(Grounded == true로 돌아오면). Moving이면 다음 프레임에 Idle->Move가 바로 받는다.
            var jumpFallToIdle = jumpFallState.AddTransition(idleState);
            jumpFallToIdle.hasExitTime = false;
            jumpFallToIdle.duration = 0f;
            jumpFallToIdle.AddCondition(AnimatorConditionMode.If, 0, "Grounded");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void BuildOverride(string name, AnimatorController baseController, AnimationClip baseAttackClip,
            AnimationClip replacementAttackClip)
        {
            string path = $"{ControllerRoot}/{name}.overrideController";
            if (AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path) != null) AssetDatabase.DeleteAsset(path);

            var over = new AnimatorOverrideController(baseController);
            // baseAttackClip(지금은 마법사 Attack 클립)을 그 캐릭터의 공격 클립으로 교체한다. 나머지(Idle/Move/JumpFall/Hit)는
            // 베이스와 같은 클립을 그대로 쓴다 — override 리스트에 안 올리면 베이스 값이 유지된다.
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            over.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i].Key == baseAttackClip)
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(baseAttackClip, replacementAttackClip);
            over.ApplyOverrides(overrides);

            AssetDatabase.CreateAsset(over, path);
        }
    }
}
