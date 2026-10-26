using System.Collections.Generic;
using UnityEngine;

// =====================================================================
//  BubbleShooterAim  (EASY VERSION 2: mobile + 10 balls)
//  Kaam: kheencho -> aim line dikhao -> chhodo -> ball shoot -> bounce.
//
//  Naya kya hai:
//    - Mobile touch chalta hai (PC par mouse bhi chalta hai)
//    - Ek ke baad ek 10 balls (maxBalls) shoot kar sakte ho,
//      pehli ball ke udte hue bhi
//
//  Hisse:
//    1) SETTINGS : sirf numbers badalne hain
//    2) INPUT    : touch / mouse padhna
//    3) AIM      : kheenchna, angle theek karna, line dikhana
//    4) SHOOT    : ball banana aur chhodna
// =====================================================================
public class BubbleShooterAim : MonoBehaviour
{
    // ---------------------------------------------------------------
    // 1) SETTINGS
    // ---------------------------------------------------------------

    [Header("Inspector mein drag-drop karo")]
    public GameObject ballPrefab;       // ball ka prefab (Rigidbody2D + Circle Collider2D wala)
    public Transform shootingPoint;     // jahan se ball shoot hoti hai
    public LineRenderer aimLine;        // dikhne wali preview line

    [Header("Balls")]
    public int maxBalls = 10;           // total kitni balls shoot kar sakte ho
    public int ballsLeft;               // abhi kitni bachi hain (khud ghat-ti hai)
    public float fireDelay = 0.3f;      // ek shot ke baad agla shot kitni der baad (seconds)
    public float ballLifeTime = 10f;    // ball kitni der baad gayab (0 = kabhi nahi)

    [Header("Kheenchna (pull)")]
    public float maxDragDistance = 5f;  // zyada se zyada kitna kheench sakte ho
    public float minPullToShoot = 0.3f; // isse chhoti pull par shot nahi (galti se tap)
    // true  = jahan ungli rakhi wahin se kheencho (mobile ke liye best)
    // false = shootingPoint se ungli/mouse tak ka fasla pull mana jayega
    public bool dragFromTouchStart = true;

    [Header("Angle limit (degree). 0 = side, 90 = seedha upar")]
    public float minimumAimAngle = 20f; // isse kam angle par shot nahi
    public float maximumAimAngle = 90f; // 90 = koi limit nahi. 75 = seedha-upar mana

    [Header("Speed (force)")]
    public float minLaunchForce = 8f;   // halka kheencho to itni speed
    public float maxLaunchForce = 15f;  // poora kheencho to itni speed

    [Header("Preview line")]
    public int maxReflections = 3;      // line kitni baar bounce dikhaye
    public float rayDistance = 20f;     // line ka ek tukda maximum kitna lamba
    public LayerMask trajectoryMask = ~0; // line kin layers se takrayegi (ball wali layer hata do)
    public float ballRadius = 0.25f;    // prefab mein Circle Collider2D ho to khud wahan se le leta hai


    // ---------------------------------------------------------------
    // Script ki apni yaad-dasht (inhe chhedna nahi hai)
    // ---------------------------------------------------------------
    Camera cam;
    Vector2 aimDirection;       // player ne kis taraf aim kiya
    Vector2 dragStartWorld;     // ungli/mouse pehli baar kahan rakhi thi
    float force;                // is shot ki speed
    float pullAmount;           // abhi kitna kheench rahe ho
    float nextFireTime;         // agla shot kab allowed hai
    bool isDragging;            // abhi kheench rahe ho?
    List<Collider2D> flyingBalls = new List<Collider2D>(); // udti hui balls (aapas mein na takraayein)


    // ---------------------------------------------------------------
    // GAME SHURU HONE PAR
    // ---------------------------------------------------------------
    void Awake()
    {
        // Ball deewar ko chhu rahi ho to line usse "hit" na maane.
        Physics2D.queriesStartInColliders = false;
    }

    void Start()
    {
        cam = Camera.main;
        aimLine.positionCount = 0; // shuru mein line chhupi hui
        ballsLeft = maxBalls;

        // Prefab ke Circle Collider2D se ball ka radius khud nikal lo.
        CircleCollider2D circle = ballPrefab.GetComponent<CircleCollider2D>();
        if (circle != null)
            ballRadius = circle.radius * ballPrefab.transform.localScale.x;
    }


    // ---------------------------------------------------------------
    // 2) INPUT  (touch pehle, nahi to mouse)
    // ---------------------------------------------------------------
    bool PointerDown()
    {
        if (Input.touchCount > 0)
            return Input.GetTouch(0).phase == TouchPhase.Began;
        return Input.GetMouseButtonDown(0);
    }

    bool PointerUp()
    {
        if (Input.touchCount > 0)
        {
            TouchPhase phase = Input.GetTouch(0).phase;
            return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
        }
        return Input.GetMouseButtonUp(0);
    }

    // Ungli ya mouse ki jagah, game ki duniya mein.
    Vector2 PointerWorldPosition()
    {
        Vector2 screenPos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        return cam.ScreenToWorldPoint(screenPos);
    }

    bool CanShoot()
    {
        return ballsLeft > 0 && Time.time >= nextFireTime;
    }


