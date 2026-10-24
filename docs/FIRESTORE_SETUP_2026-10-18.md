# HABIMON Firestore project configuration

This repository targets the existing Firebase project `habimon-808e2`.

Last review: **2026-10-24** (Asia/Ho_Chi_Minh), local branch `Nam`, HEAD `486765987db5726873185235fb1a6c398f6b98c8` plus uncommitted changes. Filenames retain their original creation date.

## Project state and verification dates

- Historical CLI verification (2026-10-18/19), not re-run on 2026-10-22: project `HABIMON` (`habimon-808e2`), project number `350279351530`, state `ACTIVE`.
- Historical CLI verification (2026-10-18/19): Firestore database: `(default)`, Standard edition, location `asia-southeast1`.
- Historical CLI verification (2026-10-18/19): registered mobile apps: one Android and one iOS app are `ACTIVE`. Read-only CLI listing confirmed the Android/iOS app IDs and package/bundle ID `com.GSAM.HABIMON` match the corresponding repository config files.
- Historical Console observation (2026-10-18), not refreshed on 2026-10-22: manual composite index list: empty in Firebase Console. Automatic indexing is enabled for collection scope; collection-group automatic indexes are disabled; no field exemptions were listed.
- Firestore Rules active in the Firebase project were re-read through Firebase MCP on **2026-10-22**, after its environment tool confirmed active project `habimon-808e2`. They still allow public reads and writes to every document:

```text
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /{document=**} {
      allow read, write: if true;
    }
  }
}
```

This readback records the initial 22/10 baseline, before the later containment rollout that day. The active project state after those actions is recorded under **Current post-action status** below. The owner-scoped policy still cannot distinguish Parent and Child modes sharing one Firebase Auth UID or enforce all schema/business operations.

## Security review status — initial 22/10 baseline (historical)

```json
{
  "score": 1,
  "summary": "The deployed project still permits public read and write. The local prototype improves cross-UID isolation but is not a complete authorization policy.",
  "findings": [
    {
      "check": "Deployed authorization",
      "severity": "critical",
      "issue": "Live Firestore Rules allow anyone to read and write every document.",
      "recommendation": "The mobile/DB owner must approve and deploy a reviewed policy after compatibility and migration planning."
    },
    {
      "check": "Parent and Child roles",
      "severity": "major",
      "issue": "Both modes use the same Firebase Auth UID, so owner checks cannot enforce parent-only actions.",
      "recommendation": "Move PIN verification, elevation, deletion, approval, and reward authority to a trusted server boundary."
    },
    {
      "check": "Schema and business validation",
      "severity": "major",
      "issue": "The local prototype allows the owner to write arbitrary fields and values within their subtree.",
      "recommendation": "Confirm the production schema and define create/update validators and operation-specific authorization before deployment."
    }
  ]
}
```

This score describes the project at the initial baseline readback. Emulator test success alone does not change deployed Rules; the later deployment/readback is recorded below.

## Repository files and local Rules tests

- `.firebaserc` selects `habimon-808e2` as the default project.
- `firebase.json` maps the default Firestore database to `firestore.rules` and `firestore.indexes.json`, and configures the local Firestore Emulator on `127.0.0.1:8080`.
- `firestore.indexes.json` has no manual indexes, matching the current Console index state.
- `firestore.rules` denies access outside `users/{parentId}`, permits only the matching authenticated UID within that owner subtree, and denies child-subcollection access when the parent document does not exist. This is a prototype; owner-scoped access alone cannot distinguish Parent from Child mode.
- `tests/firestore-rules/firestore.rules.test.mjs` uses the Rules Unit Testing library. It requires `FIRESTORE_EMULATOR_HOST=127.0.0.1:8080`, uses project ID `habimon-808e2`, and exercises only the local emulator. It does not read, seed, or write production Firestore data.
- `.github/workflows/firestore-rules.yml` runs this suite on relevant pushes and pull requests. The workflow needs no Firebase credentials and does not deploy Rules. Historical local `actionlint` validation passed on 2026-10-18/19 (not re-run on 2026-10-22); a hosted workflow run still needs a push or pull request.
- Unity Firebase SDK configuration remains in `Assets/Plugins/Android/google-services.json` and `Assets/Data/GoogleService-Info.plist`.

The initial Rules suite passed **6/6** against the local Firestore Emulator. After containment changes, it passed **9/9** on 2026-10-22 and again on 2026-10-24. These suites do not load the Unity native SDK.

Run the Rules suite from the repository root:

```bash
npm install
npm run test:firestore-rules
```

The test command starts only the Firestore Emulator, pins its project ID to `habimon-808e2`, and uses the local Rules file. The test runner refuses to start unless the expected emulator host is present.

## CLI verification

These commands inspect project/database configuration without deploying or writing documents:

```bash
npx -y firebase-tools@15.34.0 projects:list
npx -y firebase-tools@15.34.0 firestore:databases:list --project habimon-808e2
npx -y firebase-tools@15.34.0 firestore:databases:get '(default)' --project habimon-808e2
npx -y firebase-tools@15.34.0 use
```

