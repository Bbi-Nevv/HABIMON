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

	void Start()
	{
		displayBoxchat.SetActive(false);
	}
#if UNITY_ANDROID && !UNITY_EDITOR
	private const int QueueFlush = 0;
	private const int QueueAdd = 1;
	private const int Success = 0;

	private readonly Queue<string> pendingSpeeches = new Queue<string>();
	private TextToSpeechInitListener initListener;
	private AndroidJavaObject textToSpeech;
	private AndroidJavaObject speakParameters;
	private bool isInitialized;

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

		var queueMode = QueueFlush;
		while (pendingSpeeches.Count > 0)
		{
			Play(pendingSpeeches.Dequeue(), queueMode);
			queueMode = QueueAdd;
		}
	}

	private void OnDestroy()
	{
		if (textToSpeech == null)
		{
			return;
		}

		textToSpeech.Call("shutdown");
		textToSpeech.Dispose();
		speakParameters.Dispose();
		initListener = null;
	}

	private void OnInitialized(int status)
	{
		isInitialized = status == Success;
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

		StartCoroutine(DisplayBoxchat(text));

#if UNITY_ANDROID && !UNITY_EDITOR
		if (isInitialized)
		{
			Play(text, QueueFlush);

		}
		else
		{
			pendingSpeeches.Enqueue(text);
		}
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
		StartCoroutine(DisplayBoxchat(text));

#if UNITY_ANDROID && !UNITY_EDITOR
		if (isInitialized)
		{
			Play(text, QueueFlush);
		}
		else
		{
			pendingSpeeches.Enqueue(text);
		}
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
	IEnumerator DisplayBoxchat(string text)
	{
		if (displayBoxchat != null)
		{
			displayBoxchat.SetActive(true);
			SetText(text);
			yield return new WaitForSeconds(timeDisplayBoxchat);
			displayBoxchat.SetActive(false);
			SetText(string.Empty);
		}
	}
}
