using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    private EnemyScript[] enemies;
    public EnemyStruct[] allEnemies;
    private List<int> enemyIndexes;

    [Header("Main AI Loop - Settings")]
    private Coroutine AI_Loop_Coroutine;

    public int aliveEnemyCount;

    [Header("Gang Up Settings")]
    [Tooltip("Maximum enemies that can attack you at the exact same time")]
    public int maxSimultaneousAttackers = 3;

    void Start()
    {
        enemies = GetComponentsInChildren<EnemyScript>();
        allEnemies = new EnemyStruct[enemies.Length];

        for (int i = 0; i < allEnemies.Length; i++)
        {
            allEnemies[i].enemyScript = enemies[i];
            allEnemies[i].enemyAvailability = true;
        }

        StartAI();
    }

    public void StartAI()
    {
        AI_Loop_Coroutine = StartCoroutine(AI_Loop());
    }

    IEnumerator AI_Loop()
    {
        if (AliveEnemyCount() == 0) yield break;

        yield return new WaitForSeconds(Random.Range(.5f, 1.5f));

        int attackersCount = Random.Range(1, maxSimultaneousAttackers + 1);
        List<EnemyScript> attackingEnemies = new List<EnemyScript>();

        for (int i = 0; i < attackersCount; i++)
        {
            EnemyScript e = RandomEnemy();
            if (e != null && !attackingEnemies.Contains(e))
            {
                attackingEnemies.Add(e);
            }
        }

        if (attackingEnemies.Count == 0) yield break;

        foreach (EnemyScript enemy in attackingEnemies)
        {
            if (!enemy.IsRetreating() && !enemy.IsLockedTarget() && !enemy.IsStunned())
            {
                enemy.SetAttack();
            }
        }

        // Wait until everyone is done swinging (or times out!)
        yield return new WaitUntil(() => !AnEnemyIsPreparingAttack());

        foreach (EnemyScript enemy in attackingEnemies)
        {
            if (enemy != null && enemy.IsAttackable() && enemy.isActiveAndEnabled)
            {
                enemy.SetRetreat();
            }
        }

        yield return new WaitForSeconds(Random.Range(0f, .5f));

        if (AliveEnemyCount() > 0)
        {
            AI_Loop_Coroutine = StartCoroutine(AI_Loop());
        }
    }

    public EnemyScript RandomEnemy()
    {
        enemyIndexes = new List<int>();

        for (int i = 0; i < allEnemies.Length; i++)
        {
            if (allEnemies[i].enemyAvailability && allEnemies[i].enemyScript.isActiveAndEnabled)
            {
                enemyIndexes.Add(i);
            }
        }

        if (enemyIndexes.Count == 0) return null;

        int randomIndex = Random.Range(0, enemyIndexes.Count);
        return allEnemies[enemyIndexes[randomIndex]].enemyScript;
    }

    public int AvailableEnemyCount()
    {
        int count = 0;
        for (int i = 0; i < allEnemies.Length; i++)
        {
            if (allEnemies[i].enemyAvailability) count++;
        }
        return count;
    }

    public bool AnEnemyIsPreparingAttack()
    {
        foreach (EnemyStruct enemyStruct in allEnemies)
        {
            // CRITICAL FIX: Ignore dead or disabled enemies so the loop never freezes!
            if (enemyStruct.enemyScript != null && enemyStruct.enemyScript.isActiveAndEnabled)
            {
                if (enemyStruct.enemyScript.IsPreparingAttack()) return true;
            }
        }
        return false;
    }

    public int AliveEnemyCount()
    {
        int count = 0;
        for (int i = 0; i < allEnemies.Length; i++)
        {
            if (allEnemies[i].enemyScript.isActiveAndEnabled) count++;
        }
        aliveEnemyCount = count;
        return count;
    }

    public void SetEnemyAvailiability(EnemyScript enemy, bool state)
    {
        for (int i = 0; i < allEnemies.Length; i++)
        {
            if (allEnemies[i].enemyScript == enemy)
                allEnemies[i].enemyAvailability = state;
        }

        if (FindObjectOfType<EnemyDetection>().CurrentTarget() == enemy)
            FindObjectOfType<EnemyDetection>().SetCurrentTarget(null);
    }
}

[System.Serializable]
public struct EnemyStruct
{
    public EnemyScript enemyScript;
    public bool enemyAvailability;
}