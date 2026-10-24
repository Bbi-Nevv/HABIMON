using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
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
            Uri uri = ParseBaseUri(baseUrl);
            return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        internal static Uri ParseBaseUri(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("A valid HTTP or HTTPS base URL without credentials, query, or fragment is required.", nameof(baseUrl));

            return uri;
        }
    }

    public sealed class ChatbotApiCancellation
    {
        private readonly object sync = new object();
        private UnityWebRequest activeRequest;
        private bool cancellationRequested;

        public bool IsCancellationRequested
        {
            get
            {
                lock (sync)
                    return cancellationRequested;
            }
        }

        public void Cancel()
        {
            UnityWebRequest request;
            lock (sync)
            {
                cancellationRequested = true;
                request = activeRequest;
            }

            request?.Abort();
        }

        internal void Attach(UnityWebRequest request)
        {
            bool abort;
            lock (sync)
            {
                activeRequest = request;
                abort = cancellationRequested;
            }

            if (abort)
                request.Abort();
        }

        internal void Detach(UnityWebRequest request)
        {
            lock (sync)
            {
                if (ReferenceEquals(activeRequest, request))
                    activeRequest = null;
            }
        }
    }

    public static class ChatbotSseProtocol
    {
        public static bool TryValidateEvent(ChatbotSseEvent value, out bool isTerminal, out string errorCode)
        {
            isTerminal = false;
            errorCode = "invalid_sse_payload";
            if (value == null || value.data == null)
                return false;

            try
            {
                switch (value.eventName)
                {
                    case "meta":
                        ChatbotSseMetaPayload meta = JsonUtility.FromJson<ChatbotSseMetaPayload>(value.data);
                        return meta != null && !string.IsNullOrWhiteSpace(meta.session_id);
                    case "delta":
                        ChatbotSseDeltaPayload delta = JsonUtility.FromJson<ChatbotSseDeltaPayload>(value.data);
                        return delta != null && delta.seq > 0 && delta.text != null;
                    case "done":
                        ChatbotSseDonePayload done = JsonUtility.FromJson<ChatbotSseDonePayload>(value.data);
                        if (done == null || done.status != "completed")
                            return false;
                        isTerminal = true;
                        return true;
                    case "error":
                        ChatbotSseErrorPayload error = JsonUtility.FromJson<ChatbotSseErrorPayload>(value.data);
                        if (error == null || string.IsNullOrWhiteSpace(error.code))
                            return false;
                        isTerminal = true;
                        return true;
                    default:
                        errorCode = "unsupported_sse_event";
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

#pragma warning disable 0649 // Unity JsonUtility populates these fields through reflection.
        [Serializable]
        private class ChatbotSseMetaPayload
        {
            public string session_id;
        }

        [Serializable]
        private class ChatbotSseDeltaPayload
        {
            public int seq;
            public string text;
        }

        [Serializable]
        private class ChatbotSseDonePayload
        {
            public string status;
        }

        [Serializable]
        private class ChatbotSseErrorPayload
        {
            public string code;
        }
#pragma warning restore 0649
    }

    public class ChatbotApiClient
    {
        private const int JsonTimeoutSeconds = 30;
        private const int StreamIdleTimeoutSeconds = 45;
        private const int StreamTotalTimeoutSeconds = 180;
        private const int MaximumQueuedStreamBytes = 1024 * 1024;

        private readonly string baseUrl;
        private readonly string ownerToken;

        public ChatbotApiClient(string baseUrl, string ownerToken, bool allowInsecureLocalhostForDevelopment = false)
        {
            Uri uri = ChatbotApiRoutes.ParseBaseUri(baseUrl);
            if (uri.Scheme == Uri.UriSchemeHttp && !(allowInsecureLocalhostForDevelopment && uri.IsLoopback))
                throw new ArgumentException("HTTPS is required except for an explicitly enabled local loopback endpoint.", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(ownerToken))
                throw new ArgumentException("An owner token is required.", nameof(ownerToken));

            this.baseUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
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
            Action<ChatbotApiError> onError,
            ChatbotApiCancellation cancellation = null)
        {
            var request = new UnityWebRequest(ChatbotApiRoutes.Sessions(baseUrl), UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ChatbotRequestJson.SerializeSession(childId))),
                downloadHandler = new DownloadHandlerBuffer()
            };
            SetCommonHeaders(request, true);
            yield return SendJson(request, onSuccess, onError, cancellation);
        }

        public IEnumerator ListSessions(
            string childId,
            int limit,
            int offset,
            Action<ChatbotSessionListResponse> onSuccess,
            Action<ChatbotApiError> onError,
            ChatbotApiCancellation cancellation = null)
        {
            string url = ChatbotApiRoutes.Sessions(baseUrl, childId, limit, offset);
            var request = UnityWebRequest.Get(url);
            SetCommonHeaders(request, false);
            yield return SendJson(request, onSuccess, onError, cancellation);
        }

        public IEnumerator GetMessages(
            string sessionId,
            string childId,
            Action<ChatbotMessageListResponse> onSuccess,
            Action<ChatbotApiError> onError,
            ChatbotApiCancellation cancellation = null)
        {
            var request = UnityWebRequest.Get(ChatbotApiRoutes.Messages(baseUrl, sessionId, childId));
            SetCommonHeaders(request, false);
            yield return SendJson(request, onSuccess, onError, cancellation);
        }

        public IEnumerator DeleteSession(
            string sessionId,
            string childId,
            Action onSuccess,
            Action<ChatbotApiError> onError,
            ChatbotApiCancellation cancellation = null)
        {
            using (UnityWebRequest request = UnityWebRequest.Delete(ChatbotApiRoutes.Session(baseUrl, sessionId, childId)))
            {
                SetCommonHeaders(request, false);
                request.timeout = JsonTimeoutSeconds;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                cancellation?.Attach(request);
                try
                {
                    double deadline = Time.realtimeSinceStartupAsDouble + JsonTimeoutSeconds;

                    while (!operation.isDone && !HasTimedOut(deadline) && !(cancellation?.IsCancellationRequested ?? false))
                        yield return null;

                    bool timedOut = !operation.isDone && HasTimedOut(deadline);
                    if (!operation.isDone)
                    {
                        request.Abort();
                        while (!operation.isDone)
                            yield return null;
                    }

                    if (cancellation?.IsCancellationRequested ?? false)
                        onError?.Invoke(new ChatbotApiError { code = "cancelled", statusCode = request.responseCode });
                    else if (timedOut)
                        onError?.Invoke(new ChatbotApiError { code = "timeout", statusCode = request.responseCode });
                    else if (request.result == UnityWebRequest.Result.Success)
                        onSuccess?.Invoke();
                    else
                        onError?.Invoke(CreateError(request));
                }
                finally
                {
                    cancellation?.Detach(request);
                }
            }
        }

        public IEnumerator SendMessage(
            string sessionId,
            ChatbotSendMessageRequest body,
            Action<ChatbotSseEvent> onEvent,
            Action<ChatbotApiError> onError,
            ChatbotApiCancellation cancellation = null,
            Action<Exception> onConsumerError = null)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            var downloadHandler = new ChatbotSseDownloadHandler(MaximumQueuedStreamBytes);
            bool terminalEventReceived = false;
            bool terminalWasError = false;
            bool consumerFailed = false;
            string protocolError = null;
            Exception consumerException = null;

            using (var request = new UnityWebRequest(ChatbotApiRoutes.Messages(baseUrl, sessionId), UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ChatbotRequestJson.Serialize(body)));
                request.downloadHandler = downloadHandler;
                request.timeout = StreamTotalTimeoutSeconds;
                SetCommonHeaders(request, true);
                request.SetRequestHeader("Accept", "text/event-stream");

                var parser = new ChatbotSseParser();
                parser.EventReceived += parsedEvent =>
                {
                    if (!ChatbotSseProtocol.TryValidateEvent(parsedEvent, out bool isTerminal, out string errorCode))
                    {
                        protocolError = errorCode;
                        request.Abort();
                        return;
                    }

                    terminalEventReceived |= isTerminal;
                    terminalWasError |= isTerminal && parsedEvent.eventName == "error";
                    try
                    {
                        onEvent?.Invoke(parsedEvent);
                    }
                    catch (Exception ex)
                    {
                        consumerFailed = true;
                        consumerException = ex;
                        request.Abort();
                    }
                };

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                cancellation?.Attach(request);
                try
                {
                    double startedAt = GetStopwatchTime();
                    double lastDataAt = startedAt;
                    bool timedOut = false;

                    while (!operation.isDone)
                    {
                        if (cancellation?.IsCancellationRequested ?? false)
                        {
                            request.Abort();
                            break;
                        }

                        if (downloadHandler.IsOverflowed)
                        {
                            protocolError = "stream_buffer_limit_exceeded";
                            request.Abort();
                            break;
                        }

                        string drainError = Drain(downloadHandler, parser);
                        if (drainError != null)
                        {
                            protocolError = drainError;
                            request.Abort();
                            break;
                        }

                        if (consumerFailed || protocolError != null)
                        {
                            request.Abort();
                            break;
                        }

                        double now = GetStopwatchTime();
                        double lastReceived = downloadHandler.LastReceiveTime;
                        if (lastReceived > lastDataAt)
                            lastDataAt = lastReceived;

                        if (now - startedAt >= StreamTotalTimeoutSeconds || now - lastDataAt >= StreamIdleTimeoutSeconds)
                        {
                            timedOut = true;
                            request.Abort();
                            break;
                        }

                        yield return null;
                    }

                    while (!operation.isDone)
                        yield return null;

                    if (consumerFailed)
                    {
                        NotifyConsumerError(onConsumerError, consumerException);
                        yield break;
                    }

                    if (cancellation?.IsCancellationRequested ?? false)
                    {
                        onError?.Invoke(new ChatbotApiError { code = "cancelled", statusCode = request.responseCode });
                        yield break;
                    }

                    if (timedOut)
                    {
                        onError?.Invoke(new ChatbotApiError { code = "timeout", statusCode = request.responseCode });
                        yield break;
                    }

                    if (protocolError == null)
                    {
                        string drainError = Drain(downloadHandler, parser);
                        if (protocolError == null)
                            protocolError = drainError;
                    }
                    if (protocolError == null)
                    {
                        try
                        {
                            parser.Complete();
                        }
                        catch (DecoderFallbackException)
                        {
                            protocolError = "invalid_utf8";
                        }
                        catch (InvalidDataException)
                        {
                            protocolError = "stream_limit_exceeded";
                        }
                    }

                    if (protocolError != null)
                        onError?.Invoke(new ChatbotApiError { code = protocolError, statusCode = request.responseCode });
                    else if (request.result != UnityWebRequest.Result.Success)
                        onError?.Invoke(CreateError(request));
                    else if (!terminalEventReceived)
                        onError?.Invoke(new ChatbotApiError { code = "incomplete_stream", statusCode = request.responseCode });
                    else if (terminalWasError)
                        yield break;
                }
                finally
                {
                    cancellation?.Detach(request);
                }
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
            Action<ChatbotApiError> onError,
            ChatbotApiCancellation cancellation)
        {
            using (request)
            {
                request.timeout = JsonTimeoutSeconds;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                cancellation?.Attach(request);
                try
                {
                    double deadline = Time.realtimeSinceStartupAsDouble + JsonTimeoutSeconds;

                    while (!operation.isDone && !HasTimedOut(deadline) && !(cancellation?.IsCancellationRequested ?? false))
                        yield return null;

                    bool timedOut = !operation.isDone && HasTimedOut(deadline);
                    if (!operation.isDone)
                    {
                        request.Abort();
                        while (!operation.isDone)
                            yield return null;
                    }

                    if (cancellation?.IsCancellationRequested ?? false)
                    {
                        onError?.Invoke(new ChatbotApiError { code = "cancelled", statusCode = request.responseCode });
                        yield break;
                    }

                    if (timedOut)
                    {
                        onError?.Invoke(new ChatbotApiError { code = "timeout", statusCode = request.responseCode });
                        yield break;
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke(CreateError(request));
                        yield break;
                    }

                    T response = default(T);
                    bool invalidResponse = false;
                    try
                    {
                        response = JsonUtility.FromJson<T>(request.downloadHandler.text);
                        if (!IsValidResponse(response))
                            throw new InvalidDataException("The response is missing required fields.");
                    }
                    catch (Exception)
                    {
                        invalidResponse = true;
                    }

                    if (invalidResponse)
                    {
                        onError?.Invoke(new ChatbotApiError { code = "invalid_response", statusCode = request.responseCode });
                        yield break;
                    }

                    // Keep consumer code outside the response parse catch so its exceptions retain their origin.
                    onSuccess?.Invoke(response);
                }
                finally
                {
                    cancellation?.Detach(request);
                }
            }
        }

        private static bool IsValidResponse<T>(T response)
        {
            if (response == null)
                return false;

            if (response is ChatbotSessionResponse session)
                return !string.IsNullOrWhiteSpace(session.id);
            if (response is ChatbotSessionListResponse sessions)
                return sessions.sessions != null;
            if (response is ChatbotMessageListResponse messages)
                return messages.messages != null;

            return true;
        }

        private static bool HasTimedOut(double deadline)
        {
            return Time.realtimeSinceStartupAsDouble >= deadline;
        }

        private static double GetStopwatchTime()
        {
            return (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        }

        private static void NotifyConsumerError(Action<Exception> callback, Exception error)
        {
            if (callback == null)
            {
                Debug.LogException(error);
                return;
            }

            try
            {
                callback(error);
            }
            catch (Exception callbackError)
            {
                Debug.LogException(callbackError);
            }
        }

        private static ChatbotApiError CreateError(UnityWebRequest request)
        {
            string code = request.result == UnityWebRequest.Result.ConnectionError ? "connection_failed" :
                request.result == UnityWebRequest.Result.DataProcessingError ? "invalid_response" : "http_error";
            return new ChatbotApiError { code = code, statusCode = request.responseCode };
        }

        private static string Drain(ChatbotSseDownloadHandler handler, ChatbotSseParser parser)
        {
            if (handler.IsOverflowed)
                return "stream_buffer_limit_exceeded";

            while (handler.TryDequeue(out byte[] chunk))
            {
                try
                {
                    parser.Feed(chunk, chunk.Length);
                }
                catch (DecoderFallbackException)
                {
                    return "invalid_utf8";
                }
                catch (InvalidDataException)
                {
                    return "stream_limit_exceeded";
                }
            }

            return handler.IsOverflowed ? "stream_buffer_limit_exceeded" : null;
        }

        private sealed class ChatbotSseDownloadHandler : DownloadHandlerScript
        {
            private readonly ConcurrentQueue<byte[]> chunks = new ConcurrentQueue<byte[]>();
            private readonly int maxQueuedBytes;
            private int queuedBytes;
            private int overflowed;
            private long lastReceiveTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

            public ChatbotSseDownloadHandler(int maxQueuedBytes)
                : base(new byte[8192])
            {
                this.maxQueuedBytes = maxQueuedBytes;
            }

            public bool IsOverflowed => Volatile.Read(ref overflowed) != 0;
            public double LastReceiveTime => (double)Interlocked.Read(ref lastReceiveTimestamp) / System.Diagnostics.Stopwatch.Frequency;

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                    return true;

                if (Interlocked.Add(ref queuedBytes, dataLength) > maxQueuedBytes)
                {
                    Interlocked.Add(ref queuedBytes, -dataLength);
                    Interlocked.Exchange(ref overflowed, 1);
                    return false;
                }

                var copy = new byte[dataLength];
                Buffer.BlockCopy(data, 0, copy, 0, dataLength);
                chunks.Enqueue(copy);
                Interlocked.Exchange(ref lastReceiveTimestamp, System.Diagnostics.Stopwatch.GetTimestamp());
                return true;
            }

            public bool TryDequeue(out byte[] chunk)
            {
                if (!chunks.TryDequeue(out chunk))
                    return false;

                Interlocked.Add(ref queuedBytes, -chunk.Length);
                return true;
            }
        }
    }
}
