import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { after, before, beforeEach, describe, it } from 'node:test';
import {
  assertFails,
  assertSucceeds,
  initializeTestEnvironment,
} from '@firebase/rules-unit-testing';
import {
  collection,
  deleteField,
  deleteDoc,
  doc,
  getDoc,
  getDocs,
  setDoc,
  updateDoc,
} from 'firebase/firestore';

const projectId = 'habimon-808e2';
const expectedEmulatorHost = '127.0.0.1:8080';
if (process.env.FIRESTORE_EMULATOR_HOST !== expectedEmulatorHost) {
  throw new Error(`Refusing Firestore Rules tests without emulator at ${expectedEmulatorHost}.`);
}

let testEnvironment;

before(async () => {
  testEnvironment = await initializeTestEnvironment({
    projectId,
    firestore: {
      host: '127.0.0.1',
      port: 8080,
      rules: readFileSync(new URL('../../firestore.rules', import.meta.url), 'utf8'),
    },
  });
});

beforeEach(async () => {
  await testEnvironment.clearFirestore();
  await testEnvironment.withSecurityRulesDisabled(async (context) => {
    const firestore = context.firestore();
    await setDoc(doc(firestore, 'users/parentA'), { email: 'parent-a@example.test' });
    await setDoc(doc(firestore, 'users/parentLegacy'), {
      email: 'legacy@example.test',
      passwordHash: 'fixture-hash',
      passwordSalt: 'fixture-salt',
    });
    await setDoc(doc(firestore, 'users/parentA/children/childA'), { name: 'An' });
    await setDoc(doc(firestore, 'users/parentA/children/childA/habits/habitA'), { title: 'Read' });
    await setDoc(doc(firestore, 'users/parentA/children/childA/habit_logs/logA'), { isCompleted: true });
    await setDoc(doc(firestore, 'users/parentA/children/childA/rewards/rewardA'), { name: 'Sticker' });
    await setDoc(doc(firestore, 'users/parentA/children/childA/habimon/status'), { petName: 'Milo' });
    await setDoc(doc(firestore, 'users/parentB'), { email: 'parent-b@example.test' });
    await setDoc(doc(firestore, 'users/parentB/children/childB'), { name: 'Bình' });
  });
});

after(async () => {
  await testEnvironment?.cleanup();
});

