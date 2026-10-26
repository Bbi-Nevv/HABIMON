using UnityEngine;

[CreateAssetMenu(fileName = "HABIMONChatbotSettings", menuName = "HABIMON/Chatbot Settings")]
public sealed class ChatbotRuntimeSettings : ScriptableObject
{
    [SerializeField] private string baseUrl = "https://habimon-chat-demo-261017-0ed006.azurewebsites.net";
    [SerializeField] private string plan = "free";
    [SerializeField] private int historyDays = 30;
    [SerializeField] private bool allowInsecureLocalhostForDevelopment;

    public void Configure(string url, string selectedPlan, int selectedHistoryDays, bool allowLocalHttp)
    {
        baseUrl = url;
        plan = selectedPlan;
        historyDays = selectedHistoryDays;
        allowInsecureLocalhostForDevelopment = allowLocalHttp;
    }

    public string BaseUrl => baseUrl;
    public string Plan => plan == "plus" || plan == "premium" ? plan : "free";
    public int HistoryDays => Mathf.Clamp(historyDays, 1, 90);
    public bool AllowInsecureLocalhostForDevelopment => allowInsecureLocalhostForDevelopment;
}
