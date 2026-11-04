using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 실제 Play Mode에서 Physics2D를 돌려 PlayerAttack이 사거리 안의 Enemy만 파괴하는지 확인한다.
/// Edit Mode에서는 Physics2D 쿼리와 Destroy()의 지연 파괴가 제대로 동작하지 않아
/// (Update 루프가 안 돌아서 다음 프레임 정리가 일어나지 않음) 반드시 PlayMode 테스트로 검증해야 한다.
/// </summary>
public class PlayerAttackTests
{
    [UnityTest]
    public IEnumerator Attack_DestroysOnlyTaggedEnemyInRange()
    {
        var playerGO = new GameObject("Player");
        playerGO.tag = "Player";
        playerGO.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = playerGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        var attack = playerGO.AddComponent<PlayerAttack>();

        var near = NewTagged("EnemyNear", playerGO.transform.position + new Vector3(attack.range * 0.5f, 0, 0), "Enemy");
        var far = NewTagged("EnemyFar", playerGO.transform.position + new Vector3(attack.range * 5f, 0, 0), "Enemy");
        var untagged = NewTagged("UntaggedNear", playerGO.transform.position + new Vector3(attack.range * 0.3f, 0, 0), "Untagged");

        yield return null; // 콜라이더가 물리 월드에 등록되도록 한 프레임 대기

        var attackMethod = typeof(PlayerAttack).GetMethod("Attack", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(attackMethod, "PlayerAttack.Attack 메서드를 리플렉션으로 못 찾음");
        attackMethod.Invoke(attack, null);

        yield return null;

        // 스프린트 2 개정: 이제 즉사가 아니라 피해를 준다. 이 테스트가 원래 검증하던 것
        // (사거리 필터 + 태그 필터)은 그대로, "파괴됐나" 대신 "피해를 입었나"로 확인한다.
        var nearHp = near.GetComponent<ChronoSavior.Enemies.EnemyHealth>();
        var farHp = far.GetComponent<ChronoSavior.Enemies.EnemyHealth>();
        Assert.Less(nearHp.CurrentHp, nearHp.MaxHp, "사거리 안의 Enemy가 피해를 안 입었다");
        Assert.AreEqual(farHp.MaxHp, farHp.CurrentHp, 0.001f, "사거리 밖의 Enemy가 피해를 입었다(오탐)");
        var untaggedHp = untagged.GetComponent<ChronoSavior.Enemies.EnemyHealth>();
        Assert.AreEqual(untaggedHp.MaxHp, untaggedHp.CurrentHp, 0.001f,
            "Enemy 태그가 아닌데 사거리 안이라고 피해를 입었다(태그 필터 실패)");

        Object.Destroy(near);
        Object.Destroy(far);
        Object.Destroy(untagged);
        Object.Destroy(playerGO);
        yield return null;
    }

    /// <summary>
    /// 공격에 아무 시각 표시가 없어서 "눌러도 안 보인다"는 피드백으로 추가된 판정링(LineRenderer)이
    /// 공격 순간 켜지고, flashDuration이 지나면 다시 꺼지는지 확인한다.
    /// </summary>
    [UnityTest]
    public IEnumerator Attack_ShowsFlashRing_ThenHidesAfterDuration()
    {
        var playerGO = new GameObject("Player");
        playerGO.tag = "Player";
        playerGO.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = playerGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        var attack = playerGO.AddComponent<PlayerAttack>();
        attack.flashDuration = 0.05f; // 테스트를 빠르게 끝내려고 짧게

        var ring = playerGO.GetComponent<LineRenderer>();
        Assert.IsNotNull(ring, "PlayerAttack이 LineRenderer를 안 만듦");
        Assert.IsFalse(ring.enabled, "시작 전부터 링이 켜져 있음");

        var attackMethod = typeof(PlayerAttack).GetMethod("Attack", BindingFlags.NonPublic | BindingFlags.Instance);
        attackMethod.Invoke(attack, null);

        Assert.IsTrue(ring.enabled, "공격 직후 링이 안 켜짐");

        yield return new WaitForSeconds(0.2f); // flashDuration(0.05초)보다 넉넉히 대기

        Assert.IsFalse(ring.enabled, "flashDuration이 지났는데도 링이 안 꺼짐");

        Object.Destroy(playerGO);
        yield return null;
    }

    [Test]
    public void FieldBounds_ClampX_KeepsXInsideBounds()
    {
        Assert.AreEqual(FieldBounds.MaxX, FieldBounds.ClampX(999f), 0.001f);
        Assert.AreEqual(FieldBounds.MinX, FieldBounds.ClampX(-999f), 0.001f);
    }

    /// <summary>
    /// KeyCode.C 키 입력 자체는 시뮬레이트할 수 없어서(Input은 실제 키보드만 읽음),
    /// Rigidbody2D.linearVelocity에 "방금 점프 시작" 속도를 직접 주입하고 그 이후 물리 스텝은
    /// 실제 Physics2D + CharacterMover2D.FixedUpdate가 그대로 돌리게 한다 — 중력·착지 판정 모두
    /// 실제 엔진 경로를 태운다(공식을 베껴 쓰지 않는다). CharacterMover2D가 자체 중력 계산에서
    /// 진짜 Physics2D로 바뀌면서 이 테스트도 Ground 레이어 바닥 콜라이더를 직접 세팅해야 한다.
    /// </summary>
    [UnityTest]
    public IEnumerator Jump_RisesThenReturnsToGround()
    {
        var groundGO = new GameObject("TestGround");
        groundGO.layer = LayerMask.NameToLayer("Ground");
        groundGO.AddComponent<BoxCollider2D>().size = new Vector2(20f, 0.3f);
        groundGO.transform.position = new Vector3(2.2f, FieldBounds.GroundY - 0.15f, 0f);

        var go = new GameObject("JumpTestPlayer");
        go.tag = "Player";
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        go.transform.position = new Vector3(2.2f, FieldBounds.GroundY + 0.5f, 0f);
        var mover = go.AddComponent<CharacterMover2D>();

        var groundedField = typeof(CharacterMover2D).GetField("grounded", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(groundedField);

        yield return new WaitForFixedUpdate(); // 착지해서 grounded=true가 될 시간을 준다
        yield return new WaitForFixedUpdate();

        rb.linearVelocity = new Vector2(0f, mover.jumpSpeed);

        float maxY = go.transform.position.y;
        float minYAfterPeak = float.MaxValue;
        bool pastPeak = false;
        float t = 0f;
        while (t < 3f)
        {
            yield return new WaitForFixedUpdate();
            float y = go.transform.position.y;
            if (y > maxY) maxY = y;
            else pastPeak = true;
            if (pastPeak) minYAfterPeak = Mathf.Min(minYAfterPeak, y);
            t += Time.fixedDeltaTime;
            if (pastPeak && (bool)groundedField.GetValue(mover) && t > 0.1f) break;
        }

        // 콜라이더 반지름(0.5)만큼 중심이 바닥 위에 떠서 정지한다 — transform.position은 원의 중심이지 발밑이 아니다.
        float restY = FieldBounds.GroundY + 0.5f;
        Assert.Greater(maxY, restY + 0.05f, "점프가 바닥 위로 올라가지 않았다");
        Assert.AreEqual(restY, go.transform.position.y, 0.05f, "점프 후 바닥으로 복귀하지 않았다");

        Object.Destroy(go);
        Object.Destroy(groundGO);
        yield return null;
    }

    /// <summary>
    /// 스프린트 2부터 공격은 대상을 즉시 파괴하지 않고 `IDamageable.TakeDamage()`를 부른다.
    /// 그래서 피격 여부를 확인하려면 대상에 `EnemyHealth`가 붙어 있어야 한다.
    /// </summary>
    static GameObject NewTagged(string name, Vector3 pos, string tag, bool withHealth = true)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.tag = tag;
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        if (withHealth)
        {
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<ChronoSavior.Enemies.EnemyHealth>();
        }
        return go;
    }
}

}
