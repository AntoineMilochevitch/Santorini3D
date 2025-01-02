using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    private GameObject[] cases;
    private List<GameObject> indicators = new List<GameObject>();
    public GameObject indicatorPrefab;
    public bool selected = false;
    private BuildingManager buildingManager;
    private bool isMoving = false;
    private bool hasMoved = false;
    private bool hasBuilt = false;

    public bool TurnCompleted { get; private set; } // Propriété pour indiquer que le tour est terminé
    public Vector3 LastBuildPosition { get; private set; } // Propriété pour récupérer la position du dernier bâtiment construit
    public Vector3 LastMovePosition { get; private set; } // Propriété pour récupérer la position du dernier déplacement
    public string PlayerName { get; set; } // Propriété pour définir le nom du joueur

    public delegate void PlayerActionHandler(string playerName, Vector3 movePosition, Vector3 buildPosition);
    public static event PlayerActionHandler OnPlayerAction;


    void Start()
    {
        // R�cup�rer toutes les cases du plateau
        cases = GameObject.FindGameObjectsWithTag("Case");

        // D�sactiver les collisions entre les personnages
        foreach (var character in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (character != gameObject)
            {
                Physics.IgnoreCollision(character.GetComponent<Collider>(), GetComponent<Collider>());
            }
        }

        // R�cup�rer le BuildingManager
        buildingManager = Object.FindFirstObjectByType<BuildingManager>();
    }

    void Update()
    {
        // D�tecter le clic de la souris
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Si un autre personnage est cliqu� et que le personnage actuel n'a pas encore boug�
                if (hit.transform.CompareTag("Player") && !hasMoved)
                {
                    // D�s�lectionner tous les personnages
                    DeselectAllCharacters();

                    // Activer la s�lection du nouveau personnage
                    hit.transform.GetComponent<PlayerMovement>().SelectCharacter();
                }
                // Si une case ou un indicateur est cliqu� et que ce personnage est s�lectionn�
                else if ((hit.transform.CompareTag("Case") || hit.transform.CompareTag("Indicateur")) && selected)
                {
                    Vector3 targetPosition = hit.transform.position;

                    // Si le personnage est en mode déplacement
                    if (isMoving)
                    {
                        if (IsWithinRangeHorizontal(transform.position, targetPosition) && IsWithinRangeVertical(transform.position, targetPosition) && CanMoveTo(targetPosition))
                        {
                            transform.position = targetPosition;
                            LastMovePosition = targetPosition; // Mettre à jour la position du dernier déplacement
                            isMoving = false; // Fin du déplacement
                            hasMoved = true; // Marquer que le personnage a bougé
                            selected = true; // Garder le personnage sélectionné
                            DestroyIndicators(); // Supprimer les indicateurs après le déplacement

                            ShowConstructionIndicators(); // Afficher les indicateurs pour la construction
                        }
                    }
                    // Si le personnage a déjà bougé et est en mode construction
                    else if (hasMoved)
                    {
                        if (IsWithinRangeHorizontal(transform.position, targetPosition) && !IsPlayerOnPosition(targetPosition))
                        {
                            buildingManager.Build(targetPosition);
                            LastBuildPosition = targetPosition; // Mettre à jour la position du dernier bâtiment construit
                            hasBuilt = true; // Marquer que le personnage a construit
                            DestroyIndicators(); // Supprimer les indicateurs après la construction
                            CompleteTurn(); // Indiquer que le tour est terminé

                            // Notifier GameManager de l'action du joueur
                            OnPlayerAction?.Invoke(PlayerName, LastMovePosition, LastBuildPosition);
                        }
                    }
                }
            }
        }
    }


    // V�rifier si la case est dans la port�e de d�placement
    bool IsWithinRangeHorizontal(Vector3 characterPosition, Vector3 targetPosition)
    {
        float horizontalDistance = Vector2.Distance(new Vector2(characterPosition.x, characterPosition.z), new Vector2(targetPosition.x, targetPosition.z));

        return horizontalDistance <= 4f;
    }

    // V�rifier si la case est dans la port�e de d�placement
    bool IsWithinRangeVertical(Vector3 characterPosition, Vector3 targetPosition)
    {
        float verticalDistance = Mathf.Abs(characterPosition.y - targetPosition.y);
        return verticalDistance <= 4.0f;
    }

    // Afficher les indicateurs sur les cases disponibles pour le d�placement
    void ShowMovementIndicators()
    {
        foreach (var caseObj in cases)
        {
            Vector3 casePosition = caseObj.transform.position;
            if (IsWithinRangeHorizontal(transform.position, casePosition) && IsWithinRangeVertical(transform.position, casePosition) && CanMoveTo(casePosition) && !IsPlayerOnPosition(casePosition))
            {
                Vector3 indicatorPosition = casePosition + new Vector3(0, 0.05f, 0);
                GameObject indicator = Instantiate(indicatorPrefab, indicatorPosition, Quaternion.identity);
                indicators.Add(indicator);
            }
        }
    }

    // Afficher les indicateurs sur les cases disponibles pour la construction
    void ShowConstructionIndicators()
    {
        foreach (var caseObj in cases)
        {
            Vector3 casePosition = caseObj.transform.position;
            if (IsWithinRangeHorizontal(transform.position, casePosition) && !IsPlayerOnPosition(casePosition))
            {
                Vector3 indicatorPosition = casePosition + new Vector3(0, 0.05f, 0);
                GameObject indicator = Instantiate(indicatorPrefab, indicatorPosition, Quaternion.identity);
                Collider indicatorCollider = indicator.GetComponent<Collider>();
                if (indicatorCollider != null)
                {
                    indicatorCollider.enabled = false;
                }
                indicators.Add(indicator);
                
            }
        }
    }

    // Supprimer tous les indicateurs
    void DestroyIndicators()
    {
        foreach (var indicator in indicators)
        {
            Destroy(indicator);
        }
        indicators.Clear();
    }

    // V�rifier si le joueur peut se d�placer sur la case cible
    bool CanMoveTo(Vector3 targetPosition)
    {
        int currentLevel = buildingManager.GetBuildingLevel(transform.position);
        int targetLevel = buildingManager.GetBuildingLevel(targetPosition);
        return targetLevel <= currentLevel + 1;
    }

    // M�thodes pour g�rer la s�lection du personnage
    public void SelectCharacter()
    {
        selected = true;
        isMoving = true; // Commencer par le d�placement
        DisableGravityForAllPlayersExcept(gameObject); // D�sactiver la gravit� pour tous les personnages sauf celui s�lectionn�
        DisableCollidersForAllPlayersExcept(gameObject); // D�sactiver les colliders pour tous les personnages sauf celui s�lectionn�
        ShowMovementIndicators(); // Afficher les indicateurs pour le d�placement
    }

    public void DeselectCharacter()
    {
        selected = false;
        hasMoved = false;
        hasBuilt = false; // R�initialiser la construction
        EnableCollidersForAllPlayers(); // R�activer les colliders pour tous les personnages
        EnableGravityForAllPlayers(); // R�activer la gravit� pour tous les personnages
        DestroyIndicators(); // Supprimer les indicateurs
    }

    public void CompleteTurn()
    {
        StartCoroutine(WaitForMovementToFinish());
    }

    // D�s�lectionner tous les personnages
    void DeselectAllCharacters()
    {
        foreach (var character in GameObject.FindGameObjectsWithTag("Player"))
        {
            character.GetComponent<PlayerMovement>().DeselectCharacter();
        }
    }

    // V�rifier s'il y a un joueur sur la position donn�e
    private bool IsPlayerOnPosition(Vector3 position)
    {
        // Activer temporairement les colliders
        EnableCollidersForAllPlayers();

        Collider[] colliders = Physics.OverlapSphere(position, 0.1f);
        bool playerFound = false;
        foreach (Collider collider in colliders)
        {
            if (collider.CompareTag("Player"))
            {
                playerFound = true;
                break;
            }
        }

        // D�sactiver les colliders apr�s v�rification
        DisableCollidersForAllPlayersExcept(gameObject);
        return playerFound;
    }

    // D�sactiver la gravit� pour tous les personnages sauf celui s�lectionn�
    void DisableGravityForAllPlayersExcept(GameObject selectedPlayer)
    {
        foreach (var character in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (character != selectedPlayer)
            {
                Rigidbody rb = character.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.useGravity = false;
                }
            }
        }
    }

    // D�sactiver la gravit� pour tous les personnages
    void DisableGravityForAllPlayers()
    {
        foreach (var character in GameObject.FindGameObjectsWithTag("Player"))
        {
            Rigidbody rb = character.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
            }
        }
    }

    // R�activer la gravit� pour tous les personnages
    void EnableGravityForAllPlayers()
    {
        foreach (var character in GameObject.FindGameObjectsWithTag("Player"))
        {
            Rigidbody rb = character.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = true;
            }
        }
    }

    // D�sactiver les colliders pour tous les personnages sauf celui s�lectionn�
    void DisableCollidersForAllPlayersExcept(GameObject selectedPlayer)
    {
        foreach (var character in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (character != selectedPlayer)
            {
                Collider col = character.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                }
            }
        }
    }

    // R�activer les colliders pour tous les personnages
    void EnableCollidersForAllPlayers()
    {
        foreach (var character in GameObject.FindGameObjectsWithTag("Player"))
        {
            Collider col = character.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }
        }
    }

    public void ResetTurn()
    {
        selected = false;
        isMoving = false;
        hasMoved = false;
        hasBuilt = false;
        TurnCompleted = false;
    }

    public void ResetCase()
    {
        cases = GameObject.FindGameObjectsWithTag("Case");
        Debug.Log(cases.Length);
        foreach ( var casObj in cases)
        {
            Debug.Log(casObj.transform.position);
            casObj.transform.position = new Vector3(casObj.transform.position.x, 0, casObj.transform.position.z);
            Debug.Log(casObj.transform.position);
        }
    }

    private IEnumerator WaitForMovementToFinish()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        while (rb != null && rb.linearVelocity.magnitude > 0.1f)
        {
            yield return null;
        }
        TurnCompleted = true;
        DeselectCharacter();
    }

}
