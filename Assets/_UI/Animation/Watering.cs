using UnityEngine;

public class Watering : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem waterParticle;

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (waterParticle == null)
        {
            waterParticle = GetComponentInChildren<ParticleSystem>();
        }
    }

    public void StartWatering()
    {
        animator.SetBool("isWatering", true);
    }

    public void StopWatering()
    {
        animator.SetBool("isWatering", false);
    }

    public void StartParticle()
    {
        waterParticle.Play();
    }
    public void StopParticle()
    {
        waterParticle.Stop();
    }
}
