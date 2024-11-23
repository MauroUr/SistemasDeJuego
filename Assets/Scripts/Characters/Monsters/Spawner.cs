using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    private List<EnemyFactory> allFactories;
    [SerializeField] int enemiesToSpawn;
    [SerializeField] private List<GameObject> floorList = new List<GameObject>();
    private List<Transform> _floorSquares = new List<Transform>();
    private void Start()
    {
        StartCoroutine(GetEnemyFactories());
        
        //_floorSquares.AddRange(floorList[0].GetComponentsInChildren<Transform>());
        //_floorSquares.AddRange(floorList[1].GetComponentsInChildren<Transform>());
        
        //floorList.Clear();
    }

    private IEnumerator GetEnemyFactories()
    {
        while (WindowsManager.instance == null)
            yield return null;

        EnemyFactoryService factoriesService = null;
        while (factoriesService == null)
        {
            factoriesService = ServiceLocator.instance.GetService<EnemyFactoryService>(typeof(EnemyFactoryService));
            yield return null;
        }
        allFactories = factoriesService.GetFactories();
    }

    public void Spawn()
    {
        SpawnRandomEnemies(enemiesToSpawn);
    }
    private void SpawnRandomEnemies(int quantity)
    {
        for (int i = 0;i< quantity;i++)
        {
            Enemy newEnemy = allFactories[Random.Range(0, allFactories.Count)].SpawnEnemy();
            Relocate(newEnemy);
        }
    }

    private void Relocate(Enemy newEnemy)
    {
        MeshRenderer selectedRenderer = floorList[Random.Range(0, floorList.Count)].GetComponentInChildren<MeshRenderer>();

        if (selectedRenderer == null)
        {
            Debug.LogError("Floor object missing!");
            return;
        }

        Bounds bounds = selectedRenderer.bounds;
        Vector3 randomPosition = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            0.6f,
            Random.Range(bounds.min.z, bounds.max.z)
        );

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(randomPosition, out hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
            newEnemy.transform.position = hit.position;
        else
            Debug.LogWarning("Failed to relocate enemy to a valid NavMesh position!");
    }

}
