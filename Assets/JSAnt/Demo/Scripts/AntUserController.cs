using UnityEngine;
using System.Collections;

public class AntUserController : MonoBehaviour {
    AntCharacter antCharacter;

    void Start()
    {
        antCharacter = GetComponent<AntCharacter>();
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            antCharacter.Attack();
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            antCharacter.Hit();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            antCharacter.Death();
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            antCharacter.Rebirth();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            antCharacter.EatStart();
        }
        if (Input.GetKeyUp(KeyCode.E))
        {
            antCharacter.EatEnd();
        }

        antCharacter.forwardSpeed = 1f;//Input.GetAxis("Vertical");
        antCharacter.turnSpeed = Input.GetAxis("Horizontal");
    }
}
