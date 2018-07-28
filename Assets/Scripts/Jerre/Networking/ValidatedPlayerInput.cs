using UnityEngine;
using System.Collections;

public struct ValidatedPlayerInput 
{
    public int inputSequence;
    public Vector3 position;
    public Quaternion rotation;

    public ValidatedPlayerInput(int inputSequence, Vector3 position, Quaternion rotation)
    {
        this.inputSequence = inputSequence;
        this.position = position;
        this.rotation = rotation;
    }

	public override string ToString()
	{
        return string.Format("ValidatedPlayerInput(inputSequence: {0}, position: {1}, rotation: {2})",
                             inputSequence, position, rotation);
	}
}