    // ---------------------------------------------------------------
    // 3) AIM  (har frame chalta hai)
    // ---------------------------------------------------------------
    void Update()
    {
        // Touch/click hua aur shot allowed hai -> kheenchna shuru.
        if (PointerDown() && CanShoot())
        {
            isDragging = true;
            dragStartWorld = PointerWorldPosition();
        }

        // Kheench rahe ho -> aim aur line update karo.
        if (isDragging)
            UpdateAim();

        // Ungli/mouse chhodi -> shoot.
        if (PointerUp() && isDragging)
            Shoot();
    }

    void UpdateAim()
    {
        // 1. Kahan se kheencha mana jaye.
        Vector2 origin = dragFromTouchStart ? dragStartWorld : (Vector2)shootingPoint.position;

        // 2. Pull = origin se ungli/mouse tak ka teer.
        Vector2 pull = PointerWorldPosition() - origin;

        // 3. Zyada kheencha to limit par kaat do.
        if (pull.magnitude > maxDragDistance)
            pull = pull.normalized * maxDragDistance;

        pullAmount = pull.magnitude;

        // 4. Bahut chhoti pull -> kuch mat dikhao.
        if (pullAmount < 0.03f)
        {
            aimDirection = Vector2.zero;
            aimLine.positionCount = 0;
            return;
        }

        // 5. Disha = pull ki ULTI taraf (slingshot), phir angle limit lagao.
        aimDirection = FixAngle(-pull.normalized);

        // 6. Jitna zyada kheencha, utni zyada speed.
        float pullPercent = pullAmount / maxDragDistance; // 0 se 1
        force = Mathf.Lerp(minLaunchForce, maxLaunchForce, pullPercent);

        // 7. Preview line banao.
        DrawAimLine();
    }

    // Disha ka angle limit ke andar rakhta hai.
    Vector2 FixAngle(Vector2 dir)
    {
        // Angle nikalo: 0 = bilkul side, 90 = seedha upar.
        float angle = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;

        // Left gaye ya right, ye yaad rakho.
        float side = dir.x >= 0f ? 1f : -1f;

        if (dir.y <= 0f || angle < minimumAimAngle)
            angle = minimumAimAngle;          // bahut neeche/side -> minimum par chipka do
        else if (angle > maximumAimAngle)
            angle = maximumAimAngle;          // bahut steep -> maximum par chipka do
        else
            return dir.normalized;            // sab theek hai

        // Angle se wapas disha banao: (cos, sin).
        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(side * Mathf.Cos(rad), Mathf.Sin(rad));
    }

    // Preview line: ball jaayegi to kahan-kahan takrayegi.
    void DrawAimLine()
    {
        Vector2 pos = shootingPoint.position;
        Vector2 dir = aimDirection;

        aimLine.positionCount = 1;
        aimLine.SetPosition(0, pos);

        for (int i = 0; i < maxReflections; i++)
        {
            // Moti gol ball ko aage dhakelo, dekho pehle kis cheez se takrati hai.
            RaycastHit2D hit = Physics2D.CircleCast(pos, ballRadius, dir, rayDistance, trajectoryMask);

            // Kuch nahi takraya -> line seedhi aage tak kheench do aur ruk jao.
            if (hit.collider == null)
            {
                AddLinePoint(pos + dir * rayDistance);
                break;
            }

            // Takraya -> yahan line ka naya point.
            AddLinePoint(hit.point);

            // Bounce: disha badlo.
            dir = Vector2.Reflect(dir, hit.normal);

            // Agla tukda deewar se thoda bahar se shuru karo.
            pos = hit.point + hit.normal * (ballRadius + 0.05f);
        }
    }

    void AddLinePoint(Vector2 point)
    {
        aimLine.positionCount++;
        aimLine.SetPosition(aimLine.positionCount - 1, point);
    }


    // ---------------------------------------------------------------
    // 4) SHOOT
    // ---------------------------------------------------------------
    void Shoot()
    {
        isDragging = false;
        aimLine.positionCount = 0; // agle shot ke liye line saaf

        // Sahi disha nahi bani, ya pull bahut chhoti thi (galti se tap) -> shot nahi.
        if (aimDirection.sqrMagnitude < 0.001f || pullAmount < minPullToShoot)
            return;

        // Nayi ball banao.
        GameObject ballObject = Instantiate(ballPrefab, shootingPoint.position, Quaternion.identity);

        // Ball par BouncingBall script na ho to laga do.
        BouncingBall newBall = ballObject.GetComponent<BouncingBall>();
        if (newBall == null)
            newBall = ballObject.AddComponent<BouncingBall>();

        // Udti hui doosri balls se takrao mat.
        IgnoreOtherBalls(ballObject.GetComponent<Collider2D>());

        // Chhod do.
        newBall.Launch(aimDirection, force, ballLifeTime);

        ballsLeft--;
        nextFireTime = Time.time + fireDelay;
    }

    void IgnoreOtherBalls(Collider2D newCollider)
    {
        if (newCollider == null) return;

        flyingBalls.RemoveAll(c => c == null); // jo gayab ho gayi unhe list se hatao

        foreach (Collider2D other in flyingBalls)
            Physics2D.IgnoreCollision(newCollider, other);

        flyingBalls.Add(newCollider);
    }


    // ---------------------------------------------------------------
    // Baahar ke scripts / buttons ke liye
    // ---------------------------------------------------------------
    public void RefillBalls()           // balls wapas poori (maxBalls)
    {
        ballsLeft = maxBalls;
    }

    public void AddBalls(int count)     // extra balls do (jaise power-up)
    {
        ballsLeft += count;
    }
}