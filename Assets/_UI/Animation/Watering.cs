using UnityEngine;

public class Watering : MonoBehaviour
{
    private const int WaterCost = 2;
    private const string AlreadyCheckedInMessage = "Bé ơi, nước không đủ để tưới nữa rồi! Ngày mai bé quay lại điểm danh để nhận thêm nước nhé!";
    private const string NotCheckedInMessage = "Bé ơi, mình chưa đủ nước để tưới cây! Bé hãy điểm danh hôm nay để nhận thêm nước nhé!";
    private static readonly int IsWateringParameter = Animator.StringToHash("isWatering");

    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem waterParticle;
    [SerializeField] private TextToSpeech textToSpeech;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (waterParticle == null)
        {
            waterParticle = GetComponentInChildren<ParticleSystem>();
        }

        if (textToSpeech == null)
        {
            textToSpeech = GetComponent<TextToSpeech>();
            if (textToSpeech == null)
                textToSpeech = FindFirstObjectByType<TextToSpeech>();
        }
    }

    public void StartWatering()
    {
        if (!isActiveAndEnabled)
            return;

        if (animator == null)
        {
            Debug.LogWarning("Watering requires an Animator before water can be spent.", this);
            return;
        }

        // Repeated clicks during the same watering animation must not charge again.
        if (animator.GetBool(IsWateringParameter))
            return;

        // Check before deducting: exactly 2 drops is enough, 0 or 1 is not.
        if (!AttendanceCheck.TrySpendWater(WaterCost))
        {
            if (textToSpeech == null)
                textToSpeech = FindFirstObjectByType<TextToSpeech>();

            if (textToSpeech != null)
                textToSpeech.Speak(AttendanceCheck.CanCheckInToday ? NotCheckedInMessage : AlreadyCheckedInMessage);
            else
                Debug.LogWarning("Assign TextToSpeech to Watering to announce insufficient water.", this);

            return;
        }

        animator.SetBool(IsWateringParameter, true);
        Achievements.RecordWatering(WaterCost);
    }

    public void StopWatering()
    {
        if (animator != null)
            animator.SetBool(IsWateringParameter, false);
    }

    public void StartParticle()
    {
        if (waterParticle != null)
            waterParticle.Play();
    }
    public void StopParticle()
    {
        if (waterParticle != null)
            waterParticle.Stop();
    }
}
