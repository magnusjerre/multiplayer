using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class Rocket : NetworkBehaviour
{

    //[SyncVar]
    public int ownerConnectionId;

    private  Color ownerColor = Color.green, enemyColor = Color.yellow;

    public float lifetime = 2f, speed = 3f;

	public override void OnStartClient()
	{
        //base.OnStartClient();
        //if (ownerConnectionId == connectionToServer.connectionId)
        //{
        //    GetComponent<MeshRenderer>().material.color = ownerColor;
        //}
        //else 
        //{
        //    GetComponent<MeshRenderer>().material.color = enemyColor;
        //}
	}

	public override void OnStartServer()
	{
        //base.OnStartServer();
        //if (ownerConnectionId == connectionToClient.connectionId)
        //{
        //    GetComponent<MeshRenderer>().material.color = ownerColor;
        //}
        //else
        //{
        //    GetComponent<MeshRenderer>().material.color = enemyColor;
        //}
        Destroy(gameObject, lifetime);
	}



	// Use this for initialization
	void Start()
	{
	}

	// Update is called once per frame
	void Update()
	{
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
	}

	public void OnTriggerEnter(Collider other)
	{
        Debug.Log("Trigger enter");
        if (hasAuthority) {
            Destroy(gameObject);
        }
	}
}
