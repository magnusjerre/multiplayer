using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using Jerre.Networking;

public class Pickup : NetworkBehaviour
{

    public Material rocketMaterial, bombMaterial;

    [SyncVar(hook = "ChangeMaterial")]
    public PickupEnum pickup = PickupEnum.ROCKET;

    public float rotationSpeed = 180f;

	// Use this for initialization
	void Start()
	{
        if (pickup == PickupEnum.ROCKET)
        {
            GetComponent<MeshRenderer>().material = rocketMaterial;
        }
        else if (pickup == PickupEnum.BOMB)
        {
            GetComponent<MeshRenderer>().material = bombMaterial;
        }
	}

	// Update is called once per frame
	void Update()
	{
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
	}

	public void OnTriggerEnter(Collider other)
	{
        if (hasAuthority)
        {
            var player = other.GetComponent<MJPlayerController>();
            if (player != null)
            {
                player.CmdChangePickup(pickup);
                var newPickup = pickup == PickupEnum.BOMB ? PickupEnum.ROCKET : PickupEnum.BOMB;
                this.pickup = newPickup;
                ChangeMaterial(pickup);
            }
        }
	}

    public void ChangeMaterial(PickupEnum pickup) {
        this.pickup = pickup;
        if (pickup == PickupEnum.ROCKET) {
            GetComponent<MeshRenderer>().material = rocketMaterial;
        } else if (pickup == PickupEnum.BOMB) {
            GetComponent<MeshRenderer>().material = bombMaterial;
        }
    }
}
