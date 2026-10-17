# HABIMON.Chatbot mobile module

This module contains the chatbot wire contract, context adapters, session API client, and SSE parser. It deliberately does not add a scene, prefab, button, credential store, or UI.

## Integration inputs

The consuming app provides:

- the currently selected `ChildModel`;
- loaded habits, logs, and rewards for that child;
- the current `HabimonModel`, or `null` while pet state is unavailable or not live;
- a backend base URL, the submitted plan (`free`, `plus`, or `premium`), and a stable high-entropy per-installation owner token stored by the app's secure credential layer.

`FirebaseChatbotContextAdapter.Build(...)` maps the Firebase models into the model context. It uses child/document IDs only to join logs to habits; Firebase IDs are not serialized into model context. The API client sends the selected child's document ID separately as a session-scope value, and the backend stores only a hash. Placeholder profiles such as `Unnull`/age `0` produce an unknown child context. Gold and EXP are omitted because the mobile app does not currently maintain them.

Habit schedules are normalized in the chatbot adapter: the Vietnamese period labels become `day_period` values `morning`, `afternoon`, or `evening`; a clock value becomes `local_time` in `HH:mm` form; an unrecognized non-empty label is sent as `unclassified` with its original value. Empty schedule data is `null`.

The API client accepts the base URL and owner token at construction. It does not create or persist the token. Pass the selected child's document ID to session creation, listing, history retrieval, deletion, and each chat request; it is sent separately from context and is not included in model input. The backend hashes this ID on storage and checks that every operation matches the session's child scope. Keep the session ID associated with that child in the host app. Use the coroutine methods from a `MonoBehaviour`; `SendMessage` calls back with the `meta`, `delta`, `done`, and `error` SSE events on the Unity coroutine thread.

The backend URL and secure token lifecycle belong to the consuming app configuration/auth layer. The module does not choose the first child; the caller must pass the child selected in the UI.
