using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 2026-08-26 "빠짐없이 원본과 대조" 감사에서 새로 발견해 이식한 디테일들을 검증한다:
/// 코요테 타임, 점프 입력 버퍼, 차지 중 이동속도 50% 감소, 낙하 종단속도, 필드 경계 여백.
/// Input.GetKeyDown은 실제 키 이벤트 큐에 의존해 테스트에서 시뮬레이트할 수 없으므로,
/// CharacterMover2D/EnemyMove의 private 타이머 필드를 리플렉션으로 직접 주입해서
/// "방금 이런 입력이 있었다"는 상태만 만들고, 그 이후 판정/물리는 실제 Update/FixedUpdate가 돈다.
/// </summary>
public class MovementFidelityTests
{
    static GameObject NewGroundedPlayer(Vector3 pos)
    {
        var go = new GameObject("TestPlayer");
        go.tag = "Player";
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        go.AddComponent<Rigidbody2D>();
        go.transform.position = pos;
        go.AddComponent<CharacterMover2D>();
        return go;
    }

    /// <summary>
    /// 코요테 타임: 발판을 막 벗어나(grounded=false) 있어도 그 직후(0.10초 안)엔 점프가 나가야 한다.
    /// 실제로 바닥에 붙어있는 상태에서 grounded만 리플렉션으로 false를 주입하면, 그사이 실제
    /// FixedUpdate의 CheckGrounded()가 다시 true로 덮어써서 레이스가 생길 수 있어 — 아예 바닥/발판이
    /// 없는 허공에 캐릭터를 둬서 CheckGrounded()가 항상 진짜로 false를 내게 만들었다.
    /// </summary>
    [UnityTest]
    public IEnumerator CoyoteTime_AllowsJump_ShortlyAfterLeavingGround()
    {
        var go = NewGroundedPlayer(new Vector3(2.2f, 30f, 0f)); // 근처에 바닥/발판이 전혀 없는 허공
        var mover = go.GetComponent<CharacterMover2D>();
        var coyoteField = typeof(CharacterMover2D).GetField("coyoteTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var bufferField = typeof(CharacterMover2D).GetField("jumpBufferTimer", BindingFlags.NonPublic | BindingFlags.Instance);

        // "방금 발판을 벗어났다"를 시뮬레이트: 코요테 창은 아직 살아있음, 점프 입력도 막 들어옴.
        coyoteField.SetValue(mover, 0.05f);
        bufferField.SetValue(mover, 0.13f);

        yield return null; // Update() 한 프레임 — 이 안에서 점프가 나가야 함

        float vy = go.GetComponent<Rigidbody2D>().linearVelocity.y;
        Assert.AreEqual(mover.jumpSpeed, vy, 0.5f, "코요테 타임 안인데 점프가 안 나갔다(공중에서 점프 속도가 안 붙음)");

        Object.Destroy(go);
        yield return null;
    }

    /// <summary>코요테 타임이 지난 뒤엔(0.10초보다 오래 공중에 있었으면) 점프가 안 나가야 한다 — 상한 확인.</summary>
    [UnityTest]
    public IEnumerator CoyoteTime_DoesNotAllowJump_AfterWindowExpires()
    {
        var go = NewGroundedPlayer(new Vector3(2.2f, FieldBounds.GroundY + 3f, 0f)); // 공중에서 시작
        var mover = go.GetComponent<CharacterMover2D>();
        var groundedField = typeof(CharacterMover2D).GetField("grounded", BindingFlags.NonPublic | BindingFlags.Instance);
        var coyoteField = typeof(CharacterMover2D).GetField("coyoteTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var bufferField = typeof(CharacterMover2D).GetField("jumpBufferTimer", BindingFlags.NonPublic | BindingFlags.Instance);

        groundedField.SetValue(mover, false);
        coyoteField.SetValue(mover, -1f); // 코요테 창이 이미 끝난 상태
        bufferField.SetValue(mover, 0.13f);

        yield return null;

        float vy = go.GetComponent<Rigidbody2D>().linearVelocity.y;
        Assert.Less(vy, mover.jumpSpeed - 0.5f, "코요테 타임이 끝났는데도 점프가 나갔다(공중에서 무제한 점프가 되는 버그)");

        Object.Destroy(go);
        yield return null;
    }

    /// <summary>
    /// 마법사 차지 중엔 이동속도가 원본 그대로 절반이 돼야 한다(`chargeSlow=0.5`).
    /// Input은 시뮬레이트 못 하므로 MageAttack의 private charging 플래그를 리플렉션으로 켜고
    /// SpeedMultiplier가 실제로 반영되는지 확인한다.
    /// </summary>
    [UnityTest]
    public IEnumerator MageAttack_Charging_HalvesMoveSpeed()
    {
        var go = NewGroundedPlayer(new Vector3(2.2f, FieldBounds.GroundY + 0.5f, 0f));
        var mage = go.AddComponent<MageAttack>();
        var mover = go.GetComponent<CharacterMover2D>();
        var chargingField = typeof(MageAttack).GetField("charging", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(chargingField);

        yield return null;
        Assert.AreEqual(1f, mover.SpeedMultiplier, 0.001f, "차지 시작 전인데 이미 감속돼 있다");

        chargingField.SetValue(mage, true);
        yield return null; // MageAttack.Update()가 SpeedMultiplier를 갱신할 시간

        Assert.AreEqual(mage.chargeMoveSlow, mover.SpeedMultiplier, 0.001f, "차지 중인데 이동속도가 원본 배율(0.5)로 안 줄었다");

        Object.Destroy(go);
        yield return null;
    }

    /// <summary>
    /// 낙하 종단속도(15유닛/s) 상한 확인. 높은 곳에서 오래 떨어지면 실제 물리 중력(26유닛/s²)만으로는
    /// 이론상 이 값을 넘어서므로(최상단 발판에서 떨어지면 ≈16.8유닛/s), 상한이 실제로 걸리는지 확인.
    /// </summary>
    [UnityTest]
    public IEnumerator TerminalFallSpeed_CapsDownwardVelocity()
    {
        var go = NewGroundedPlayer(new Vector3(2.2f, 30f, 0f)); // 충분히 높은 곳(발판 훨씬 위)에서 낙하 시작
        var mover = go.GetComponent<CharacterMover2D>();

        float maxDownwardSpeed = 0f;
        float t = 0f;
        while (t < 1.5f) // 종단속도에 도달하기 충분한 시간(바닥엔 안 닿는 높이)
        {
            yield return new WaitForFixedUpdate();
            maxDownwardSpeed = Mathf.Max(maxDownwardSpeed, -go.GetComponent<Rigidbody2D>().linearVelocity.y);
            t += Time.fixedDeltaTime;
        }

        // 물리 엔진이 매 스텝 중력을 적용한 "직후"에 값을 읽기 때문에, 다음 FixedUpdate에서 클램프가
        // 걸리기 전까지 한 스텝만큼(중력×fixedDeltaTime ≈ 26×0.02 = 0.52유닛/s) 일시적으로 넘을 수
        // 있다 — 실제 게임에서 감지 불가능한 서브프레임 오차라 허용치에 반영(0.7로 여유 있게).
        Assert.LessOrEqual(maxDownwardSpeed, mover.terminalFallSpeed + 0.7f, "낙하 속도가 종단속도 상한을 훨씬 넘었다");
        Assert.Greater(maxDownwardSpeed, mover.terminalFallSpeed - 0.5f, "종단속도 근처까지 가속이 안 됐다 — 테스트 설정(높이/시간) 재검토 필요");

        Object.Destroy(go);
        yield return null;
    }

    /// <summary>필드 X 경계 여백(원본 24px→0.24유닛): 캐릭터가 경계에 딱 붙지 않고 여백 안에서 멈춰야 한다.</summary>
    [UnityTest]
    public IEnumerator EdgeMargin_StopsPlayerBeforeFieldBoundary()
    {
        var go = NewGroundedPlayer(new Vector3(FieldBounds.MinX + 0.05f, FieldBounds.GroundY + 0.5f, 0f));
        var mover = go.GetComponent<CharacterMover2D>();

        // 왼쪽 경계를 향해 계속 미는 상태를 시뮬레이트하기 위해, 여러 프레임 동안 rb 속도를 직접 왼쪽으로 유지
        var rb = go.GetComponent<Rigidbody2D>();
        float t = 0f;
        while (t < 0.5f)
        {
            rb.position = new Vector2(Mathf.Max(FieldBounds.MinX - 1f, rb.position.x - 0.05f), rb.position.y);
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }

        Assert.GreaterOrEqual(go.transform.position.x, FieldBounds.MinX + mover.edgeMargin - 0.01f, "여백 없이 필드 경계에 딱 붙었다(원본은 24px 여백을 둠)");

        Object.Destroy(go);
        yield return null;
    }
}

}
