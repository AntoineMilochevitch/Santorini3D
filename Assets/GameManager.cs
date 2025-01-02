using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Data.SqlTypes;

public class GameManager : MonoBehaviour
{
    public GameObject player1; // Parent contenant les deux pions du joueur 1
    public GameObject player2; // Parent contenant les deux pions du joueur 2
    public Text turnIndicator;
    public Button startButton;
    public Dropdown modeDropdown;

    private int currentPlayer = 1; // 1 pour joueur 1, 2 pour joueur 2
    private bool gameStarted = false;
    private bool initialPlacement = true;
    private int player1PawnsPlaced = 0;
    private int player2PawnsPlaced = 0;

    private PlayerMovement activePlayerMovement; // Le script PlayerMovement actif
    private bool playerTurnCompleted = false; // Indique si le joueur a terminé son tour
    private GameObject activePawn;
    private BuildingManager buildingManager;

    private GameObject lastPlacedPawn = null;
    private bool waitingForPawnToSettle = false;

    private int dropDownValue = 0;
    private bool isPlacedByServer = false;

    private PythonClient pythonClient;

    private Vector3 lastBuildPosition;
    private Vector3 lastMovePosition;

    private bool moveCompleted = false;

    void Start()
    {
        // Initialisation des boutons et affichages
        turnIndicator.gameObject.SetActive(false);
        startButton.onClick.AddListener(StartGame);
        UpdateTurnIndicator();

        // Rendre les pions invisibles et désactiver la gravité
        SetPawnsVisibile(player1, false);
        SetPawnsVisibile(player2, false);

        pythonClient = UnityEngine.Object.FindFirstObjectByType<PythonClient>();
        if (pythonClient == null)
        {
            Debug.LogError("PythonClient n'est pas trouvé dans la scène.");
        }

        // Initialiser le BuildingManager
        buildingManager = UnityEngine.Object.FindFirstObjectByType<BuildingManager>();
        if (buildingManager == null)
        {
            Debug.LogError("BuildingManager n'est pas trouvé dans la scène.");
        }

        PlayerMovement.OnPlayerAction += HandlePlayerAction;

    }

    void OnDestroy()
    {
        PlayerMovement.OnPlayerAction -= HandlePlayerAction;
    }

    void HandlePlayerAction(string playerName, Vector3 movePosition, Vector3 buildPosition)
    {
        lastMovePosition = movePosition;
        lastBuildPosition = buildPosition;
    }

    void StartGame()
    {
        if (pythonClient == null)
        {
            Debug.LogError("PythonClient n'est pas trouvé dans la scène.");
            return;
        }

        int selectedMode = modeDropdown.value + 1;
        pythonClient.SendMessageToServer("START " + selectedMode);
        dropDownValue = selectedMode;

        // Faire disparaître le menu déroulant
        modeDropdown.gameObject.SetActive(false);

        ResetGame();
        gameStarted = true;
        startButton.gameObject.SetActive(false);
        turnIndicator.gameObject.SetActive(true);
        UpdateTurnIndicator();

        // Attendre que le jeu soit démarré par le serveur
        //StartCoroutine(WaitForGameStart());
    }

    IEnumerator WaitForGameStart()
    {
        while (!pythonClient.IsStarted())
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Continuer le jeu après que le serveur a démarré le jeu
        if (initialPlacement)
        {
            //HandleInitialPlacement();
        }
        else
        {
            HandlePlayerTurn();
        }
    }

    void Update()
    {
        if (!gameStarted) return;

        if (initialPlacement)
        {
            HandleInitialPlacement();
        }
        else
        {
            HandlePlayerTurn();
        }
    }

