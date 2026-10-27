using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Logout : MonoBehaviour
{
    [SerializeField] private Button btnLogout;
    void OnEnable()
    {
        if (btnLogout != null)
            btnLogout.onClick.AddListener(OnLogoutClicked);
    }

    void OnDisable()
    {
        if (btnLogout != null)
            btnLogout.onClick.RemoveListener(OnLogoutClicked);
    }

    private void OnLogoutClicked()
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null)
            return;

        manager.LogoutParent();
        if (manager.CurrentUser == null)
        {
            Debug.Log("User logged out successfully.");
            SceneManager.LoadScene("Menu");
        }
        else
        {
            Debug.LogWarning("Logout failed. User is still logged in.");
        }
    }
}
