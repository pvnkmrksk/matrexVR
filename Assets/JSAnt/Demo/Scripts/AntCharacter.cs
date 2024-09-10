using UnityEngine;
using System.Collections;

public class AntCharacter : MonoBehaviour {
    Animator antAnimator;
    public float forwardSpeed;
    public float turnSpeed;

    void Start()
    {
        antAnimator = GetComponent<Animator>();

    }

    void Update()
    {
        Move();
    }

    public void Attack()
    {
        antAnimator.SetTrigger("Attack");
    }
    public void Hit()
    {
        antAnimator.SetTrigger("Hit");
    }
    public void Death()
    {
        antAnimator.SetBool("IsLived", false);
    }
    public void Rebirth()
    {
        antAnimator.SetBool("IsLived", true);
    }
    public void EatStart()
    {
        antAnimator.SetBool("IsEating", true);
    }
    public void EatEnd()
    {
        antAnimator.SetBool("IsEating", false);
    }



    public void Move()
    {
        antAnimator.SetFloat("Forward", forwardSpeed);
        antAnimator.SetFloat("Turn", turnSpeed);
    }

}