    void HandleInitialPlacement()
    {
        if (isPlacedByServer)
        {
            isPlacedByServer = false;
            return;
        }
        if (waitingForPawnToSettle)
        {
            // Attendre que le pion se stabilise
            Rigidbody rb = lastPlacedPawn.GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude < 0.1f)
            {
                waitingForPawnToSettle = false;

                if (currentPlayer == 1 && player1PawnsPlaced == 2)
                {
                    currentPlayer = 2;
                    UpdateTurnIndicator();
                }
                else if (currentPlayer == 2 && player2PawnsPlaced == 2)
                {
                    initialPlacement = false; // Placement initial terminé
                    currentPlayer = 1; // Joueur 1 commence
                    UpdateTurnIndicator();
                    SetActivePlayerCollidersAndGravity(); // Activer/désactiver les colliders et la gravité après le placement initial
                }
            }
        }
        else
        {
            if (currentPlayer == 1 && player1PawnsPlaced < 2)
            {
                GameObject placedPawn = PlacePawn(player1);
                if (placedPawn != null)
                {
                    player1PawnsPlaced++;
                    lastPlacedPawn = placedPawn;
                    waitingForPawnToSettle = true;
                }
            }
            else if (currentPlayer == 2 && player2PawnsPlaced < 2)
            {
                GameObject placedPawn = PlacePawn(player2);
                if (placedPawn != null)
                {
                    player2PawnsPlaced++;
                    lastPlacedPawn = placedPawn;
                    waitingForPawnToSettle = true;
                }
            }
        }
    }

    public void setInitialPlacement(bool value)
    {
        initialPlacement = value;
        currentPlayer = 1;
        UpdateTurnIndicator();
    }

    public void setMoveCompleted(bool value)
    {
        moveCompleted = value;
    }

    GameObject PlacePawn(GameObject player)
    {
        // Récupère les clics de souris pour positionner les pions
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.transform.CompareTag("Case"))
                {
                    // Vérifie si la case est libre
                    if (!IsOccupied(hit.point))
                    {
                        // Place un pion du joueur
                        foreach (Transform pawn in player.transform)
                        {
                            if (!pawn.gameObject.activeInHierarchy) // Cherche un pion non placé
                            {
                                Vector3 casePosition = hit.transform.position;
                                pawn.position = new Vector3(casePosition.x, casePosition.y + 1.0f, casePosition.z);
                                pawn.gameObject.SetActive(true);
                                Rigidbody rb = pawn.GetComponent<Rigidbody>();
                                if (rb != null)
                                {
                                    rb.useGravity = true; // Réactiver la gravité
                                }

                                // Convertir les coordonnées en indices de cases
                                Vector2Int caseIndices = ConvertPositionToIndices(casePosition);

                                // Envoyer les coordonnées du pion au serveur
                                if (pythonClient != null)
                                {
                                    string message = $"INIT {pawn.name} {caseIndices.x} {caseIndices.z}";
                                    pythonClient.SendMessageToServer(message);
                                }

                                return pawn.gameObject; // Retourne le pion placé
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    Vector3 ConvertIndicesToPosition(int x, int z)
    {
        float posX = 2.2f * (x - 2); // Ajustement des coordonnées
        float posZ = 2.2f * (z - 2); // Ajustement des coordonnées
        return new Vector3(posX, 0f, posZ);
    }

    public void PlacePawnFromServer(string playerName, string pawnName, int x, int z)
    {
        isPlacedByServer = true;
        // Convertir les indices de cases en positions réelles
        Vector3 casePosition = ConvertIndicesToPosition(x, z);

        GameObject player = null;
        if (playerName == "Player1")
        {
            player = player1;
        }
        else if (playerName == "Player2")
        {
            player = player2;
        }
        else
        {
            Debug.LogError("Nom de joueur invalide");
            return;
        }

        if (player == null)
        {
            Debug.LogError("Aucun joueur trouvé pour le placement");
            return;
        }

        // Exécuter le code sur le thread principal
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            try
            {
                bool pawnPlaced = false;
                foreach (Transform pawn in player.transform)
                {
                    if (pawn.name == pawnName)
                    {
                        Debug.Log($"Pawn {pawn.name} found");
                        if (!pawn.gameObject.activeInHierarchy)
                        {
                            pawn.position = new Vector3(casePosition.x, casePosition.y + 1.0f, casePosition.z);
                            pawn.gameObject.SetActive(true);
                            Debug.Log($"Placed pawn {pawnName} at ({casePosition.x}, {casePosition.z})");
                            Rigidbody rb = pawn.GetComponent<Rigidbody>();
                            if (rb != null)
                            {
                                rb.useGravity = true; // Réactiver la gravité
                            }
                            Collider collider = pawn.GetComponent<Collider>();
                            if (collider != null)
                            {
                                collider.enabled = true; // Activer le collider
                            }
                            if (player == player1)
                            {
                                player1PawnsPlaced++;
                            }
                            else
                            {
                                player2PawnsPlaced++;
                            }
                            pawnPlaced = true;
                            break;
                        }
                        else
                        {
                            Debug.Log($"Pawn {pawn.name} is already active");
                        }
                    }
                }
                if (!pawnPlaced)
                {
                    Debug.LogError($"Pawn {pawnName} could not be placed");
                }

                // Vérifier si le placement initial est terminé
                if (player1PawnsPlaced == 2 && player2PawnsPlaced == 2)
                {
                    StartCoroutine(WaitForAllPawnsToSettle());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Erreur lors de l'accès aux propriétés du joueur: {ex.Message}");
            }
        });
    }


    IEnumerator WaitForAllPawnsToSettle()
    {
        while (!AreAllPawnsSettled())
        {
            yield return new WaitForSeconds(0.1f); // Attendre 0.1 seconde avant de vérifier à nouveau
        }

        initialPlacement = false;
        currentPlayer = 1;
        UpdateTurnIndicator();
        SetActivePlayerCollidersAndGravity();
    }




    Vector2Int ConvertPositionToIndices(Vector3 position)
    {
        int x = Mathf.RoundToInt((position.x / 2.2f) + 2);
        int z = Mathf.RoundToInt((position.z / 2.2f ) + 2);
        return new Vector2Int(x, z);
    }

    void HandlePlayerTurn()
    {
        if (playerTurnCompleted)
        {
            // Passe au joueur suivant
            currentPlayer = currentPlayer == 1 ? 2 : 1;
            playerTurnCompleted = false;
            UpdateTurnIndicator();
            SetActivePlayerCollidersAndGravity();

            if (currentPlayer == 2 && dropDownValue == 2)
            {
                Debug.Log("Player 2's turn. Waiting for AI instructions.");
                // Envoyer les actions du joueur 1 au serveur et attendre les instructions de l'IA
                SendPlayerActionsToServer();
                StartCoroutine(WaitForAIInstructions());
            }
            activePawn = null; // Réinitialise le pion actif
            activePlayerMovement = null; // Réinitialise le mouvement du joueur actif

            return;
        }

        GameObject currentPlayerObject = currentPlayer == 1 ? player1 : player2;
        HandleActivePlayer(currentPlayerObject);
    }

    void SendPlayerActionsToServer()
    {
        StartCoroutine(SendMoveAndBuildActions());
    }

    IEnumerator SendMoveAndBuildActions()
    {
        // Envoyer le mouvement du joueur 1 au serveur
        Vector2Int moveIndices = ConvertPositionToIndices(lastMovePosition);
        pythonClient.SendMessageToServer($"MOVE {activePlayerMovement.name} {moveIndices.x} {moveIndices.z}");
        Debug.Log($"Sent MOVE {activePlayerMovement.name} {moveIndices.x} {moveIndices.z}");

        // Attendre que le mouvement soit complété
        yield return new WaitUntil(() => moveCompleted);

        // Envoyer la construction du joueur 1 au serveur
        Vector2Int buildIndices = ConvertPositionToIndices(lastBuildPosition);
        pythonClient.SendMessageToServer($"BUILD {buildIndices.x} {buildIndices.z}");
        Debug.Log($"Sent BUILD {buildIndices.x} {buildIndices.z}");

        // Réinitialiser moveCompleted pour le prochain tour
        moveCompleted = false;
    }



    IEnumerator WaitForAIInstructions()
    {
        while (!pythonClient.HasReceivedAIInstructions())
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Traiter les instructions de l'IA reçues
        ProcessAIInstructions();
    }

    void ProcessAIInstructions()
    {
        string[] instructions = pythonClient.GetAIInstructions();
        foreach (string instruction in instructions)
        {
            string[] parts = instruction.Split();
            if (parts[0] == "AI" && parts[1] == "MOVE" && parts[5] == "BUILD")
            {
                string pawnName = parts[2] == "1" ? "Perso1" : "Perso2";
                int moveX = int.Parse(parts[3]);
                int moveZ = int.Parse(parts[4]);
                int buildX = int.Parse(parts[6]);
                int buildZ = int.Parse(parts[7]);

                //set pawn to false
                foreach (Transform pawn in player2.transform)
                {
                    if (pawn.name == pawnName)
                    {
                        pawn.gameObject.SetActive(false);
                    }
                }

                // Placer le pion à la nouvelle position
                PlacePawnFromServer("Player2", pawnName, moveX, moveZ);

                // Construire à la position spécifiée
                Vector3 buildPosition = ConvertIndicesToPosition(buildX, buildZ);
                buildingManager.Build(buildPosition);
            }
            else
            {
                Debug.LogWarning("Unknown AI instruction format.");
            }
        }

        // Reprendre le tour du joueur 1
        currentPlayer = 1;
        UpdateTurnIndicator();
        SetActivePlayerCollidersAndGravity();
    }




    void HandleActivePlayer(GameObject currentPlayerObject)
    {
        if (activePawn == null)
        {
            foreach (Transform pawn in currentPlayerObject.transform)
            {
                if (pawn.CompareTag("Player"))
                {
                    PlayerMovement movement = pawn.GetComponent<PlayerMovement>();
                    if (movement != null && movement.selected)
                    {
                        activePawn = pawn.gameObject; // Définit le pion actif
                        activePlayerMovement = movement;
                        break;
                    }
                }
            }
        }

        if (activePawn != null && activePlayerMovement != null)
        {
            if (activePlayerMovement.TurnCompleted)
            {
                activePlayerMovement.ResetTurn();
                //activePawn = null;
                //activePlayerMovement = null;
                playerTurnCompleted = true; // Signaler la fin du tour
                if (CheckWinCondition(currentPlayerObject))
                {
                    EndGame(currentPlayer);
                }
            }
        }
    }

    void SetActivePlayerCollidersAndGravity()
    {
        GameObject activePlayer = currentPlayer == 1 ? player1 : player2;
        GameObject inactivePlayer = currentPlayer == 1 ? player2 : player1;

        SetColliderAndGravity(activePlayer, true);
        SetColliderAndGravity(inactivePlayer, false);
    }

    bool IsOccupied(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, 0.1f);
        foreach (Collider collider in colliders)
        {
            if (collider.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    bool CheckWinCondition(GameObject player)
    {
        foreach (Transform pawn in player.transform)
        {
            if (pawn.CompareTag("Player"))
            {
                if (pawn.position.y >= 4.0f) // Niveau 3 atteint
                {
                    return true;
                }
            }
        }
        return false;
    }

    void EndGame(int winningPlayer)
    {
        gameStarted = false;
        turnIndicator.text = "Joueur " + winningPlayer + " a gagné";
        turnIndicator.gameObject.SetActive(true);
        startButton.gameObject.SetActive(true);
        DisableAllPawnsCollidersAndGravity();
        ResetGame();
    }

    void UpdateTurnIndicator()
    {
        turnIndicator.text = "Joueur " + currentPlayer;
        StartCoroutine(ShowTurnIndicatorTemporarily(3.0f));
    }

    IEnumerator ShowTurnIndicatorTemporarily(float duration)
    {
        turnIndicator.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        turnIndicator.gameObject.SetActive(false);
    }

    void SetPawnsVisibile(GameObject player, bool visible)
    {
        foreach (Transform pawn in player.transform)
        {
            pawn.gameObject.SetActive(visible);
        }
    }

    void SetColliderAndGravity(GameObject player, bool active)
    {
        foreach (Transform pawn in player.transform)
        {
            Collider collider = pawn.GetComponent<Collider>();
            Rigidbody rb = pawn.GetComponent<Rigidbody>();
            if (collider != null)
            {
                collider.enabled = active;
            }
            if (rb != null)
            {
                rb.useGravity = active;
            }
        }
    }

    void DisableAllPawnsCollidersAndGravity()
    {
        SetColliderAndGravity(player1, false);
        SetColliderAndGravity(player2, false);
    }

    void ResetGame()
    {
        // Réinitialiser les variables de jeu
        currentPlayer = 1;
        initialPlacement = true;
        player1PawnsPlaced = 0;
        player2PawnsPlaced = 0;
        playerTurnCompleted = false;
        activePawn = null;
        activePlayerMovement = null;

        // Rendre les pions invisibles et désactiver la gravité
        SetPawnsVisibile(player1, false);
        SetPawnsVisibile(player2, false);

        EnableAllPawnsCollidersAndGravity();

        // Réinitialiser les cases et détruire tous les bâtiments
        if (activePlayerMovement != null)
        {
            Debug.Log("Resetting active player movement");
            activePlayerMovement.ResetCase();
        }
        if (buildingManager != null)
        {
            buildingManager.DestroyAllBuildings();
        }
    }

    void EnableAllPawnsCollidersAndGravity()
    {
        SetColliderAndGravity(player1, true);
        SetColliderAndGravity(player2, true);
    }

    bool AreAllPawnsSettled()
{
    foreach (Transform pawn in player1.transform)
    {
        if (pawn.gameObject.activeInHierarchy && Mathf.Abs(pawn.position.y) > 0.1f)
        {
            return false;
        }
    }

    foreach (Transform pawn in player2.transform)
    {
        if (pawn.gameObject.activeInHierarchy && Mathf.Abs(pawn.position.y) > 0.1f)
        {
            return false;
        }
    }

    return true;
}

}
