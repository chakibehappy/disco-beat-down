using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyHitbox : MonoBehaviour
{
    private Collider hitboxCollider;
    public EnemyScript enemyScript;

    void Start()
    {
        hitboxCollider = GetComponent<Collider>();
        hitboxCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (enemyScript != null)
            {
                enemyScript.HitEvent();
            }
            hitboxCollider.enabled = false;
        }
    }
}