using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class PlayerToServerMessage : MessageBase
{
    public int latestServerSequenceReceived;
    public int playerSequence;
    public PlayerInput[] playerInputs;

    public PlayerToServerMessage() {}

    public PlayerToServerMessage(int latestServerSequenceReceived, int playerSequence, PlayerInput[] playerInputs)
    {
        this.latestServerSequenceReceived = latestServerSequenceReceived;
        this.playerSequence = playerSequence;
        this.playerInputs = playerInputs;
    }

	public override string ToString()
	{
        return string.Format(
            "PlayerToServerMessage(latestServerSequenceReveied: {0}, playerSequence: {1}, playerInputsLength: {2})",
                             latestServerSequenceReceived, 
                             playerSequence, 
                             playerInputs.Length);
	}
}
