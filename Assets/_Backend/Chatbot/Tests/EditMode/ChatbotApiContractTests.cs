using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace HABIMON.Chatbot.Tests
{
    public class ChatbotApiContractTests
    {
        [Test]
        public void SessionRoutesNormalizeBaseUrlAndEscapeSessionId()
        {
            Assert.That(
                ChatbotApiRoutes.Sessions("https://chat.example/demo/"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions"));
            Assert.That(
                ChatbotApiRoutes.Session("https://chat.example/demo", "session-id"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions/session-id"));
            Assert.That(
                ChatbotApiRoutes.Messages("https://chat.example/demo", "session id"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions/session%20id/messages"));
            Assert.That(
                ChatbotApiRoutes.Sessions("https://chat.example/demo", "child id", 50, 0),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions?child_id=child%20id&limit=50&offset=0"));
            Assert.That(
                ChatbotApiRoutes.Messages("https://chat.example/demo", "session-id", "child/one"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions/session-id/messages?child_id=child%2Fone"));
            Assert.That(
                ChatbotApiRoutes.Session("https://chat.example/demo", "session-id", "child-one"),
                Is.EqualTo("https://chat.example/demo/api/v1/sessions/session-id?child_id=child-one"));
        }

        [Test]
        public void RequestJsonPreservesVietnameseAndNestedContext()
        {
            var request = new ChatbotSendMessageRequest
            {
                message = "Xin chào \"An\"",
                plan = "free",
                childId = "child-one",
                context = new ChatbotContext
                {
                    capturedAt = "2026-10-17T14:00:00+07:00",
                    localDate = "2026-10-17",
                    timeZone = "Asia/Ho_Chi_Minh",
                    child = null,
                    pet = null,
                    habits = new ChatHabitContext[0],
                    rewards = new ChatRewardContext[0]
                }
            };

            string json = ChatbotRequestJson.Serialize(request);

            StringAssert.Contains("\"message\":\"Xin chào \\\"An\\\"\"", json);
            StringAssert.Contains("\"plan\":\"free\"", json);
            StringAssert.Contains("\"child_id\":\"child-one\"", json);
            StringAssert.Contains("\"context\":{", json);
            StringAssert.Contains("\"child\":null", json);
            StringAssert.Contains("\"pet\":null", json);
        }

        [Test]
        public void SessionRequestJsonSendsChildScopeSeparately()
        {
            Assert.That(
                ChatbotRequestJson.SerializeSession("child-one"),
                Is.EqualTo("{\"child_id\":\"child-one\"}"));
        }

        [Test]
        public void AuthorizationValueUsesBearerScheme()
        {
            Assert.That(
                ChatbotApiClient.AuthorizationHeaderValue("installation-token"),
                Is.EqualTo("Bearer installation-token"));
        }

        [Test]
        public void ClientRejectsInsecureOrCredentialBearingBaseUrls()
        {
            Assert.Throws<System.ArgumentException>(() => new ChatbotApiClient("http://api.example", "token"));
            Assert.Throws<System.ArgumentException>(() => new ChatbotApiClient("http://127.0.0.1:8080", "token"));
            Assert.Throws<System.ArgumentException>(() => new ChatbotApiClient("https://user:pass@chat.example", "token"));
            Assert.Throws<System.ArgumentException>(() => new ChatbotApiClient("https://chat.example/path?token=value", "token"));
            Assert.DoesNotThrow(() => new ChatbotApiClient("http://127.0.0.1:8080", "token", true));
        }

        [Test]
        public void SseProtocolValidatesBackendPayloadsAndTerminalEvents()
        {
            Assert.That(ChatbotSseProtocol.TryValidateEvent(
                new ChatbotSseEvent { eventName = "done", data = "{\"status\":\"completed\"}" },
                out bool doneIsTerminal,
                out _), Is.True);
            Assert.That(doneIsTerminal, Is.True);

            Assert.That(ChatbotSseProtocol.TryValidateEvent(
                new ChatbotSseEvent { eventName = "done", data = "{\"status\":\"finished\"}" },
                out bool invalidDoneIsTerminal,
                out string doneErrorCode), Is.False);
            Assert.That(invalidDoneIsTerminal, Is.False);
            Assert.That(doneErrorCode, Is.EqualTo("invalid_sse_payload"));

            Assert.That(ChatbotSseProtocol.TryValidateEvent(
                new ChatbotSseEvent { eventName = "error", data = "{\"code\":\"storage_failed\"}" },
                out bool errorIsTerminal,
                out _), Is.True);
            Assert.That(errorIsTerminal, Is.True);

            Assert.That(ChatbotSseProtocol.TryValidateEvent(
                new ChatbotSseEvent { eventName = "delta", data = "{\"seq\":0,\"text\":\"x\"}" },
                out bool deltaIsTerminal,
                out _), Is.False);
            Assert.That(deltaIsTerminal, Is.False);
        }

        [UnityTest]
        public IEnumerator SendMessageRejectsMalformedDonePayload()
        {
            byte[] responseBody = Encoding.UTF8.GetBytes(
                "event: meta\ndata: {\"session_id\":\"session-one\"}\n\n" +
                "event: done\ndata: {\"status\":\"finished\"}\n\n");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task server = Task.Run(() => ServeResponseAsync(listener, responseBody));
            ChatbotApiError error = null;
            var events = new List<ChatbotSseEvent>();
            var client = new ChatbotApiClient($"http://127.0.0.1:{port}", "token", true);
            var request = new ChatbotSendMessageRequest
            {
                message = "hello",
                plan = "free",
                childId = "child-one",
                context = new ChatbotContext
                {
                    habits = new ChatHabitContext[0],
                    rewards = new ChatRewardContext[0]
                }
            };

            try
            {
                yield return client.SendMessage("session-one", request, events.Add, value => error = value);

                Assert.That(error, Is.Not.Null);
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].eventName, Is.EqualTo("meta"));
                Assert.That(error.code, Is.EqualTo("invalid_sse_payload"));
                Assert.That(server.Wait(System.TimeSpan.FromSeconds(5)), Is.True);
            }
            finally
            {
                listener.Stop();
            }
        }

        [UnityTest]
        public IEnumerator SendMessageCanBeCancelledWhileWaitingForServer()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accepted = new TaskCompletionSource<bool>();
            var continueServer = new TaskCompletionSource<bool>();
            Task server = Task.Run(() => ServeResponseAsync(listener, null, accepted, continueServer.Task));
            var cancellation = new ChatbotApiCancellation();
            ChatbotApiError error = null;
            var client = new ChatbotApiClient($"http://127.0.0.1:{port}", "token", true);
            var request = new ChatbotSendMessageRequest
            {
                message = "hello",
                plan = "free",
                childId = "child-one",
                context = new ChatbotContext
                {
                    habits = new ChatHabitContext[0],
                    rewards = new ChatRewardContext[0]
                }
            };

            try
            {
                IEnumerator operation = client.SendMessage("session-one", request, _ => { }, value => error = value, cancellation);
                int frames = 0;
                while (!accepted.Task.IsCompleted && frames++ < 600 && operation.MoveNext())
                    yield return operation.Current;

                Assert.That(accepted.Task.IsCompleted, Is.True);
                cancellation.Cancel();
                continueServer.TrySetResult(true);
                while (operation.MoveNext())
                    yield return operation.Current;

                Assert.That(error, Is.Not.Null);
                Assert.That(error.code, Is.EqualTo("cancelled"));
                for (int frame = 0; frame < 120 && !server.IsCompleted; frame++)
                    yield return null;
                Assert.That(server.IsCompleted, Is.True);
            }
            finally
            {
                listener.Stop();
            }
        }

        [UnityTest]
        public IEnumerator JsonConsumerExceptionIsNotReportedAsInvalidResponse()
        {
            byte[] responseBody = Encoding.UTF8.GetBytes("{\"sessions\":[]}");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task server = Task.Run(() => ServeResponseAsync(listener, responseBody));
            Exception consumerException = null;
            ChatbotApiError apiError = null;
            var client = new ChatbotApiClient($"http://127.0.0.1:{port}", "token", true);
            var stack = new Stack<IEnumerator>();
            stack.Push(client.ListSessions("child-one", 10, 0,
                _ => throw new InvalidOperationException("consumer callback failed"),
                error => apiError = error));

            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator current = stack.Peek();
                    bool moved = false;
                    object yielded = null;
                    Exception iterationError = null;
                    try
                    {
                        moved = current.MoveNext();
                        if (moved)
                            yielded = current.Current;
                    }
                    catch (Exception ex)
                    {
                        iterationError = ex;
                    }

                    if (iterationError != null)
                    {
                        consumerException = iterationError;
                        while (stack.Count > 0)
                            (stack.Pop() as IDisposable)?.Dispose();
                        break;
                    }

                    if (!moved)
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }

                    if (yielded is IEnumerator nested)
                    {
                        stack.Push(nested);
                        continue;
                    }

                    yield return yielded;
                }

                Assert.That(consumerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(apiError, Is.Null);
                Assert.That(server.Wait(System.TimeSpan.FromSeconds(5)), Is.True);
            }
            finally
            {
                while (stack.Count > 0)
                    (stack.Pop() as IDisposable)?.Dispose();
                listener.Stop();
            }
        }

        private static async Task ServeResponseAsync(
            TcpListener listener,
            byte[] responseBody,
            TaskCompletionSource<bool> accepted = null,
            Task continueServer = null)
        {
            using (TcpClient connection = await listener.AcceptTcpClientAsync())
            using (NetworkStream stream = connection.GetStream())
            {
                var requestHeader = new List<byte>();
                byte[] oneByte = new byte[1];
                while (requestHeader.Count < 4 ||
                       requestHeader[requestHeader.Count - 4] != '\r' ||
                       requestHeader[requestHeader.Count - 3] != '\n' ||
                       requestHeader[requestHeader.Count - 2] != '\r' ||
                       requestHeader[requestHeader.Count - 1] != '\n')
                {
                    int read = await stream.ReadAsync(oneByte, 0, 1);
                    if (read == 0)
                        throw new System.IO.IOException("The client closed before sending HTTP headers.");
                    requestHeader.Add(oneByte[0]);
                }

                string headerText = Encoding.ASCII.GetString(requestHeader.ToArray());
                int contentLength = 0;
                foreach (string headerLine in headerText.Split(new[] { "\r\n" }, System.StringSplitOptions.None))
                {
                    if (headerLine.StartsWith("Content-Length:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(headerLine.Substring("Content-Length:".Length).Trim(), out contentLength);
                        break;
                    }
                }

                int bodyBytesRead = 0;
                byte[] bodyBuffer = new byte[1024];
                while (bodyBytesRead < contentLength)
                {
                    int read = await stream.ReadAsync(bodyBuffer, 0, System.Math.Min(bodyBuffer.Length, contentLength - bodyBytesRead));
                    if (read == 0)
                        throw new System.IO.IOException("The client closed before sending the HTTP body.");
                    bodyBytesRead += read;
                }

                accepted?.TrySetResult(true);
                if (continueServer != null)
                    await continueServer;
                if (responseBody == null)
                    return;

                byte[] responseHeader = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nConnection: close\r\nContent-Length: " +
                    responseBody.Length + "\r\n\r\n");
                try
                {
                    await stream.WriteAsync(responseHeader, 0, responseHeader.Length);
                    await stream.WriteAsync(responseBody, 0, responseBody.Length);
                    await stream.FlushAsync();
                }
                catch (System.IO.IOException)
                {
                    // The cancellation test closes the client before the delayed response is written.
                }
                catch (SocketException)
                {
                    // The cancellation test closes the client before the delayed response is written.
                }
            }
        }
    }
}
