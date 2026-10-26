using UnityEngine;

// =====================================================================
//  BouncingBall  (har ball par ek)
//  Kaam: ek ball ki apni disha aur speed yaad rakhna, aur deewar se
//        takrane par khud bounce karna.
//
//  Pehle ek hi ball thi, isliye disha aur speed BubbleShooterAim mein thi.
//  Ab 10 balls ek saath ud sakti hain, isliye har ball apna hisaab khud rakhti hai.
//  (Ye script BallCollisionRelay ki jagah leti hai. Purani wali delete kar do.)
// =====================================================================
[RequireComponent(typeof(Rigidbody2D))]
public class BouncingBall : MonoBehaviour
{
    Rigidbody2D rb;
    Vector2 moveDirection;   // ye ball kis taraf ja rahi hai
    float speed;             // ye ball kitni tez ja rahi hai
    bool launched;           // ball chhoot chuki hai?

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;                                              // gravity band
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;   // deewar ke paar na nikle
    }

    // BubbleShooterAim ye bulata hai jab ball shoot hoti hai.
    public void Launch(Vector2 direction, float launchSpeed, float lifeTime)
    {
        moveDirection = direction.normalized;
        speed = launchSpeed;
        launched = true;

        rb.linearVelocity = moveDirection * speed; // Unity 5/2022 mein: rb.velocity

        // lifeTime seconds baad ball khud gayab (0 = kabhi nahi).
        if (lifeTime > 0f)
            Destroy(gameObject, lifeTime);
    }

    // Har physics step par speed ek jaisi rakho (takrane par physics dheema na kare).
    void FixedUpdate()
    {
        if (!launched) return;

        rb.linearVelocity = moveDirection * speed;
    }

    // Unity ye khud bulata hai jab ball kisi se takrati hai.
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!launched) return;

        Vector2 wallNormal = collision.contacts[0].normal;                      // deewar ka "bahar wala teer"
        moveDirection = Vector2.Reflect(moveDirection, wallNormal).normalized;  // bounce
        rb.linearVelocity = moveDirection * speed;
    }
}