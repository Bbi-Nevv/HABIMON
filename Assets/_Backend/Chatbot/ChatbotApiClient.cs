using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine.Networking;

namespace HABIMON.Chatbot
{
    [Serializable]
    public class ChatbotSendMessageRequest
    {
        public string message;
        public string plan;
        public string childId;
        public ChatbotContext context;
    }

    public static class ChatbotRequestJson
    {
        public static string Serialize(ChatbotSendMessageRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.childId))
                throw new ArgumentException("A child id is required.", nameof(request));

            return "{\"message\":" + ChatbotContextJson.Quote(request.message) +
                   ",\"plan\":" + ChatbotContextJson.Quote(request.plan) +
                   ",\"child_id\":" + ChatbotContextJson.Quote(request.childId) +
                   ",\"context\":" + ChatbotContextJson.Serialize(request.context) + "}";
        }

        public static string SerializeSession(string childId)
        {
            if (string.IsNullOrWhiteSpace(childId))
                throw new ArgumentException("A child id is required.", nameof(childId));
            return "{\"child_id\":" + ChatbotContextJson.Quote(childId) + "}";
        }
    }

    public static class ChatbotApiRoutes
    {
        private const string ApiRoot = "/api/v1/sessions";

        public static string Sessions(string baseUrl)
        {
            return NormalizeBaseUrl(baseUrl) + ApiRoot;
        }

        public static string Sessions(string baseUrl, string childId, int limit, int offset)
        {
            if (limit < 1)
                throw new ArgumentOutOfRangeException(nameof(limit));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return Sessions(baseUrl) + ChildScopeQuery(childId) +
                   "&limit=" + limit.ToString(CultureInfo.InvariantCulture) +
                   "&offset=" + offset.ToString(CultureInfo.InvariantCulture);
        }

        public static string Session(string baseUrl, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A session id is required.", nameof(sessionId));

            return Sessions(baseUrl) + "/" + Uri.EscapeDataString(sessionId);
        }

        public static string Session(string baseUrl, string sessionId, string childId)
        {
            return Session(baseUrl, sessionId) + ChildScopeQuery(childId);
        }

        public static string Messages(string baseUrl, string sessionId)
        {
            return Session(baseUrl, sessionId) + "/messages";
        }

        public static string Messages(string baseUrl, string sessionId, string childId)
        {
            return Messages(baseUrl, sessionId) + ChildScopeQuery(childId);
        }

        private static string ChildScopeQuery(string childId)
        {
            if (string.IsNullOrWhiteSpace(childId))
                throw new ArgumentException("A child id is required.", nameof(childId));
            return "?child_id=" + Uri.EscapeDataString(childId);
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("A valid HTTP or HTTPS base URL is required.", nameof(baseUrl));

            return baseUrl.TrimEnd('/');
        }
    }

    public class ChatbotApiClient
    {
        private readonly string baseUrl;
        private readonly string ownerToken;

        public ChatbotApiClient(string baseUrl, string ownerToken)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("A valid HTTP or HTTPS base URL is required.", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(ownerToken))
                throw new ArgumentException("An owner token is required.", nameof(ownerToken));

            this.baseUrl = baseUrl.TrimEnd('/');
            this.ownerToken = ownerToken;
        }

        public static string AuthorizationHeaderValue(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("An owner token is required.", nameof(token));
            return "Bearer " + token;
        }

        public IEnumerator CreateSession(
            string childId,
            Action<ChatbotSessionResponse> onSuccess,
            Action<ChatbotApiError> onError)
        {
            var request = new UnityWebRequest(ChatbotApiRoutes.Sessions(baseUrl), UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ChatbotRequestJson.SerializeSession(childId))),
                downloadHandler = new DownloadHandlerBuffer()
            };
            SetCommonHeaders(request, true);
            yield return SendJson(request, onSuccess, onError);
        }

        public IEnumerator ListSessions(
            string childId,
            int limit,
            int offset,
            Action<ChatbotSessionListResponse> onSuccess,
            Action<ChatbotApiError> onError)
        {
            string url = ChatbotApiRoutes.Sessions(baseUrl, childId, limit, offset);
            var request = UnityWebRequest.Get(url);
            SetCommonHeaders(request, false);
            yield return SendJson(request, onSuccess, onError);
        }

        public IEnumerator GetMessages(
            string sessionId,
            string childId,
            Action<ChatbotMessageListResponse> onSuccess,
            Action<ChatbotApiError> onError)
        {
            var request = UnityWebRequest.Get(ChatbotApiRoutes.Messages(baseUrl, sessionId, childId));
            SetCommonHeaders(request, false);
            yield return SendJson(request, onSuccess, onError);
        }

        public IEnumerator DeleteSession(
            string sessionId,
            string childId,
            Action onSuccess,
            Action<ChatbotApiError> onError)
        {
            using (UnityWebRequest request = UnityWebRequest.Delete(ChatbotApiRoutes.Session(baseUrl, sessionId, childId)))
            {
                SetCommonHeaders(request, false);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                    onSuccess?.Invoke();
                else
                    onError?.Invoke(CreateError(request));
            }
        }

        public IEnumerator SendMessage(
            string sessionId,
            ChatbotSendMessageRequest body,
            Action<ChatbotSseEvent> onEvent,
            Action<ChatbotApiError> onError)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            var downloadHandler = new ChatbotSseDownloadHandler();
            bool terminalEventReceived = false;
            using (var request = new UnityWebRequest(ChatbotApiRoutes.Messages(baseUrl, sessionId), UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ChatbotRequestJson.Serialize(body)));
                request.downloadHandler = downloadHandler;
                SetCommonHeaders(request, true);
                request.SetRequestHeader("Accept", "text/event-stream");

                var parser = new ChatbotSseParser();
                parser.EventReceived += parsedEvent =>
                {
                    if (parsedEvent.eventName == "done" || parsedEvent.eventName == "error")
                        terminalEventReceived = true;
                    onEvent?.Invoke(parsedEvent);
                };
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    Drain(downloadHandler, parser);
                    yield return null;
                }

                Drain(downloadHandler, parser);
                parser.Complete();
                if (request.result != UnityWebRequest.Result.Success)
                    onError?.Invoke(CreateError(request));
                else if (!terminalEventReceived)
                    onError?.Invoke(new ChatbotApiError { code = "incomplete_stream", statusCode = request.responseCode });
            }
        }

        private void SetCommonHeaders(UnityWebRequest request, bool hasJsonBody)
        {
            request.SetRequestHeader("Authorization", AuthorizationHeaderValue(ownerToken));
            if (hasJsonBody)
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        }

        private static IEnumerator SendJson<T>(
            UnityWebRequest request,
            Action<T> onSuccess,
            Action<ChatbotApiError> onError)
        {
            using (request)
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(CreateError(request));
                    yield break;
                }

                try
                {
                    T response = UnityEngine.JsonUtility.FromJson<T>(request.downloadHandler.text);
                    if (response == null)
                    {
                        onError?.Invoke(new ChatbotApiError { code = "invalid_response", statusCode = request.responseCode });
                        yield break;
                    }
                    onSuccess?.Invoke(response);
                }
                catch (Exception)
                {
                    onError?.Invoke(new ChatbotApiError { code = "invalid_response", statusCode = request.responseCode });
                }
            }
        }

        private static ChatbotApiError CreateError(UnityWebRequest request)
        {
            return new ChatbotApiError
            {
                code = request.result == UnityWebRequest.Result.ConnectionError ? "connection_failed" : "http_error",
                statusCode = request.responseCode
            };
        }

        private static void Drain(ChatbotSseDownloadHandler handler, ChatbotSseParser parser)
        {
            while (handler.TryDequeue(out byte[] chunk))
                parser.Feed(chunk, chunk.Length);
        }

        private sealed class ChatbotSseDownloadHandler : DownloadHandlerScript
        {
            private readonly ConcurrentQueue<byte[]> chunks = new ConcurrentQueue<byte[]>();

            public ChatbotSseDownloadHandler()
                : base(new byte[8192])
            {
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                    return true;

                var copy = new byte[dataLength];
                Buffer.BlockCopy(data, 0, copy, 0, dataLength);
                chunks.Enqueue(copy);
                return true;
            }

            public bool TryDequeue(out byte[] chunk)
            {
                return chunks.TryDequeue(out chunk);
            }
        }
    }
}
