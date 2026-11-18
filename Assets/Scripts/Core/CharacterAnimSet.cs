using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 캐릭터 하나의 모든 자세 그림(2026-11-17 애니메이션 플립북 전환) — `Assets/Data/Characters/Anim/AnimSet_&lt;캐릭터&gt;.asset`,
    /// <see cref="CharacterArtSet.animSets"/>에 캐릭터 순서로 연결한다. 에디터 메뉴(`Editor/BuildCharacterFlipbooks`)가 만든다.
    /// 공격은 여러 개일 수 있다(살수가 손을 번갈아 벤다면 0·1) — 번호는 <see cref="AttackAt"/>이 감아서 고른다.
    /// 빠진 자세는 <see cref="idle"/>로 대신한다.
    /// </summary>
    [CreateAssetMenu(menuName = "ChronoSavior/Character Anim Set", fileName = "AnimSet_")]
    public class CharacterAnimSet : ScriptableObject
    {
        public SpriteFlipbook idle, move, rise, fall, charge, hit;
        public SpriteFlipbook[] attacks = new SpriteFlipbook[0];

        /// <summary>공격 묶음 — 번호를 공격 수로 감는다(없으면 null).</summary>
        public SpriteFlipbook AttackAt(int index)
        {
            if (attacks == null || attacks.Length == 0) return null;
            int n = attacks.Length;
            return attacks[((index % n) + n) % n];
        }

        /// <summary>그 자세의 묶음 — 없으면 <see cref="idle"/>.</summary>
        public SpriteFlipbook Get(CharacterPose pose, int attackIndex = 0)
        {
            SpriteFlipbook f = pose switch
            {
                CharacterPose.Move => move,
                CharacterPose.Rise => rise,
                CharacterPose.Fall => fall,
                CharacterPose.Charge => charge,
                CharacterPose.Attack => AttackAt(attackIndex),
                CharacterPose.Hit => hit,
                _ => idle,
            };
            return f != null && f.Count > 0 ? f : idle;
        }
    }
}
