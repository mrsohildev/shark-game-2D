using UnityEngine;

// Attach this to the Ball GameObject (same object as its Rigidbody2D/Collider2D).
// Unity only calls OnCollisionEnter2D on a script sitting on the colliding
// object, so without this relay, BubbleShooterAim.ReflectBall() is never
// invoked and the ball just passes through walls without bouncing.
public class BallCollisionRelay : MonoBehaviour
{
    public BubbleShooterAim shooter;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (shooter != null)
            shooter.ReflectBall(collision);
    }
}