describe('Firestore owner access', () => {
  it('denies unauthenticated access to parent and child data', async () => {
    const firestore = testEnvironment.unauthenticatedContext().firestore();

    await assertFails(getDoc(doc(firestore, 'users/parentA')));
    await assertFails(getDoc(doc(firestore, 'users/parentA/children/childA')));
    await assertFails(getDoc(doc(firestore, 'users/parentA/children/childA/habits/habitA')));
    await assertFails(setDoc(doc(firestore, 'users/parentA/children/childA/habits/unauthorized'), { title: 'No' }));
  });

  it('allows an authenticated owner to use the app document paths', async () => {
    const firestore = testEnvironment.authenticatedContext('parentA').firestore();

    await assertSucceeds(getDoc(doc(firestore, 'users/parentA')));
    await assertSucceeds(getDocs(collection(firestore, 'users/parentA/children')));
    await assertSucceeds(getDoc(doc(firestore, 'users/parentA/children/childA/habits/habitA')));
    await assertSucceeds(getDoc(doc(firestore, 'users/parentA/children/childA/habit_logs/logA')));
    await assertSucceeds(getDoc(doc(firestore, 'users/parentA/children/childA/rewards/rewardA')));
    await assertSucceeds(getDoc(doc(firestore, 'users/parentA/children/childA/habimon/status')));
    await assertSucceeds(updateDoc(doc(firestore, 'users/parentA'), { email: 'parent-a-updated@example.test' }));
    await assertSucceeds(updateDoc(doc(firestore, 'users/parentA/children/childA'), { age: 8 }));
    await assertSucceeds(setDoc(doc(firestore, 'users/parentA/children/childA/habits/habitB'), { title: 'Draw' }));
    await assertSucceeds(setDoc(doc(firestore, 'users/parentA/children/childA/habit_logs/logB'), { isCompleted: false }));
    await assertSucceeds(updateDoc(doc(firestore, 'users/parentA/children/childA/rewards/rewardA'), { cost: 2 }));
    await assertSucceeds(updateDoc(doc(firestore, 'users/parentA/children/childA/habimon/status'), { happiness: 99 }));
    await assertSucceeds(deleteDoc(doc(firestore, 'users/parentA/children/childA/habits/habitA')));
  });

  it('allows a user to create its own parent document', async () => {
    const firestore = testEnvironment.authenticatedContext('parentC').firestore();

    await assertSucceeds(setDoc(doc(firestore, 'users/parentC'), { email: 'parent-c@example.test' }));
  });

  it('denies a different authenticated user access to another owner subtree', async () => {
    const firestore = testEnvironment.authenticatedContext('parentB').firestore();

    await assertFails(getDoc(doc(firestore, 'users/parentA')));
    await assertFails(getDocs(collection(firestore, 'users/parentA/children')));
    await assertFails(getDoc(doc(firestore, 'users/parentA/children/childA/habits/habitA')));
    await assertFails(setDoc(doc(firestore, 'users/parentC'), { email: 'forged@example.test' }));
    await assertFails(setDoc(doc(firestore, 'users/parentA/children/childA/habits/foreign'), { title: 'No' }));
  });

  it('denies documents outside the app owner subtree', async () => {
    const firestore = testEnvironment.authenticatedContext('parentA').firestore();

    await assertFails(getDoc(doc(firestore, 'publicConfig/current')));
    await assertFails(setDoc(doc(firestore, 'publicConfig/current'), { enabled: true }));
  });

  it('denies orphaned child subcollections without a parent document', async () => {
    const firestore = testEnvironment.authenticatedContext('orphan').firestore();

    await assertFails(setDoc(doc(firestore, 'users/orphan/children/childA/habits/habitA'), { title: 'No parent' }));
  });

  it('does not allow the nested owner wildcard to delete a parent document', async () => {
    const firestore = testEnvironment.authenticatedContext('parentA').firestore();

    await assertFails(deleteDoc(doc(firestore, 'users/parentA')));
  });

  it('allows verifier retirement but blocks creation or reintroduction', async () => {
    const firestore = testEnvironment.authenticatedContext('parentLegacy').firestore();
    const parentRef = doc(firestore, 'users/parentLegacy');

    await assertSucceeds(updateDoc(parentRef, { email: 'legacy-updated@example.test' }));
    await assertSucceeds(updateDoc(parentRef, {
      passwordHash: deleteField(),
      passwordSalt: deleteField(),
    }));
    await assertFails(updateDoc(parentRef, { passwordHash: 'replacement' }));
    await assertFails(setDoc(doc(firestore, 'users/parentNew'), {
      email: 'new@example.test',
      passwordHash: 'fixture-hash',
      passwordSalt: 'fixture-salt',
    }));
  });

  it('denies cross-owner writes to every current child data path', async () => {
    const firestore = testEnvironment.authenticatedContext('parentB').firestore();

    await assertFails(updateDoc(doc(firestore, 'users/parentA/children/childA'), { name: 'Changed' }));
    await assertFails(setDoc(doc(firestore, 'users/parentA/children/childA/habits/foreign'), { title: 'No' }));
    await assertFails(setDoc(doc(firestore, 'users/parentA/children/childA/habit_logs/foreign'), { isCompleted: true }));
    await assertFails(updateDoc(doc(firestore, 'users/parentA/children/childA/rewards/rewardA'), { cost: 0 }));
    await assertFails(updateDoc(doc(firestore, 'users/parentA/children/childA/habimon/status'), { gold: 999 }));
    await assertFails(deleteDoc(doc(firestore, 'users/parentA/children/childA')));
  });
});
