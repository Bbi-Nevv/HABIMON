
using UnityEngine;
using System;

public class AffiliateManager : MonoBehaviour
{
    [SerializeField] private string affiliateLink;

    public void OpenProduct()
    {
        if (!Uri.TryCreate(
                affiliateLink,
                UriKind.Absolute,
                out Uri uri))
        {
            Debug.LogWarning("Affiliate URL không hợp lệ");
            return;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            Debug.LogWarning("Chỉ chấp nhận HTTPS");
            return;
        }

        Application.OpenURL(affiliateLink);
    }
}
