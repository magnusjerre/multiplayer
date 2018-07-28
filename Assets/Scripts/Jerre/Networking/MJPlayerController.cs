using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Jerre.Networking
{
    public class MJPlayerController : NetworkBehaviour, IRewindable
    {
        private int nextInputSequence, nextPacketSequence;
        private int serversAckForClientPacketSequence = -1;

        private int latestServerSequence = -1;
        private int saLatestReceivedPlayerSequence;

        private bool shouldReconcileInput = false;

        private SizeLimitedStack<PlayerInputForPacket> playerInputHistory;
        private float timeBetweenPacketsFromClientToServer = 
            1f / Constants.SEND_RATE_PLAYER_INPUT_FROM_CLIENT_TO_SERVER;
		private float timeBetweenInputFetching = 
            1f / Constants.FETCH_PLAYER_INPUT_RATE;
        private float timeSinceLastPacketSent, timeSinceInputFetched;

        private float timeBetweenPacketsFromServerToClient =
            1f / Constants.SEND_RATE_SERVER_TO_CLIENT_PLAYER;

        private bool hasReleasedShootButton = true;
        private bool hasReleaseSecondaryButton = true;

        private SizeLimitedStack<ValidatedPlayerInput> validatedPlayerInputsFromServer;

        // Server validation
        private SizeLimitedStack<PlayerInput> receivedPlayerInputs;
        private SizeLimitedStack<ValidatedPlayerInput> validatedPlayerInputs;
        private int saNextPacketSequence;
        private int currentInputSequenceToProcess;
        private float saTimeSinceInputProcessed, saTimeSinceLastPacketSent;

		private CharacterController characterController;

        public float speed = 2f;

        private float cTimeSinceInputProcessed;

        public float shootDistance = 4f;
        public ParticleSystem hitSuccessParticles, hitFailureParticles;
        public LineRenderer lineRenderer;
        public Transform muzzle;
        public Rocket rocketPrefab;

        //Rewind variables, prefix rew
        private Vector3 rewResetPosition;
        private Quaternion rewResetRotation;
        private bool canReset = false;

        private bool autoControlPlayer;
        private int direction = 1;
        private float autoTimeToMove = 2f, elapsedAuto;


        void Awake() 
        {
            playerInputHistory = new SizeLimitedStack<PlayerInputForPacket>(
                Constants.MAX_PLAYER_INPUT_HISTORY
            );
            playerInputHistory.Push(FetchInput(nextInputSequence++, nextPacketSequence));

            receivedPlayerInputs = new SizeLimitedStack<PlayerInput>(
                Constants.MAX_RECEIVED_PLAYER_INPUT_LENGTH
            );

            validatedPlayerInputs = new SizeLimitedStack<ValidatedPlayerInput>(
                Constants.MAX_VALIDATED_PLAYER_INPUTS_ON_SERVER
            );

            validatedPlayerInputsFromServer = new SizeLimitedStack<ValidatedPlayerInput>(
                Constants.MAX_VALIDATED_PLAYER_INPUTS_ON_CLIENT
            );
        }

        // Use this for initialization
        void Start()
        {
            characterController = GetComponent<CharacterController>();
        }

        // Update is called once per frame
        void Update()
        {
            if (isServer && hasAuthority)
            {
                timeSinceLastPacketSent += Time.deltaTime;
                timeSinceInputFetched += Time.deltaTime;

                var playerInput = playerInputHistory.Peek();
                var deltaTime = Time.deltaTime;
                if (timeSinceInputFetched >= timeBetweenInputFetching)
                {
                    var timeRemainingForOutdatedInput = timeBetweenInputFetching -
                        (timeSinceInputFetched - Time.deltaTime);
                    deltaTime -= timeRemainingForOutdatedInput;
                    characterController.Move(
                        playerInput.MoveDir3D * speed * timeRemainingForOutdatedInput);
                    RotateTo(playerInput.LookDir3D);

                    var inputSequence = nextInputSequence++;
                    playerInputHistory.Push(
                        FetchInput(inputSequence, nextPacketSequence)
                    );
                    validatedPlayerInputs.Push(new ValidatedPlayerInput(
                        inputSequence,
                        transform.position,
                        transform.rotation
                    ));
                    timeSinceInputFetched -= timeBetweenInputFetching;
                    if (playerInputHistory.Peek().playerAction == PlayerAction.PRIMARY) {
                        Shoot();
                    } else if (playerInputHistory.Peek().playerAction == PlayerAction.SECONDARY) {
                        ShootRocket();
                    }
                }
                characterController.Move(playerInput.MoveDir3D * speed * deltaTime);
                RotateTo(playerInput.LookDir3D);

                if (timeSinceLastPacketSent >= timeBetweenPacketsFromServerToClient)
                {
                    timeSinceLastPacketSent -= timeBetweenPacketsFromServerToClient;
                    RpcHandlePacketsFromServer(new ServerToPlayerMessage(
                        validatedPlayerInputs.Peek().inputSequence,  //TODO: Dette er feil, skal bruke packet sequence her
                        saNextPacketSequence++,
                        validatedPlayerInputs.GetArray()
                    ));
                }
            } else if (hasAuthority) {
                timeSinceLastPacketSent += Time.deltaTime;
                timeSinceInputFetched += Time.deltaTime;

                var playerInput = playerInputHistory.Peek();
                var deltaTime = Time.deltaTime;
                if (shouldReconcileInput) {
                    shouldReconcileInput = false;
                    var latestValidatedPlayerInput = 
                        validatedPlayerInputsFromServer.Peek();
                    transform.position = latestValidatedPlayerInput.position;
                    transform.rotation = latestValidatedPlayerInput.rotation;
                    var inputsToReconcile = playerInputHistory.GetArrayByFilter(
                        pi => pi.inputSequence > latestValidatedPlayerInput.inputSequence
                    );
                    for (var i = 0; i < inputsToReconcile.Length; i++) {
                        var input = inputsToReconcile[i];
                        characterController.Move(
                            input.MoveDir3D * speed * timeBetweenInputFetching
                        );
                        RotateTo(input.LookDir3D);
                        var originalInput = playerInputHistory.FindBy(pi => pi.inputSequence == input.inputSequence);
                        playerInputHistory.ReplaceByFilter(
                            pi => pi.inputSequence == input.inputSequence,
                            new PlayerInputForPacket(
                                originalInput.inputSequence,
                                originalInput.packetSequence,
                                transform.position,
                                transform.rotation,
                                originalInput.moveDir,
                                originalInput.lookDir,
                                originalInput.playerAction
                            )
                        );
                    }
				}

                if (timeSinceInputFetched >= timeBetweenInputFetching) {
                    var timeRemainingForOutdatedInput = timeBetweenInputFetching - 
                        (timeSinceInputFetched - Time.deltaTime);
                    deltaTime -= timeRemainingForOutdatedInput;
                    characterController.Move(
                        playerInput.MoveDir3D * speed * timeRemainingForOutdatedInput);
                    RotateTo(playerInput.LookDir3D);

                    playerInputHistory.Push(
                        FetchInput(nextInputSequence++, nextPacketSequence)
                    );
					timeSinceInputFetched -= timeBetweenInputFetching;
                }
                characterController.Move(playerInput.MoveDir3D * speed * deltaTime);
                RotateTo(playerInput.LookDir3D);

                if (timeSinceLastPacketSent >= timeBetweenPacketsFromClientToServer) {
                    timeSinceLastPacketSent -= timeBetweenPacketsFromClientToServer;
                    CmdHandlePacketsFromClient(new PlayerToServerMessage(
                        latestServerSequence,
                        nextPacketSequence++,
                        AssemblePlayerInputs(serversAckForClientPacketSequence)));
                }
            } else if (isServer) {
                saTimeSinceInputProcessed += Time.deltaTime;
                saTimeSinceLastPacketSent += Time.deltaTime;
				
                var playerInput = receivedPlayerInputs.FindBy((pi) => pi.inputSequence >= currentInputSequenceToProcess);
                if (playerInput.Equals(default(PlayerInput))) {
                    return;
                }

                var deltaTime = Time.deltaTime;
                if (saTimeSinceInputProcessed >= timeBetweenInputFetching) {
                    var timeRemainingForOutdatedInput = timeBetweenInputFetching - (
                        timeSinceInputFetched - Time.deltaTime);
                    deltaTime -= timeRemainingForOutdatedInput;

                    characterController.Move(
                        playerInput.MoveDir3D * speed * timeRemainingForOutdatedInput);
                    RotateTo(playerInput.LookDir3D);

                    if ((transform.position - playerInput.position).sqrMagnitude < Constants.MAX_DISCREPANCY_FROM_CLIENT) {
                        transform.position = playerInput.position;
                        transform.rotation = playerInput.rotation;
                    }

                    validatedPlayerInputs.Push(new ValidatedPlayerInput(
                        playerInput.inputSequence,
                        transform.position,
                        transform.rotation));

                    playerInput = receivedPlayerInputs.FindBy((pi) => pi.inputSequence > currentInputSequenceToProcess);
                    currentInputSequenceToProcess = playerInput.inputSequence;
                    saTimeSinceInputProcessed -= timeBetweenInputFetching;
                    if (playerInput.playerAction == PlayerAction.PRIMARY) {
                        Shoot();
                    } else if (playerInput.playerAction == PlayerAction.SECONDARY) {
                        ShootRocket();
                    }
                }

                characterController.Move(playerInput.MoveDir3D * speed * deltaTime);
                RotateTo(playerInput.LookDir3D);

                if (saTimeSinceLastPacketSent >= timeBetweenPacketsFromServerToClient) {
                    saTimeSinceLastPacketSent -= timeBetweenPacketsFromServerToClient;
                    RpcHandlePacketsFromServer(new ServerToPlayerMessage(
                        receivedPlayerInputs.Peek().inputSequence,  //TODO: Dette er feil, skal bruke packet sequence her
                        saNextPacketSequence++,
                        validatedPlayerInputs.GetArray()
                    ));

                }
            } else {
                if (validatedPlayerInputsFromServer.Count < 3) {
                    return;   
                }

				cTimeSinceInputProcessed += Time.deltaTime;
                var oldest = validatedPlayerInputsFromServer.PeekFromOldest(0);
                var nextOldest = validatedPlayerInputsFromServer.PeekFromOldest(1);
                var maxinterval = timeBetweenInputFetching * (nextOldest.inputSequence - oldest.inputSequence);
                if (cTimeSinceInputProcessed >= maxinterval) {
                    cTimeSinceInputProcessed -= maxinterval;
                    validatedPlayerInputsFromServer.RemoveOldest();
                    oldest = validatedPlayerInputsFromServer.PeekFromOldest(0);
                    nextOldest = validatedPlayerInputsFromServer.PeekFromOldest(1);
                }
                transform.position = Vector3.Lerp(oldest.position, nextOldest.position, cTimeSinceInputProcessed / maxinterval);
                transform.rotation = Quaternion.Lerp(oldest.rotation, nextOldest.rotation, cTimeSinceInputProcessed / maxinterval);
            }
        }

        private void RotateTo(Vector3 lookRotation) {
            if (lookRotation.sqrMagnitude > 0f) {
                transform.LookAt(transform.position + lookRotation, Vector3.up);
            }
        }

        [Command(channel = 1)]
        public void CmdHandlePacketsFromClient(PlayerToServerMessage message) {
            foreach (var playerInput in message.playerInputs) {
                receivedPlayerInputs.Push(playerInput);
            }
            nextPacketSequence++;
        }

        [ClientRpc(channel = 1)]
        public void RpcHandlePacketsFromServer(ServerToPlayerMessage message) {
            if (message.serverSequence > latestServerSequence) {
                latestServerSequence = message.serverSequence;
                serversAckForClientPacketSequence = message.latestPlayerSequenceReceived;
                foreach (var validatedPlayerInput in message.validatedPlayerInputs) {
                    if (validatedPlayerInputsFromServer.Count == 0 
                        || validatedPlayerInputsFromServer.Peek().inputSequence < validatedPlayerInput.inputSequence) {
						validatedPlayerInputsFromServer.Push(validatedPlayerInput);
                    }
                }
                if (hasAuthority)
                {
                    var latestValidated = validatedPlayerInputsFromServer.Peek();
                    var playerInput = playerInputHistory.FindBy(pi => pi.inputSequence == latestValidated.inputSequence);
                    if ((latestValidated.position - playerInput.position).sqrMagnitude > 0.005f)
                    {
                        shouldReconcileInput = true;
                        Debug.Log("Should reconcile");
                    }
                }
            }
        }

        private PlayerInputForPacket FetchInput(int inputSequence, int packetSequence) {

            if (Input.GetKey(KeyCode.O)) {
                autoControlPlayer = !autoControlPlayer;
            }


            if (autoControlPlayer) {
                elapsedAuto += Time.deltaTime;
                if (elapsedAuto >= autoTimeToMove) {
                    elapsedAuto -= autoTimeToMove;
                    direction *= -1;
                }
                return new PlayerInputForPacket(
                    inputSequence,
                    packetSequence,
                    transform.position,
                    transform.rotation,
                    new Vector2(direction, 0),
                    new Vector2(direction, 0),
                    PlayerAction.NONE);
            }
            //var x = Input.GetAxis("Horizontal");
            var x = Input.GetKey(KeyCode.A) ? -1 : Input.GetKey(KeyCode.D) ? 1 : 0;
            //var y = Input.GetAxis("Vertical");
            var y = Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0;
            var playerAction = PlayerAction.NONE;
            if (!Input.GetKey(KeyCode.Space)) {
                hasReleasedShootButton = true;
            }
            if (!Input.GetKey(KeyCode.V)) {
                hasReleaseSecondaryButton = true;
            }

            if (Input.GetKey(KeyCode.Space) && hasReleasedShootButton)
            {
                playerAction = PlayerAction.PRIMARY;
                hasReleasedShootButton = false;
            } else if (Input.GetKey(KeyCode.V) 
                       && playerAction == PlayerAction.NONE 
                       && hasReleaseSecondaryButton) {
                playerAction = PlayerAction.SECONDARY;
                hasReleaseSecondaryButton = false;
            }
            return new PlayerInputForPacket (inputSequence, 
                                   packetSequence, 
                                   transform.position, 
                                   transform.rotation, 
                                   new Vector2(x, y).normalized, 
                                   new Vector2(x, y).normalized,
                                   playerAction);
        }

        private PlayerInput[] AssemblePlayerInputs(int latestAckedPacketSequnce) {
            var relevantInputs = playerInputHistory.GetArrayByFilter(
                (pi) => pi.packetSequence > latestAckedPacketSequnce);
            var playerInputs = new PlayerInput[relevantInputs.Length];
            for (var i = 0; i < relevantInputs.Length; i++) {
                playerInputs[i] = relevantInputs[i].ToPlayerInput();
            }
            return playerInputs;
        }

        public bool Rewind(float time) {
            rewResetPosition = transform.position;
            rewResetRotation = transform.rotation;
            canReset = true;

            if (time < 0f) {
                return false;
            }

            if (Mathf.Abs(time) < timeBetweenInputFetching) {
                return true;
            }

            if (validatedPlayerInputs.Count == 0) {
                return false;
            }

            if (validatedPlayerInputs.Count == 1) {
                var old = validatedPlayerInputs.Peek();
                transform.position = old.position;
                transform.rotation = old.rotation;
                return true;
            }

            if (time >= validatedPlayerInputs.PeekFromOldest(0).inputSequence * timeBetweenInputFetching) {
                var old = validatedPlayerInputs.PeekFromOldest(0);
                transform.position = old.position;
                transform.rotation = old.rotation;
                return true;
            }

            var history = validatedPlayerInputs.GetArray();
            var currentSequence = history[0].inputSequence + 1;
			int indexOlder = -1, indexNewer = -1;
            for (var i = 0; i < history.Length - 1; i++) {
                var validated = history[i];
                if ((currentSequence - validated.inputSequence) * timeBetweenInputFetching >= time) {
                    indexOlder = i;
                    indexNewer = i - 1;
                    break;
                }
            }

            var oldValidated = history[indexOlder];
            var newValidated = history[indexNewer];
            var timeSinceOldest = (currentSequence - oldValidated.inputSequence) * timeBetweenInputFetching;
            var timeSinceNewest = (currentSequence - newValidated.inputSequence) * timeBetweenInputFetching;
            var lerp = (timeSinceOldest - time) / (timeSinceOldest - timeSinceNewest);
            transform.position = Vector3.Lerp(oldValidated.position, newValidated.position, lerp);
            transform.rotation = Quaternion.Lerp(oldValidated.rotation, newValidated.rotation, lerp);
            return true;
        }

		public void Reset()
		{
            canReset = false;
            transform.position = rewResetPosition;
            transform.rotation = rewResetRotation;
		}

		private void Shoot() {
            byte error;

            var shouldRewind = !(hasAuthority && isServer);
            if (shouldRewind) {
                Debug.Log("Should rewind!");
                var rtt = NetworkTransport.GetCurrentRTT(
					connectionToClient.hostId, 
					connectionToClient.connectionId, 
					out error);
                var allRewindablePlayers = GameObject.FindObjectsOfType<MJPlayerController>();
                for (var i = 0; i < allRewindablePlayers.Length; i++) {
                    if (allRewindablePlayers[i] == this) {
                        continue;
                    }
                    Debug.Log("Rewinding player for connection " + allRewindablePlayers[i].connectionToClient.connectionId);
					allRewindablePlayers[i].Rewind(rtt / 1000f);
                }
            }

            RaycastHit hit;
            if (Physics.Raycast(muzzle.position, muzzle.forward, out hit, shootDistance)) {
                RpcShowShot(muzzle.position, hit.point, true);
            } else {
                RpcShowShot(muzzle.position, muzzle.position + muzzle.forward * shootDistance, false);
            }

            if (shouldRewind) {
                var allRewindablePlayers = GameObject.FindObjectsOfType<MJPlayerController>();
                for (var i = 0; i < allRewindablePlayers.Length; i++)
                {
                    if (allRewindablePlayers[i] == this)
                    {
                        continue;
                    }
                    Debug.Log("Resetting player for connection " + allRewindablePlayers[i].connectionToClient.connectionId);
                    allRewindablePlayers[i].Reset();
                }
            }
        }

        private void ShootRocket() {
            var rocket = Instantiate(rocketPrefab, muzzle.position + muzzle.forward * 1.5f, muzzle.rotation);
            rocket.ownerConnectionId = connectionToClient.connectionId;
            NetworkServer.Spawn(rocket.gameObject);
        }

        [ClientRpc(channel = 1)]
        private void RpcShowShot(Vector3 origin, Vector3 end, bool hitSuccess) {
            var line = Instantiate(lineRenderer);
            line.SetPositions(new Vector3[]{ origin, end });
            Destroy(line.gameObject, 2f);

            if (hitSuccess)
            {
                var particles = Instantiate(hitSuccessParticles, end, Quaternion.identity);
                Destroy(particles.gameObject, 2f);
            }
            else
            {
                var particles = Instantiate(hitFailureParticles, end, Quaternion.identity);
                Destroy(particles.gameObject, 2f);
            }
        }




        class PlayerInputForPacket {
            public int inputSequence, packetSequence;
            public Vector3 position;
            public Quaternion rotation;
            public Vector2 moveDir, lookDir;
            public Vector3 MoveDir3D {
                get { return new Vector3(moveDir.x, 0, moveDir.y); }
            }
            public Vector3 LookDir3D {
                get { return new Vector3(lookDir.x, 0, lookDir.y); }
            }
            public PlayerAction playerAction;

            public PlayerInputForPacket(int inputSequence, int packetSequence, Vector3 position, Quaternion rotation, Vector2 moveDir, Vector2 lookDir, PlayerAction playerAction)
            {
                this.inputSequence = inputSequence;
                this.packetSequence = packetSequence;
                this.position = position;
                this.rotation = rotation;
                this.moveDir = moveDir;
                this.lookDir = lookDir;
                this.playerAction = playerAction;
            }

            public PlayerInput ToPlayerInput() {
                return new PlayerInput(
                    inputSequence, position, rotation, moveDir, lookDir, playerAction);
            }

            public override string ToString()
            {
                return string.Format(
                    "PlayerInput(inputSequence: {0}, packetSequence: {1}, position: {2}, rotation: {3}, moveDir: {4}, lookDir: {5}, playerAction: {6})",
                    inputSequence, packetSequence, position, rotation, moveDir, lookDir, playerAction
                );
            }
        }
    }
}