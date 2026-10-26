using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FishAi : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float acceleration = 8f;       // Kitni jaldi speed badle (kam = smooth, zyada = snappy)

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleLayer;       // Sirf obstacle wali layer (fish ki layer nahi)
    [SerializeField] private float detectDistance = 2f;
    [SerializeField] private float stopDistance = 1f;
    [SerializeField] private float castRadius = 0.3f;

    [Header("Obstacle Change Thresholds")]
    [SerializeField] private float moveThreshold = 0.05f;   // units/sec
    [SerializeField] private float rotateThreshold = 5f;    // degrees/sec
    [SerializeField] private float stateHoldTime = 0.25f;   // Ek baar "moving/rotating" detect hua toh itni der yaad rakho
    [SerializeField] private float lostObstacleGrace = 0.3f; // Obstacle thodi der nahi dikha toh turant bhoolo mat

    private Rigidbody2D rb;
    private Collider2D currentObstacle;
    private Vector2 lastObstaclePos;
    private float lastObstacleAngle;
    private float lastMovingTime = -100f;
    private float lastRotatingTime = -100f;
    private float lostTimer;
    private Vector2 moveDirection;

    // Unity 6 aur purane Unity dono me chale
    private Vector2 Velocity
    {
#if UNITY_6000_0_OR_NEWER
        get { return rb.linearVelocity; }
        set { rb.linearVelocity = value; }
#else
        get { return rb.velocity; }
        set { rb.velocity = value; }
#endif
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;           // Visual jerk kam
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;   // Tunneling/atakna kam
    }

    void Update()
    {
        Vector2 forward = transform.right;

        RaycastHit2D hit = Physics2D.CircleCast(
            transform.position, castRadius, forward, detectDistance, obstacleLayer);

        // Obstacle nahi dikha
        if (hit.collider == null)
        {
            lostTimer += Time.deltaTime;
            if (lostTimer > lostObstacleGrace)
            {
                currentObstacle = null;
                moveDirection = forward;
            }
            // Grace time me pichli direction hi chalne do (flicker nahi hoga)
            return;
        }
        lostTimer = 0f;

        Vector2 obstaclePos = hit.collider.transform.position;
        float obstacleAngle = hit.collider.transform.eulerAngles.z;

        // Naya obstacle -> pehle ruko
        if (hit.collider != currentObstacle)
        {
            currentObstacle = hit.collider;
            lastObstaclePos = obstaclePos;
            lastObstacleAngle = obstacleAngle;
            lastMovingTime = -100f;
            lastRotatingTime = -100f;
            moveDirection = Vector2.zero;
            return;
        }

        float moveSpeed = (obstaclePos - lastObstaclePos).magnitude / Time.deltaTime;
        float rotateSpeed = Mathf.Abs(Mathf.DeltaAngle(lastObstacleAngle, obstacleAngle)) / Time.deltaTime;
        lastObstaclePos = obstaclePos;
        lastObstacleAngle = obstacleAngle;

        if (moveSpeed > moveThreshold) lastMovingTime = Time.time;
        if (rotateSpeed > rotateThreshold) lastRotatingTime = Time.time;

        bool isMoving = Time.time - lastMovingTime < stateHoldTime;
        bool isRotating = Time.time - lastRotatingTime < stateHoldTime;

        if (isMoving)
            moveDirection = -forward;                 // Position badal rahi hai -> piche
        else if (isRotating)
            moveDirection = forward;                  // Sirf rotation -> aage
        else if (hit.distance <= stopDistance)
            moveDirection = Vector2.zero;             // Static aur paas -> ruko
        else
            moveDirection = forward;                  // Static par door -> aao
    }

    void FixedUpdate()
    {
        // Velocity ko dheere dheere target ki taraf le jao (achanak badalne se jerk aata hai)
        Vector2 target = moveDirection * speed;
        Velocity = Vector2.MoveTowards(Velocity, target, acceleration * Time.fixedDeltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 end = transform.position + transform.right * detectDistance;
        Gizmos.DrawLine(transform.position, end);
        Gizmos.DrawWireSphere(end, castRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.right * stopDistance, castRadius);
    }
}