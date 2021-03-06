using UnityEngine;

public struct PlayerInput
{
    public int inputSequence;
    public Vector3 position;
    public Vector3 speed;
    public Quaternion rotation;
    public Vector2 moveDir, lookDir;
    public Vector3 MoveDir3D
    {
        get { return new Vector3(moveDir.x, 0, moveDir.y); }
    }
    public Vector3 LookDir3D
    {
        get { return new Vector3(lookDir.x, 0, lookDir.y); }
    }
    public PlayerAction playerAction;

    public bool Boosting { 
        get { return playerAction == PlayerAction.BOOST; }
    }


    public PlayerInput(int inputSequence, Vector3 position, Quaternion rotation, Vector2 moveDir, Vector2 lookDir, PlayerAction playerAction, Vector3 speed)
    {
        this.inputSequence = inputSequence;
        this.position = position;
        this.rotation = rotation;
        this.moveDir = moveDir;
        this.lookDir = lookDir;
        this.playerAction = playerAction;
        this.speed = speed;
    }

    public override string ToString()
    {
        return string.Format(
            "PlayerInput(inputSequence: {0}, position: {1}, rotation: {2}, moveDir: {3}, lookDir: {4}, playerAction: {5}, speed: {6})", 
            inputSequence, position, rotation, moveDir, lookDir, playerAction, speed
        );
    }
}
