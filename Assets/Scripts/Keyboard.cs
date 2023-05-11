using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Keyboard : MonoBehaviour
{
    [SerializeField]
    private float translateSpeed = 10.0f;
    [SerializeField]
    private float rotateSpeed = 10.0f;

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update()
    {
        //use the keyboard to move, add a translateSpeed factor
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * Time.deltaTime * translateSpeed);
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(Vector3.back * Time.deltaTime * translateSpeed);
        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(Vector3.left * Time.deltaTime * translateSpeed);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(Vector3.right * Time.deltaTime * translateSpeed);
        }

        // Mouse rotation
        float h = rotateSpeed * Input.GetAxis("Mouse X");

        transform.Rotate(0, h, 0);

        // exit the game
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
