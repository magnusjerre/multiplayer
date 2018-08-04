using UnityEngine;
using System.Collections;

public struct ValidatedPlayerInput 
{
    public int inputSequence;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 speed;

    public ValidatedPlayerInput(int inputSequence, Vector3 position, Quaternion rotation, Vector3 speed)
    {
        this.inputSequence = inputSequence;
        this.position = position;
        this.rotation = rotation;
        this.speed = speed;
    }

	public override string ToString()
	{
        return string.Format("ValidatedPlayerInput(inputSequence: {0}, position: {1}, rotation: {2}, speed: {3})",
                             inputSequence, position, rotation, speed);
	}
}
