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

        public float speed = 2f, inertia = 1f, inertiaRotation = 10f, boostSpeed = 20f;
        private Vector3 oldSpeedVector = Vector3.zero;

        private float cTimeSinceInputProcessed;

        public float shootDistance = 4f;
        public ParticleSystem hitSuccessParticles, hitFailureParticles;
        public LineRenderer lineRenderer;
        public Transform muzzle;
        public Rocket rocketPrefab, bombPrefab;

        //Rewind variables, prefix rew
        private Vector3 rewResetPosition;
        private Quaternion rewResetRotation;
        private bool canReset = false;

        //Reconciliation variables
        public float lerpAmountForReconciliation = 1/3f;
        private PredictionServer predictionServer;

        private bool autoControlPlayer;
        private int direction = 1;
        private float autoTimeToMove = 2f, elapsedAuto;

        [SyncVar(hook = "ChangePickup")]
        private PickupEnum currentPickup = PickupEnum.BOMB;
        private GameObject pickupIndicator;
        public Material rocketPickupMaterial, bombPickupMaterial;

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

		public override void OnStartAuthority()
		{
            pickupIndicator = GameObject.FindWithTag("PickupIndicator");
		}

		// Use this for initialization
		void Start()
        {
            characterController = GetComponent<CharacterController>();
            predictionServer = new PredictionServer(transform.position, transform.rotation, Vector3.zero);
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
                    Move(playerInput.MoveDir3D, timeRemainingForOutdatedInput, playerInput.Boosting);
                    RotateTo(playerInput.LookDir3D);

                    var inputSequence = nextInputSequence++;
                    playerInputHistory.Push(
                        FetchInput(inputSequence, nextPacketSequence)
                    );
                    validatedPlayerInputs.Push(new ValidatedPlayerInput(
                        inputSequence,
                        transform.position,
                        transform.rotation,
                        oldSpeedVector
                    ));
                    timeSinceInputFetched -= timeBetweenInputFetching;
                    if (playerInputHistory.Peek().playerAction == PlayerAction.PRIMARY) {
                        Shoot();
                    } else if (playerInputHistory.Peek().playerAction == PlayerAction.SECONDARY) {
                        UserSecondary();
                    }
                    playerInput = playerInputHistory.Peek();
                }
                Move(playerInput.MoveDir3D, deltaTime, playerInput.Boosting);
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
                var clientPosition = transform.position;   // Po
                var clientRotation = transform.rotation;
                var originalOldSpeedVector = oldSpeedVector;
                if (shouldReconcileInput) {
                    shouldReconcileInput = false;
                    var latestValidatedPlayerInput = 
                        validatedPlayerInputsFromServer.Peek();
                    transform.position = latestValidatedPlayerInput.position;
                    transform.rotation = latestValidatedPlayerInput.rotation;
                    oldSpeedVector = latestValidatedPlayerInput.speed;
                    var inputsToReconcile = playerInputHistory.GetArrayByFilter(
                        pi => pi.inputSequence > latestValidatedPlayerInput.inputSequence
                    );
                    for (var i = 0; i < inputsToReconcile.Length; i++) {
                        var input = inputsToReconcile[i];
                        Move(input.MoveDir3D, timeBetweenInputFetching, input.Boosting);
                        RotateTo(input.LookDir3D);
                    }
                    transform.position = clientPosition;
                    transform.rotation = clientRotation;
                    oldSpeedVector = originalOldSpeedVector;
				}

                if (timeSinceInputFetched >= timeBetweenInputFetching) {
                    var timeRemainingForOutdatedInput = timeBetweenInputFetching - 
                        (timeSinceInputFetched - Time.deltaTime);
                    deltaTime -= timeRemainingForOutdatedInput;
                    MyUpdate(timeRemainingForOutdatedInput, playerInputHistory.Peek());

                    playerInputHistory.Push(
                        FetchInput(nextInputSequence++, nextPacketSequence)
                    );
					timeSinceInputFetched -= timeBetweenInputFetching;
                }
                MyUpdate(deltaTime, playerInputHistory.Peek());

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
                while (saTimeSinceInputProcessed >= timeBetweenInputFetching) {
                    var timeRemainingForOutdatedInput = timeBetweenInputFetching - (
                        saTimeSinceInputProcessed - Time.deltaTime);
                    deltaTime -= timeRemainingForOutdatedInput;

                    Move(playerInput.MoveDir3D, timeRemainingForOutdatedInput, playerInput.Boosting);
                    RotateTo(playerInput.LookDir3D);

                    validatedPlayerInputs.Push(new ValidatedPlayerInput(
                        playerInput.inputSequence,
                        transform.position,
                        transform.rotation, 
                        oldSpeedVector
                    ));

                    playerInput = receivedPlayerInputs.FindBy((pi) => pi.inputSequence > currentInputSequenceToProcess);
                    if (playerInput.Equals(default(PlayerInput))) {
                        saTimeSinceInputProcessed -= timeBetweenInputFetching;
                        return;
                    }
                    currentInputSequenceToProcess = playerInput.inputSequence;
                    saTimeSinceInputProcessed -= timeBetweenInputFetching;
                    if (playerInput.playerAction == PlayerAction.PRIMARY) {
                        Shoot();
                    } else if (playerInput.playerAction == PlayerAction.SECONDARY) {
                        UserSecondary();
                    }
                }

                Move(playerInput.MoveDir3D, deltaTime, playerInput.Boosting);
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

        private void MyUpdate(float deltaTime, MJPlayerController.PlayerInputForPacket playerInput) {
            //Move client side prediction
            Move(playerInput.MoveDir3D, deltaTime, playerInput.Boosting);
            RotateTo(playerInput.LookDir3D);
            var clientPredictedPosition = transform.position;
            var clientPredictedRotation = transform.rotation;
            var tempSpeed = oldSpeedVector;
            //Move using server prediction    
            transform.position = predictionServer.position;
            transform.rotation = predictionServer.rotation;
            oldSpeedVector = predictionServer.speed;
            Move(playerInput.MoveDir3D, deltaTime, playerInput.Boosting);
            RotateTo(playerInput.LookDir3D);
            predictionServer = new PredictionServer(transform.position, transform.rotation, oldSpeedVector);

            var lerpedPredictedPos = Vector3.Lerp(clientPredictedPosition, predictionServer.position, lerpAmountForReconciliation);
            var lerpedPredictedRot = Quaternion.Lerp(clientPredictedRotation, predictionServer.rotation, lerpAmountForReconciliation);
            transform.position = lerpedPredictedPos;
            transform.rotation = lerpedPredictedRot;
            oldSpeedVector = Vector3.Lerp(tempSpeed, predictionServer.speed, lerpAmountForReconciliation);
        }

        private void Move(Vector3 direction, float deltaTime, bool boosting) {
            var newSpeedVector = direction * (boosting ? boostSpeed : speed) * deltaTime;
            // characterController.Move(newSpeedVector);
            var angle = Mathf.Clamp(Vector3.Angle(oldSpeedVector.normalized, newSpeedVector.normalized), 5, 175);
            float rotationalInertia = (1f - angle / 180f) * inertiaRotation;
            rotationalInertia = angle > 75 ? 0.1f / inertiaRotation : 1f;
            rotationalInertia = angle > 150 ? 1f / inertiaRotation : 1f;
            float totalInertia = inertia * rotationalInertia;
            var resultingVector = Vector3.Lerp(oldSpeedVector, newSpeedVector, totalInertia * deltaTime);
            characterController.Move(resultingVector);
            oldSpeedVector = resultingVector;
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
                    PlayerAction.NONE,
                    oldSpeedVector);
            }
            var moveHorizontal = Input.GetAxis(InputNames.HORIZONTAL_MOVEMENT);
            var moveVertical = Input.GetAxis(InputNames.VERTICAL_MOVEMENT);
            var playerAction = PlayerAction.NONE;
            var aimHorizontal = Input.GetAxis(InputNames.AIM_HORIZONTAL);
            var aimVertical = Input.GetAxis(InputNames.AIM_VERTICAL);
            
            var actionPrimary = Input.GetButton(InputNames.ACTION_PRIMARY);
            var actionSecondary = Input.GetButton(InputNames.ACTION_SECONDARY);
            var boost = Input.GetAxis(InputNames.ACTION_BOOST);
            if (boost == 0f) {
                boost = (Input.GetButton(InputNames.ACTION_BOOST) ? 1f : 0f);
            }
            if (!actionPrimary) {
                hasReleasedShootButton = true;
            }
            if (!actionSecondary) {
                hasReleaseSecondaryButton = true;
            }

            if (actionPrimary && hasReleasedShootButton)
            {
                playerAction = PlayerAction.PRIMARY;
                hasReleasedShootButton = false;
            } else if (actionSecondary
                       && playerAction == PlayerAction.NONE 
                       && hasReleaseSecondaryButton) {
                playerAction = PlayerAction.SECONDARY;
                hasReleaseSecondaryButton = false;
            } else if (boost > 0.2f) {
                playerAction = PlayerAction.BOOST;
            }
            return new PlayerInputForPacket (inputSequence, 
                                   packetSequence, 
                                   transform.position, 
                                   transform.rotation, 
                                   new Vector2(moveHorizontal, moveVertical).normalized, 
                                   new Vector2(moveHorizontal, moveVertical).normalized,
                                   playerAction,
                                   oldSpeedVector);
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

        private void UserSecondary() {

            if (currentPickup == PickupEnum.ROCKET)
            {
                var rocket = Instantiate(rocketPrefab, muzzle.position + muzzle.forward * 1.5f, muzzle.rotation);
                rocket.ownerConnectionId = connectionToClient.connectionId;
                NetworkServer.Spawn(rocket.gameObject);
            } else if (currentPickup == PickupEnum.BOMB) {
                var bomb = Instantiate(bombPrefab, transform.position - transform.forward * 1.5f, transform.rotation);
                NetworkServer.Spawn(bomb.gameObject);
            }
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

        [Command]
        public void CmdChangePickup(PickupEnum pickup)
        {
            this.currentPickup = pickup;
        }

        public void ChangePickup(PickupEnum pickup) {
            if (hasAuthority) {
                if (pickup == PickupEnum.ROCKET)
                {
                    Debug.Log("PickupIndicator: " + pickupIndicator);
                    var meshRenderer = pickupIndicator.GetComponent<MeshRenderer>();
                    Debug.Log("PickupRenderer.MeshRenderer: " + meshRenderer);
                    meshRenderer.material = rocketPickupMaterial;
                }
                else if (pickup == PickupEnum.BOMB) {
                    Debug.Log("PickupIndicator: " + pickupIndicator);
                    var meshRenderer = pickupIndicator.GetComponent<MeshRenderer>();
                    Debug.Log("PickupRenderer.MeshRenderer: " + meshRenderer);
                    meshRenderer.material = bombPickupMaterial;
                }
            }
        }


        class PlayerInputForPacket {
            public int inputSequence, packetSequence;
            public Vector3 position;
            public Vector3 speed;
            public Quaternion rotation;
            public Vector2 moveDir, lookDir;
            public Vector3 MoveDir3D {
                get { return new Vector3(moveDir.x, 0, moveDir.y); }
            }
            public Vector3 LookDir3D {
                get { return new Vector3(lookDir.x, 0, lookDir.y); }
            }
            public PlayerAction playerAction;

            public bool Boosting {
                get { return playerAction == PlayerAction.BOOST; }
            }

            public PlayerInputForPacket(int inputSequence, int packetSequence, Vector3 position, Quaternion rotation, Vector2 moveDir, Vector2 lookDir, PlayerAction playerAction, Vector3 speed)
            {
                this.inputSequence = inputSequence;
                this.packetSequence = packetSequence;
                this.position = position;
                this.rotation = rotation;
                this.moveDir = moveDir;
                this.lookDir = lookDir;
                this.playerAction = playerAction;
                this.speed = speed;
            }

            public PlayerInput ToPlayerInput() {
                return new PlayerInput(
                    inputSequence, position, rotation, moveDir, lookDir, playerAction, speed);
            }

            public override string ToString()
            {
                return string.Format(
                    "PlayerInput(inputSequence: {0}, packetSequence: {1}, position: {2}, rotation: {3}, moveDir: {4}, lookDir: {5}, playerAction: {6}, speed: {7})",
                    inputSequence, packetSequence, position, rotation, moveDir, lookDir, playerAction, speed
                );
            }
        }

        class PredictionServer {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 speed;

            public PredictionServer(Vector3 position, Quaternion rotation, Vector3 speed) {
                this.position = position;
                this.rotation = rotation;
                this.speed = speed;
            }
        }
    }
}