using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed;
    public NetworkClient networkClient;
    public bool clientControlled;

    // Update is called once per frame
    void Update()
    {
        if(clientControlled)
        {
            transform.Translate(new Vector3(Input.GetAxis("Horizontal")*speed*Time.deltaTime, Input.GetAxis("Vertical")*speed*Time.deltaTime), 0);
        }
    }
}
