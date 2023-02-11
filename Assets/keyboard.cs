using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class keyboard : MonoBehaviour
{
    [SerializeField]
    private float translate_speed = 10.0f;
    [SerializeField]
    private float rotate_speed = 10.0f;
    [SerializeField]
    private float max_translate_speed = 100.0f;
    [SerializeField]
    private float max_rotate_speed = 300.0f;

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update()
    {
        
        //use the keyboard to move, add a translate_speed factor
        if (Input.GetKey(KeyCode.UpArrow))
        {
            transform.Translate(Vector3.forward * Time.deltaTime * translate_speed);
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            transform.Translate(Vector3.back * Time.deltaTime * translate_speed);
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Translate(Vector3.left * Time.deltaTime * translate_speed);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            transform.Translate(Vector3.right * Time.deltaTime * translate_speed);
        }

                //use the keyboard to go up and down using c and z
        if (Input.GetKey(KeyCode.C))
        {
            transform.Translate(Vector3.down * Time.deltaTime * translate_speed);
        }
        if (Input.GetKey(KeyCode.Z))
        {
            transform.Translate(Vector3.up * Time.deltaTime * translate_speed);
        }


        //use the keyboard to yaw left right
        if (Input.GetKey(KeyCode.A))
        {
            transform.Rotate(Vector3.up * Time.deltaTime * rotate_speed);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Rotate(Vector3.down * Time.deltaTime * rotate_speed);
        }
        //use the keyboard to roll left right
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(Vector3.back * Time.deltaTime * rotate_speed);
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(Vector3.forward * Time.deltaTime * rotate_speed);
        }
        //use the keyboard to pitch up down
        if (Input.GetKey(KeyCode.W))
        {
            transform.Rotate(Vector3.right * Time.deltaTime * rotate_speed);
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.Rotate(Vector3.left * Time.deltaTime * rotate_speed);
        }


        // shift + up together to increase speed and shift+ down to decrease speed, make sure it doesn't go negative or cross maxspeed

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.UpArrow))
        {
            translate_speed += 1.0f;

        }
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.DownArrow))
        {
            translate_speed -= 1.0f;
        }

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.W))
        {
            rotate_speed += 1.0f;

        }
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.S))
        {
            rotate_speed -= 1.0f;
        }

        
        if (translate_speed > max_translate_speed)
        {
            translate_speed = max_translate_speed;
        }
        else if (translate_speed < 0.0f)
        {
            translate_speed = 0.0f;
        }


        if (rotate_speed > max_rotate_speed)
        {
            rotate_speed = max_rotate_speed;
        }
        else if (rotate_speed < 0.0f)
        {
            rotate_speed = 0.0f;
        }

        // exit the game
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
