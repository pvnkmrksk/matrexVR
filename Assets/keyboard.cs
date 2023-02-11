using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class keyboard : MonoBehaviour
{
    [SerializeField]
    private float translate_speed = 10.0f;
    [SerializeField]
    private float rotate_speed = 10.0f;

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


        // exit the game
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
