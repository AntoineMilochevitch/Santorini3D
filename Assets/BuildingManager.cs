using System.Collections.Generic;
using UnityEngine;

public struct Vector2Int
{
    public int x;
    public int z;

    public Vector2Int(int x, int z)
    {
        this.x = x;
        this.z = z;
    }
}

public class BuildingManager : MonoBehaviour
{
    public GameObject level1Prefab;
    public GameObject level3Prefab;
    public GameObject level4Prefab;

    private Dictionary<Vector2Int, int> buildingLevels = new Dictionary<Vector2Int, int>();
    private Vector3 lastBuildPosition; 

    public void Build(Vector3 position)
    {
        Vector2Int pos2D = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));

        if (!buildingLevels.ContainsKey(pos2D))
        {
            buildingLevels[pos2D] = 0;
        }

        int currentLevel = buildingLevels[pos2D];

        if (currentLevel < 4)
        {
            currentLevel++;
            buildingLevels[pos2D] = currentLevel;

            GameObject buildingPrefab = null;
            if (currentLevel == 1 || currentLevel == 2)
            {
                buildingPrefab = level1Prefab;
            }
            else if (currentLevel == 3)
            {
                buildingPrefab = level3Prefab;
            }
            else if (currentLevel == 4)
            {
                buildingPrefab = level4Prefab;
            }

            if (buildingPrefab != null)
            {
                Instantiate(buildingPrefab, position, Quaternion.identity);
                AdjustCasePosition(position, currentLevel);
                lastBuildPosition = position; // Mettre à jour la position du dernier bâtiment construit
            }
        }
    }

    public int GetBuildingLevel(Vector3 position)
    {
        Vector2Int pos2D = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));

        if (buildingLevels.ContainsKey(pos2D))
        {
            return buildingLevels[pos2D];
        }
        return 0;
    }

    private void AdjustCasePosition(Vector3 position, int level)
    {
        // Trouver la case à la position donnée
        GameObject caseObject = null;
        foreach (var caseObj in GameObject.FindGameObjectsWithTag("Case"))
        {
            if (Mathf.RoundToInt(caseObj.transform.position.x) == Mathf.RoundToInt(position.x) && Mathf.RoundToInt(caseObj.transform.position.z) == Mathf.RoundToInt(position.z))
            {
                caseObject = caseObj;
                break;
            }
        }

        // Ajuster la position de la case en fonction du niveau du bâtiment
        if (caseObject != null)
        {
            caseObject.transform.position = new Vector3(position.x, level * 2.0f, position.z); 
        }
    }

    public void DestroyAllBuildings()
    {
        foreach (var building in GameObject.FindGameObjectsWithTag("Building"))
        {
            Destroy(building);
        }
        buildingLevels.Clear();
    }

    public Vector3 GetLastBuildPosition() 
    {
        return lastBuildPosition;
    }
}
