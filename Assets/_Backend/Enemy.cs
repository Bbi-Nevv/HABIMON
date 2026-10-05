using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 1000f;
    private float currentHealth;
    
    [Header("UI")]
    public Scrollbar healthBar;
    public TextMeshProUGUI HP;

    [Header("Spawn")]
    public GameObject enemyPrefab;
    public Transform spawnPoint;

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        if (currentHealth < 0)
            currentHealth = 0;

        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void UpdateHealthBar()
    {
        healthBar.size = currentHealth / maxHealth;
        // HP.text = currentHealth + "/" + maxHealth;
    }

    void Die()
    {
        Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);
        Destroy(gameObject);
    }
}