The active CLI project should be `habimon-808e2`. The deployed policy is containment, not the final authorization policy; any further rollout must include the approved client compatibility and parent/business authorization plan.

## Unity integration status — latest local verification 2026-10-24

C# build passed with 0 warnings/errors; Unity EditMode passed 63/63; Parent popup/chart PlayMode passed 9/9. `npm run test:firestore-rules` passed 9/9. The explicit `FirebaseProvisioningEmulatorTests.ExistingAuthUserIsProvisionedOnLogin` test passed 1/1 using local Auth and Firestore emulators at `127.0.0.1`.

The 8 Firebase App/Auth/Firestore/Analytics `.so`/`.bundle` LFS objects are now hydrated; `git lfs fsck --objects` passes. The four Linux `.so` files are ELF x86-64 and the four `.bundle` files are Mach-O universal. User-local Git LFS 3.4.1 is installed. Unity native runtime no longer fails with `invalid ELF header` in the emulator integration test.

These results run in the current worktree and local emulators; they are **not clean-clone, Android/iOS target, device, hosted CI, or final business-authorization evidence**. Android/iOS builds, device OAuth/TTS, clean-clone dependency regeneration, complete business authorization and hosted CI remain unverified.

Test XML/log artifacts for the 24/10 run are under `/tmp` and are not versioned evidence. The results are summarized here and in the updated [remediation plan](CODEBASE_REMEDIATION_PLAN_2026-10-18.md).

These configuration, Rules, workflow and docs files remain local/uncommitted; `git ls-files docs` is empty at this snapshot. On 2026-10-22, two Rules releases were deployed and exactly two legacy verifier fields were removed from 29 parent documents under update preconditions. On 2026-10-24 the active Rules source was read-only rechecked and remained owner-scoped containment; no further production deploy, data write/delete, Neon migration, API deployment, stage, commit or push occurred. See the [current review report](CODEBASE_REVIEW_AND_CHANGE_REPORT_2026-10-18.md) and [60-finding matrix](CODEBASE_REMEDIATION_PLAN_2026-10-18.md).

## Action plan and additional production inventory — 2026-10-22

See [FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md](FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md) for the detailed implementation sequence, proposed interfaces, test cases, migration/recovery policy and closure criteria. The initial read-only inventory found 33 parents, 51 existing children plus 3 missing child ancestors, and 211 domain documents. At that time, password verifier fields existed on 29 parents (15 nonempty pairs, 14 empty); 29 PIN hashes matched the old default PIN; 19 children lacked a pet; 20 documents remained under missing child profiles. The inventory itself was read-only; subsequent Rules deployment and verifier-field deletion are recorded under Current post-action status. Counts are sequential-read observations, not an atomic snapshot.

The active account has document read/write and Rules-update permissions, but the IAM probe did not return managed export or IAM-policy-write permission. No backups/schedules were listed, PITR is disabled, Functions/Run APIs returned SERVICE_DISABLED and billing is not enabled. Do not describe those as tested deployment/recovery capabilities. Android/iOS registration/config matches, Google provider enablement and registered Android SHA-1/SHA-256 were read-only verified; device/build/signing-runtime validation remains separate.

The initial inventory counts are pre-action baseline. The current outcome is that custom password verifier fields are absent from all 33 parent documents in the readback and Rules no longer allow public access. PIN fields remain, and parent-role/business authorization and device findings remain open or partial.

## Current post-action status — rollout 2026-10-22; Rules re-read 2026-10-24

- Active Rules were deployed to `habimon-808e2`; current release is `3e9f7ec6-4218-45a7-8f1b-154f609327b1`. Readback matches the intended containment source: authenticated UID owner check, nested-subtree scope, and denial of attempts to reintroduce `passwordHash/passwordSalt`; there is no broad `allow read, write: if true` rule.
- Firebase MCP confirmed project `habimon-808e2` and re-read the active Firestore Rules on 2026-10-24. The source remains authenticated owner-scoped containment with no public wildcard. It is not final parent authorization because Parent and Child share the same Firebase Auth UID and owner-subtree writes remain broad.
- A preceding containment release was `ebe182b6-66d0-42b0-bba4-5acd731036a3`. The active policy is still not final: owner UID does not distinguish Parent from Child, and domain-field/operation allowlists plus server-authoritative PIN/elevation, deletion, approval, and reward flows are incomplete.
- Exactly 29 parent documents that had legacy `passwordHash/passwordSalt` fields were updated with a two-field update mask and per-document updateTime precondition. The subsequent parent-scope readback found 0 remaining `passwordHash` and 0 `passwordSalt`; `pincode` and unrelated fields were not changed by that operation.
- The updated local Rules tests pass 9/9 on the Emulator. This is test evidence for Rules behavior, not proof of Unity client compatibility or deployed business authorization.
- F24 and F25 therefore moved from the prior status to **partial**, not closed. The backend PIN code is documented in the separate [action plan](FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md); it is not deployed or integrated with this Firestore project.
