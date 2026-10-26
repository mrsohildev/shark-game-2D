using UnityEngine;

// Ye chhota "messenger" script ball par lagta hai (BubbleShooterAim khud laga deta hai).
// Unity "takra gaya" ki khabar sirf takrane wale object ki script ko deta hai,
// isliye ye ball se takrane ki khabar BubbleShooterAim tak pahunchata hai.
public class BallCollisionRelay : MonoBehaviour
{
    public BubbleShooterAim shooter;

    // Unity ye function khud bulata hai jab ball kisi se takrati hai.
    //void OnCollisionEnter2D(Collision2D collision)
    //{
    //    if (shooter != null)
    //        //shooter.ReflectBall(collision);
    //}
}