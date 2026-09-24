using UnityEngine;

public class BubbleShooterAim : MonoBehaviour
{
    public Rigidbody2D ball;
    public Transform shootingPoint;
    public LineRenderer aimLine;

    [Header("Aim")]
    public float maxDragDistance = 5f;
    public float minimumAimAngle = 20f;
    // Caps how steep/vertical the shot can go. 90 = straight up allowed,
    // lower values (e.g. 75) stop it from firing too close to vertical.
    public float maximumAimAngle = 160f;

    [Header("Ball")]
    // Force scales with how far the player pulls back (like a slingshot).
    // At full maxDragDistance, launch force = maxLaunchForce.
    public float minLaunchForce = 8f;
    public float maxLaunchForce = 15f;

    [Header("Trajectory")]
    public int maxReflections = 3;
    public float rayDistance = 20f;
    // Assign this in the Inspector to EXCLUDE the ball's own layer,
    // otherwise the first raycast can hit the ball itself.
    public LayerMask trajectoryMask = ~0;

    [Header("Ball Radius (for accurate preview)")]
    // Should match the Circle Collider2D radius on the ball.
    // Using a plain Raycast (zero width) instead of the ball's actual
    // size is the #1 cause of the preview line and real ball not
    // matching at the bounce point.
    public float ballRadius = 0.25f;

    private Camera cam;
    private Vector2 aimDirection;
    private Vector2 velocityDirection;
    private float currentLaunchForce;
    private bool isDragging;
    private bool ballLaunched;

    void Awake()
    {
        // Prevents CircleCast from treating the ball's own overlap with
        // the wall it just bounced off as a fresh "hit" at distance 0,
        // which was breaking reflections after adding CircleCast.
        Physics2D.queriesStartInColliders = false;

        // Bubble-shooter aim assumes straight-line travel.
        // If gravity is on, the real ball will arc away from the preview.
        if (ball != null)
        {
            ball.gravityScale = 0f;
            // Prevents fast-moving balls from tunneling through thin
            // colliders between physics steps (missed collisions).
            ball.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    void Start()
    {
        cam = Camera.main;
        aimLine.positionCount = 0;

        // Auto-wire the relay component so the ball actually reports
        // collisions back to this script. See BallCollisionRelay.cs.
        if (ball != null)
        {
            BallCollisionRelay relay = ball.GetComponent<BallCollisionRelay>();
            if (relay == null)
                relay = ball.gameObject.AddComponent<BallCollisionRelay>();
            relay.shooter = this;
        }
    }

    void Update()
    {
        if (ballLaunched)
            return;

        Vector2 pointerPosition = cam.ScreenToWorldPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
        }

        if (isDragging)
        {
            Vector2 pull = pointerPosition - (Vector2)shootingPoint.position;

            if (pull.magnitude > maxDragDistance)
            {
                pull = pull.normalized * maxDragDistance;
            }

            if (pull.sqrMagnitude > 0.001f)
            {
                Vector2 desiredDirection = -pull.normalized;
                aimDirection = ClampAimDirection(desiredDirection);

                // Stronger pull = stronger shot, mapped between
                // minLaunchForce and maxLaunchForce.
                float pullFraction = pull.magnitude / maxDragDistance;
                currentLaunchForce = Mathf.Lerp(minLaunchForce, maxLaunchForce, pullFraction);

                ShowTrajectory(shootingPoint.position, aimDirection);
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            LaunchBall();
        }
    }

    Vector2 ClampAimDirection(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
        float xDirection = direction.x >= 0f ? 1f : -1f;

        // Ball is aimed too low / sideways / downward — snap up to the
        // minimum allowed angle.
        if (direction.y <= 0f || angle < minimumAimAngle)
        {
            float minAngleRadians = minimumAimAngle * Mathf.Deg2Rad;
            return new Vector2(
                xDirection * Mathf.Cos(minAngleRadians),
                Mathf.Sin(minAngleRadians)
            ).normalized;
        }

        // Ball is aimed too steep/vertical — clamp down to the maximum
        // allowed angle so it can't fire straight up (or past your limit).
        if (angle > maximumAimAngle)
        {
            float maxAngleRadians = maximumAimAngle * Mathf.Deg2Rad;
            return new Vector2(
                xDirection * Mathf.Cos(maxAngleRadians),
                Mathf.Sin(maxAngleRadians)
            ).normalized;
        }

        return direction.normalized;
    }

    void ShowTrajectory(Vector2 startPosition, Vector2 direction)
    {
        Vector2 currentPosition = startPosition;
        Vector2 currentDirection = direction;

        aimLine.positionCount = 1;
        aimLine.SetPosition(0, currentPosition);

        for (int i = 0; i < maxReflections; i++)
        {
            // CircleCast (not Raycast) so the predicted bounce point
            // matches where the ball's actual surface touches the wall,
            // not where a zero-width line would touch it.
            RaycastHit2D hit = Physics2D.CircleCast(
                currentPosition,
                ballRadius,
                currentDirection,
                rayDistance,
                trajectoryMask
            );

            if (hit.collider == null)
            {
                aimLine.positionCount++;
                aimLine.SetPosition(
                    aimLine.positionCount - 1,
                    currentPosition + currentDirection * rayDistance
                );
                break;
            }

            aimLine.positionCount++;
            aimLine.SetPosition(aimLine.positionCount - 1, hit.point);

            currentDirection = Vector2.Reflect(currentDirection, hit.normal);
            // Push the next cast's start point clear of the wall by the
            // ball's full radius (not just a tiny epsilon) — with
            // CircleCast, a small offset still overlaps the wall and
            // causes the next cast to falsely "hit" the same surface,
            // which stops the line from reflecting further.
            currentPosition = hit.point + hit.normal * (ballRadius + 0.05f);
        }
    }

    void LaunchBall()
    {
        isDragging = false;

        if (aimDirection.sqrMagnitude < 0.001f)
            return;

        ball.position = shootingPoint.position;
        velocityDirection = aimDirection;
        ball.linearVelocity = velocityDirection * currentLaunchForce;
        ballLaunched = true;

        //aimLine.positionCount = 0;
    }

    void FixedUpdate()
    {
        if (!ballLaunched)
            return;

        // Enforce our own tracked direction every physics step, instead
        // of reading ball.linearVelocity back. The physics solver can
        // damp/zero the velocity on contact (especially with 0
        // bounciness) before our reflection code ever runs, which was
        // causing the ball to stall instead of bounce. Our own
        // velocityDirection is never touched by the solver, so this
        // keeps the ball moving exactly along the reflected path.
        ball.linearVelocity = velocityDirection * currentLaunchForce;
    }

    // Called by BallCollisionRelay when the ball's own collider hits something.
    public void ReflectBall(Collision2D collision)
    {
        if (!ballLaunched)
            return;

        Vector2 normal = collision.contacts[0].normal;
        velocityDirection = Vector2.Reflect(velocityDirection, normal).normalized;

        ball.linearVelocity = velocityDirection * currentLaunchForce;
    }

    public void ResetBall()
    {
        ball.linearVelocity = Vector2.zero;
        ball.angularVelocity = 0f;
        ball.position = shootingPoint.position;

        aimDirection = Vector2.zero;
        velocityDirection = Vector2.zero;
        isDragging = false;
        ballLaunched = false;

        aimLine.positionCount = 0;
    }
}