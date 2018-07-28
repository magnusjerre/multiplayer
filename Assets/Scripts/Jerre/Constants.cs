using UnityEngine;
using System.Collections;

public class Constants
{
    public const int MAX_PLAYER_INPUT_HISTORY = 30;
    public const int SEND_RATE_PLAYER_INPUT_FROM_CLIENT_TO_SERVER = 30; //Per second
    public const int SEND_RATE_SERVER_TO_CLIENT_PLAYER = 10; //Per second
    public const int FETCH_PLAYER_INPUT_RATE = 30;  //Per second
    public const int MAX_RECEIVED_PLAYER_INPUT_LENGTH = 30;
    public const float MAX_DISCREPANCY_FROM_CLIENT = 0.05f;
    public const int MAX_VALIDATED_PLAYER_INPUTS_ON_SERVER = 10;
    public const int MAX_VALIDATED_PLAYER_INPUTS_ON_CLIENT = 30;
}
