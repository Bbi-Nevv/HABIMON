using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Logout : MonoBehaviour
{
    [SerializeField] private Button btnLogout;
    void OnEnable()
    {
        btnLogout.onClick.AddListener(() => 
        {
            FirebaseManager.Instance.LogoutParent();
            if (FirebaseManager.Instance.CurrentUser == null)
            {
                Debug.Log("User logged out successfully.");
                SceneManager.LoadScene("Menu");
            }
            else
            {
                Debug.LogWarning("Logout failed. User is still logged in.");
            }
        });
    }
    void OnDestroy()
    {
        btnLogout.onClick.RemoveAllListeners();
    }
}
