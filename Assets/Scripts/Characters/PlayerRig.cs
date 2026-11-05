using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 플레이어 오브젝트에 붙은 캐릭터 키트들 중 **선택된 하나만** 켠다.
    /// 원본 `updatePlayer`가 `meta.weapon`으로 갱신 함수를 갈라 부르는 것(project_test.html:3509~3538)에
    /// 대응한다 — 원본은 함수 분기지만 Unity는 컴포넌트마다 `Update()`가 따로 도니, 안 쓰는 키트를
    /// 꺼주지 않으면 네 캐릭터가 동시에 공격하게 된다.
    ///
    /// **캐릭터를 바꾸는 길은 하나 — 세이브의 `PlayerProfile.character`다**(2026-11-05 2a). 로비 캐릭터 선택은 그 값만 바꾸고 저장하며,
    /// 이 컴포넌트가 <see cref="Update"/>에서 따라간다. 예전엔 로비 화면이 씬에서 이 컴포넌트를 찾아 직접 `Select`했다 — 로비가 전투 씬과
    /// 떨어지면 못 찾고, 길이 둘이라 프로필과 키트가 어긋날 수 있었다.
    ///
    /// 전환 시 하는 일:
    /// 1. 이전 키트 `OnDeselected()` → 비활성화
    /// 2. `PlayerProfile.character` 갱신(파생 스탯의 캐릭터 배수가 이 값을 본다)
    /// 3. 이동 방식을 새 캐릭터에 맞춤(<see cref="ICharacterKit.RequiredMoveMode"/>) + 관성 속도 초기화
    /// 4. 새 키트 활성화 → `OnSelected()`
    ///
    /// 키트를 새로 만들면(섬영·드루이드) 이 오브젝트에 컴포넌트로 붙이기만 하면 자동으로 등록된다 —
    /// 이 파일을 고칠 필요가 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerRig : MonoBehaviour, ISkillSlotHost
    {
        /// <summary>지금 선택된 캐릭터. 초기값은 원본 `defaultMeta().character`(:1134)와 같은 마법사.</summary>
        public CharacterId Current { get; private set; } = CharacterId.Mage;


        [Header("캐릭터 그림")]
        [Tooltip("캐릭터별 그림·덧칠 색(Assets/Data/Characters/CharacterArtSet.asset). 로비 아바타·에디터 도구도 같은 에셋을 본다. " +
                 "비어 있으면 기본 스프라이트를 그대로 쓴다.")]
        [SerializeField] CharacterArtSet art;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)·캡처 도구·테스트가 읽고 넣는다.</summary>
        public CharacterArtSet Art { get => art; set => art = value; }
        [Tooltip("그림 높이(월드 유닛). 콜라이더 지름도 이 값에 맞춰진다 — 기본값은 Core.EntitySizeConfig.")]
        public float spriteHeight = EntitySizeConfig.PlayerHeight;

        readonly List<ICharacterKit> kits = new List<ICharacterKit>();
        CharacterMover2D mover;
        SpriteRenderer sr;

        void Awake()
        {
            mover = GetComponent<CharacterMover2D>();
            sr = GetComponent<SpriteRenderer>();
            // 잔상(`PlayerAfterimages`)·섬영 베기 효과(`BladeSlashVfx`)는 씬의 Player에 붙어 있다(2026-11-05 — 예전엔 씬을 다시
            // 굽지 않으려고 여기서 붙였다). 씬 검사(`Editor/SceneSetupValidator`)가 빠졌는지 본다.
            // 덩치의 단일 출처는 Core.EntitySizeConfig다 — 씬에는 컴포넌트를 붙인 시점의 값(예: 1.0)이
            // 직렬화돼 남아 있어서, 상수만 바꾸면 씬을 다시 굽기 전까지 실제 게임에 반영되지 않았다.
            spriteHeight = EntitySizeConfig.PlayerHeight;
            kits.Clear();
            kits.AddRange(GetComponents<ICharacterKit>());

            // 프로필이 이미 다른 캐릭터를 가리키고 있으면(전환 후 씬 재시작 등) 그쪽을 따른다.
            var wanted = ProfileService.Current.character;
            Current = HasKit(wanted) ? wanted : CharacterId.Mage;
            ApplySelection(Current, notifyPrevious: false);
        }

        // 카메라(`00_Persistent`)는 씬이 달라 이 플레이어를 직렬화로 못 가리킨다 — 플레이어가 스스로 추적 대상으로 등록한다(2026-11-04 씬 분할).
        void OnEnable() => ChronoSavior.World.CameraFollow2D.Track(transform);
        void OnDisable() => ChronoSavior.World.CameraFollow2D.Untrack(transform);

        /// <summary>
        /// 지금 켜져 있는 키트(<see cref="Current"/>의 것). HUD가 여기서 스킬 슬롯 상태(<see cref="ISkillSlotSource"/>)를
        /// 읽는다 — 키트가 없는 캐릭터를 가리키는 순간은 없지만(<see cref="Update"/>가 되돌린다) 방어로 null을 준다.
        /// </summary>
        /// <summary>전투 화면이 읽는 스킬 칸(`Core/ISkillSlotHost`) — 지금 키트가 칸을 내주면 그것(2026-11-05 2b, `Systems/CombatDirector`가 읽는다).</summary>
        public ISkillSlotSource CurrentSkillSlots => CurrentKit as ISkillSlotSource;

        public ICharacterKit CurrentKit
        {
            get
            {
                foreach (var kit in kits)
                    if (kit.Character == Current) return kit;
                return null;
            }
        }

        bool HasKit(CharacterId id)
        {
            foreach (var kit in kits)
                if (kit.Character == id) return true;
            return false;
        }

        /// <summary>
        /// **키트는 항상 프로필의 캐릭터를 따라간다.** 선택 캐릭터의 출처는 세이브(`PlayerProfile.character`)이고, 그 값은 로비 캐릭터 선택이
        /// 바꾸거나 프로필이 통째로 바뀌며(타이틀의 "새 원정" — `GameFlow.StartNewExpedition` → `SaveService.DeleteSave`) 달라진다.
        /// 처음 불러오기는 `GameBoot`가 이 컴포넌트의 Awake보다 먼저 한다. 그래도 바뀌는 순간 키트를 다시 맞추지 않으면
        /// 전문화·캐릭터 탭(프로필을 본다)과 실제 공격(키트)이 서로 다른 캐릭터가 된다(2026-09-28 사용자 발견:
        /// 메카닉을 골랐는데 전문화 탭엔 마법사 트리, 사냥에선 메카닉 공격).
        /// </summary>
        void Update()
        {
            var profile = ProfileService.Current;
            if (profile.character == Current) return;
            if (HasKit(profile.character)) ApplySelection(profile.character, notifyPrevious: true);
            else profile.character = Current; // 키트가 없는 캐릭터(드루이드 등)를 가리키면 지금 키트로 되돌린다
        }

        /// <summary>
        /// 선택된 캐릭터의 그림으로 갈아끼운다. 그림마다 원본 픽셀 크기가 달라서(마법사 81×116,
        /// 메카닉 63×66 …) 그대로 쓰면 캐릭터끼리 덩치가 제각각으로 보인다 — **높이를 콜라이더
        /// 지름에 맞춰 정규화**해서 히트박스와 어긋나 보이지 않게 한다.
        ///
        /// 그림이 없으면(정식 아트 전) 아무것도 안 하고 기본 원형 스프라이트를 그대로 둔다.
        /// </summary>
        void ApplySprite(CharacterId id)
        {
            if (sr == null || art == null) return;
            var sprite = art.SpriteOf(id);
            if (sprite == null) return;

            sr.sprite = sprite;
            // 임시 도형은 색이 없는 흰 도형이라 **여기서 칠해야** 캐릭터가 구분된다.
            // 정식 아트(색이 이미 칠해진 그림)로 바꾸면 그 캐릭터 색을 흰색으로 두면 된다(`CharacterArtSet.tints`).
            sr.color = art.TintOf(id);

            float h = sprite.bounds.size.y;
            if (h <= 0.0001f) return;
            float scale = spriteHeight / h;

            // ⚠️ 콜라이더가 같은 GameObject에 있어서 `localScale`이 물리에도 걸린다 —
            // 그래서 반지름을 역으로 나눠 원하는 월드 반지름을 맞춘다(적 쪽 `EnemySpawner.ApplySize`와 같은 방식).
            //
            // 예전엔 **기존 월드 반지름을 그대로 유지**했는데, 그러면 `spriteHeight`를 키워도
            // 그림만 커지고 히트박스는 그대로라 "보이는 곳을 때렸는데 안 맞는" 상태가 된다.
            // 지금은 그림 높이가 곧 지름이다.
            transform.localScale = new Vector3(scale, scale, 1f);

            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.radius = (spriteHeight / 2f) / scale;
        }

        void ApplySelection(CharacterId id, bool notifyPrevious)
        {
            foreach (var kit in kits)
            {
                bool selected = kit.Character == id;
                var behaviour = kit as MonoBehaviour;

                if (!selected)
                {
                    if (notifyPrevious && kit.Character == Current) kit.OnDeselected();
                    if (behaviour != null) behaviour.enabled = false;
                }
            }

            Current = id;
            ProfileService.Current.character = id;
            ApplySprite(id);

            foreach (var kit in kits)
            {
                if (kit.Character != id) continue;

                // 이동 방식을 먼저 맞추고 관성 속도를 버린 뒤에 키트를 켠다 — 키트가 OnSelected에서
                // MoveScale 같은 값을 세팅할 수 있으므로 순서를 뒤집으면 그게 지워진다.
                if (mover != null)
                {
                    mover.ResetMotion();
                    mover.moveMode = kit.RequiredMoveMode;
                }

                if (kit is MonoBehaviour behaviour) behaviour.enabled = true;
                kit.OnSelected();
                break;
            }
        }
    }
}
