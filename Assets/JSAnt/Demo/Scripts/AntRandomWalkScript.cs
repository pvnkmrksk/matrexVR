using UnityEngine;
using System.Collections;

public class AntRandomWalkScript : MonoBehaviour {
    AntCharacter antCharacter;

    float forwardSpeed = 10f;
    float turnSpeed = 0f;
    float nextForwardSpeed = 0f;
    float nextTurnSpeed = 0f;
    float changeTime = 0f;

    // Use this for initialization
    void Start()
    {
        antCharacter = GetComponent<AntCharacter>();

        changeTime = Random.Range(0f, 5f);
    }

    // Update is called once per frame
    void Update()
    {
        forwardSpeed = Mathf.Lerp(forwardSpeed, nextForwardSpeed, Time.deltaTime);
        turnSpeed = Mathf.Lerp(turnSpeed, nextTurnSpeed, Time.deltaTime);

        changeTime -= Time.deltaTime;
        if (changeTime < 0f)
        {
            changeTime = Random.Range(0f, 5f);
            nextForwardSpeed = Random.Range(-1f, 1f);
            nextTurnSpeed = Random.Range(-1f, 1f);
        }

        antCharacter.forwardSpeed = forwardSpeed;
        antCharacter.turnSpeed = turnSpeed;
    }
}
