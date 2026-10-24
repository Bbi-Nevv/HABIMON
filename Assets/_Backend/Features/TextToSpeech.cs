using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;
public class TextToSpeech : MonoBehaviour
{
	[TextArea(3, 10)]
	[SerializeField] private List<string> speeches = new List<string>();
	[SerializeField] private TMP_Text textMeshPro;
	[SerializeField] private GameObject displayBoxchat;
	[SerializeField] private float timeDisplayBoxchat;
	private Coroutine displayCoroutine;

	void Start()
	{
		if (displayBoxchat != null)
			displayBoxchat.SetActive(false);
	}

	private void OnDisable()
	{
		if (displayCoroutine != null)
		{
			StopCoroutine(displayCoroutine);
			displayCoroutine = null;
		}
		if (displayBoxchat != null)
			displayBoxchat.SetActive(false);
		SetText(string.Empty);
#if UNITY_ANDROID && !UNITY_EDITOR
		pendingSpeeches.Clear();
		if (textToSpeech != null && isInitialized)
			textToSpeech.Call("stop");
#endif
	}

#if UNITY_ANDROID && !UNITY_EDITOR
	private const int QueueFlush = 0;
	private const int QueueAdd = 1;
	private const int Success = 0;
	private const int MaxPendingSpeeches = 16;

	private readonly Queue<string> pendingSpeeches = new Queue<string>();
	private TextToSpeechInitListener initListener;
	private AndroidJavaObject textToSpeech;
	private AndroidJavaObject speakParameters;
	private bool isInitialized;
	private bool canSpeakVietnamese;

	private void Awake()
	{
		initListener = new TextToSpeechInitListener(this);
		speakParameters = new AndroidJavaObject("android.os.Bundle");
		using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
		using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
		{
			textToSpeech = new AndroidJavaObject(
				"android.speech.tts.TextToSpeech",
				activity,
				initListener);
		}
	}

	private void Update()
	{
		if (!isInitialized)
		{
			return;
		}
		if (!canSpeakVietnamese)
		{
			pendingSpeeches.Clear();
			return;
		}

		var queueMode = QueueFlush;
		while (pendingSpeeches.Count > 0)
		{
			Play(pendingSpeeches.Dequeue(), queueMode);
			queueMode = QueueAdd;
		}
	}

	private void OnDestroy()
	{
		if (textToSpeech != null)
		{
			textToSpeech.Call("shutdown");
			textToSpeech.Dispose();
		}
		speakParameters?.Dispose();
		initListener = null;
	}

	private void OnInitialized(int status)
	{
		isInitialized = status == Success;
		canSpeakVietnamese = false;
		if (!isInitialized || textToSpeech == null)
		{
			pendingSpeeches.Clear();
			return;
		}

		try
		{
			using (var vietnameseLocale = new AndroidJavaObject("java.util.Locale", "vi", "VN"))
			{
				int languageStatus = textToSpeech.Call<int>("setLanguage", vietnameseLocale);
				canSpeakVietnamese = languageStatus >= 0;
			}
			if (!canSpeakVietnamese)
				Debug.LogWarning("Thiết bị chưa có dữ liệu giọng nói tiếng Việt; ứng dụng vẫn hiển thị câu thoại.", this);
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning($"Không thể chọn giọng tiếng Việt: {ex.Message}", this);
		}
	}

	private void Play(string text, int queueMode)
	{
		textToSpeech.Call<int>("speak", text, queueMode, speakParameters, string.Empty);
	}

	private sealed class TextToSpeechInitListener : AndroidJavaProxy
	{
		private readonly TextToSpeech owner;

		public TextToSpeechInitListener(TextToSpeech owner)
			: base("android.speech.tts.TextToSpeech$OnInitListener")
		{
			this.owner = owner;
		}

		public void onInit(int status)
		{
			owner.OnInitialized(status);
		}
	}
#endif

	public void Speak()
	{
		if (speeches == null || speeches.Count == 0)
		{
			return;
		}

		var text = speeches[Random.Range(0, speeches.Count)];
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		ShowMessage(text);

#if UNITY_ANDROID && !UNITY_EDITOR
		if (isInitialized && canSpeakVietnamese)
			Play(text, QueueFlush);
		else if (!isInitialized)
			QueuePendingSpeech(text);
#else
		Debug.LogWarning("TextToSpeech is available only in an Android player build.", this);
#endif
	}

	public void Speak(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		ShowMessage(text);

#if UNITY_ANDROID && !UNITY_EDITOR
		if (isInitialized && canSpeakVietnamese)
			Play(text, QueueFlush);
		else if (!isInitialized)
			QueuePendingSpeech(text);
#else
		Debug.LogWarning("TextToSpeech is available only in an Android player build.", this);
#endif
	}

	public void SetText(string text)
	{
		if (textMeshPro != null)
		{
			textMeshPro.text = text;
		}
	}

	private void ShowMessage(string text)
	{
		if (displayBoxchat == null)
			return;

		if (displayCoroutine != null)
			StopCoroutine(displayCoroutine);
		displayCoroutine = StartCoroutine(DisplayBoxchat(text));
	}

	private void QueuePendingSpeech(string text)
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		if (pendingSpeeches.Count >= MaxPendingSpeeches)
			pendingSpeeches.Dequeue();
		pendingSpeeches.Enqueue(text);
#endif
	}

	IEnumerator DisplayBoxchat(string text)
	{
		GameObject box = displayBoxchat;
		box.SetActive(true);
		SetText(text);
		yield return new WaitForSeconds(timeDisplayBoxchat);
		if (this != null && box != null && displayCoroutine != null)
		{
			box.SetActive(false);
			SetText(string.Empty);
			displayCoroutine = null;
		}
	}
}
