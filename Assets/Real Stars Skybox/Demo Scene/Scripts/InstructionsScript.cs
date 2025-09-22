using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstructionsScript : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void ClickHideButton()
    {
        gameObject.SetActive(false);
    }

    public void ClickShowButton()
    {
        gameObject.SetActive(true);
    }   

}
