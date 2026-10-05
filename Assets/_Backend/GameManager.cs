using UnityEngine;

public class GameManager : MonoBehaviour
{
    public EnemyHealth currentEnemy;
    public float damage = 100f;
    
    public void AttackEnemy()
    {
        if (currentEnemy != null)
        {
            currentEnemy.TakeDamage(damage);
        }
    }
}