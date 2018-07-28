using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public struct ServerToPlayerMessage
{
    public int latestPlayerSequenceReceived;
    public int serverSequence;
    public ValidatedPlayerInput[] validatedPlayerInputs;

    public ServerToPlayerMessage(int latestPlayerSequenceReceived, int serverSequence, ValidatedPlayerInput[] validatedPlayerInputs)
    {
        this.latestPlayerSequenceReceived = latestPlayerSequenceReceived;
        this.serverSequence = serverSequence;
        this.validatedPlayerInputs = validatedPlayerInputs;
    }

	public override string ToString()
	{
        return string.Format(
            "ServerToPlayerMessage(latestPlayerSequenceReceived: {0}, serverSequence: {1}, validatedPlayerInputsLength: {2})",
            latestPlayerSequenceReceived, 
            serverSequence, 
            validatedPlayerInputs.Length);
	}
}
