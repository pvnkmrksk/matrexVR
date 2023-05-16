using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Keyboard : MonoBehaviour
{
    [SerializeField]
    private float translateSpeed = 10.0f;

    [SerializeField]
    private float rotateSpeed = 10.0f;

    [SerializeField]
    private float maxTranslateSpeed = 100.0f;

    [SerializeField]
    private float maxRotateSpeed = 300.0f;

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update()
    {
        //use the keyboard to move, add a translateSpeed factor
        if (Input.GetKey(KeyCode.UpArrow))
        {
            transform.Translate(Vector3.forward * Time.deltaTime * translateSpeed);
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            transform.Translate(Vector3.back * Time.deltaTime * translateSpeed);
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Translate(Vector3.left * Time.deltaTime * translateSpeed);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            transform.Translate(Vector3.right * Time.deltaTime * translateSpeed);
        }

        //use the keyboard to go up and down using c and z
        if (Input.GetKey(KeyCode.Z))
        {
            transform.Translate(Vector3.down * Time.deltaTime * translateSpeed);
        }
        if (Input.GetKey(KeyCode.C))
        {
            transform.Translate(Vector3.up * Time.deltaTime * translateSpeed);
        }

        //use the keyboard to yaw left right
        if (Input.GetKey(KeyCode.A))
        {
            transform.Rotate(Vector3.up * Time.deltaTime * rotateSpeed);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Rotate(Vector3.down * Time.deltaTime * rotateSpeed);
        }
        //use the keyboard to roll left right
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(Vector3.back * Time.deltaTime * rotateSpeed);
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(Vector3.forward * Time.deltaTime * rotateSpeed);
        }
        //use the keyboard to pitch up down
        if (Input.GetKey(KeyCode.W))
        {
            transform.Rotate(Vector3.right * Time.deltaTime * rotateSpeed);
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.Rotate(Vector3.left * Time.deltaTime * rotateSpeed);
        }

        // shift + up together to increase speed and shift+ down to decrease speed, make sure it doesn't go negative or cross maxspeed

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.UpArrow))
        {
            translateSpeed += 1.0f;
        }
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.DownArrow))
        {
            translateSpeed -= 1.0f;
        }

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.W))
        {
            rotateSpeed += 1.0f;
        }
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.S))
        {
            rotateSpeed -= 1.0f;
        }

        if (translateSpeed > maxTranslateSpeed)
        {
            translateSpeed = maxTranslateSpeed;
        }
        else if (translateSpeed < 0.0f)
        {
            translateSpeed = 0.0f;
        }

        if (rotateSpeed > maxRotateSpeed)
        {
            rotateSpeed = maxRotateSpeed;
        }
        else if (rotateSpeed < 0.0f)
        {
            rotateSpeed = 0.0f;
        }

        // exit the game
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
