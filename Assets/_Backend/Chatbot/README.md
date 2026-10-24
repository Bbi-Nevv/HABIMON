# HABIMON.Chatbot mobile module

This module contains the chatbot wire contract, context adapters, session API client, and SSE parser. It deliberately does not add a scene, prefab, button, credential store, or UI.

## Integration inputs

The consuming app provides a backend base URL, the submitted plan (`free`, `plus`, or `premium`), and a stable high-entropy per-installation owner token stored by the app's secure credential layer.

Use `FirebaseChatbotContextAdapter.LoadCurrentSnapshotAsync(...)` to load the current child's profile, habits, a caller-bounded `yyyy-MM-dd` log range, rewards, and pet under one captured parent/child/session-generation scope. The requested end date is exclusive. It returns `null` when there is no valid authenticated parent or selected child, the selected child document is missing, or the session changes while data is loading; invalid or unbounded date ranges are rejected. Firebase read failures propagate to the caller. Keep the returned `FirebaseChatbotContextSnapshot` for the operation; use `snapshot.Context` as model context and `snapshot.Scope.ChildId` separately as the API session scope. Pass `snapshot.Cancellation` to each API request and dispose the snapshot when the host operation/view ends; account or child changes cancel active requests automatically. Do not serialize `snapshot.Scope` or reuse the snapshot after a parent/child/session change.

The adapter uses child/document IDs only to join logs to habits; Firebase IDs are not serialized into model context. The API client sends the selected child's document ID separately as a session-scope value, and the backend stores only a hash. Placeholder profiles such as `Unnull`/age `0` produce an unknown child context. Gold and EXP are omitted because the mobile app does not currently maintain them.

Habit schedules are normalized in the chatbot adapter: the Vietnamese period labels become `day_period` values `morning`, `afternoon`, or `evening`; a clock value becomes `local_time` in `HH:mm` form; an unrecognized non-empty label is sent as `unclassified` with its original value. Empty schedule data is `null`.

The API client accepts the base URL and owner token at construction. It does not create or persist the token. Pass the selected child's document ID to session creation, listing, history retrieval, deletion, and each chat request; it is sent separately from context and is not included in model input. The backend hashes this ID on storage and checks that every operation matches the session's child scope. Keep the session ID associated with that child in the host app. Use the coroutine methods from a `MonoBehaviour`; `SendMessage` calls back with the `meta`, `delta`, `done`, and `error` SSE events on the Unity coroutine thread.

Production API URLs must use HTTPS and cannot contain user information, query parameters, or fragments. HTTP is accepted only for loopback addresses when the caller explicitly passes `allowInsecureLocalhostForDevelopment: true`; never enable that option for a deployed host. Keep one `ChatbotApiCancellation` per in-flight request and call `Cancel()` when the child/account changes or the owning view is destroyed. JSON requests have a 30-second deadline; streams have a 45-second idle timeout and a 180-second total timeout. SSE lines, events, and queued network bytes are capped; malformed UTF-8, incomplete terminal frames, and invalid `done` payloads report protocol errors.

The backend URL and secure token lifecycle belong to the consuming app configuration/auth layer. The module does not choose the first child; the caller must pass the child selected in the UI.
