# User App Logic

This document describes the application **from the user's point of view**, not from the current code implementation.
It is a behavioral specification: we first define **what the app must do**, and only then design the target architecture to support it.

This document is intentionally not tied to current classes, services, or layers. It defines:
- what user scenarios exist;
- what application and torrent states exist;
- what the user can and cannot do;
- what behavior is considered correct;
- what constraints and invariants must always hold.

---

## 1. Application Purpose

The application is a desktop torrent client for Windows.
Users use it to:
- add torrent files and magnet links;
- choose which files to download;
- see download state;
- control downloads;
- persist state between launches;
- use it as a normal desktop app, including minimizing to tray.

Core user value:
- the torrent list is not lost;
- user actions are not lost;
- the app restores predictably after restart;
- UI state matches what is actually happening in the torrent engine.

---

## 2. User-Facing Entities

From the user perspective, the app has these core entities.

### 2.1. Torrent
A torrent is a single list item with understandable properties:
- name;
- status;
- progress;
- download speed;
- upload speed;
- size;
- save path;
- file list;
- date added.

The user should not need to know internal entities like `manager`, `snapshot`, `catalog`, `engine state`, etc.

### 2.2. Settings
Settings are persisted app parameters that:
- are displayed in UI;
- can be changed in UI;
- are explicitly saved;
- affect app behavior after save/apply.

### 2.3. Main Window
The main window is the primary way to use the app.
The user expects that:
- it opens quickly;
- the torrent list appears immediately or as early as possible;
- opening pages does not freeze the window;
- the window can be closed, minimized to tray, and restored.

### 2.4. Tray
Tray is an additional shell scenario, not a separate app mode.
The user expects that:
- tray appears only when needed;
- the main window can be restored from tray;
- the app can be fully exited from tray.

---

## 3. Core User Scenarios

### 3.1. First App Launch
The user launches the app for the first time.

Expected behavior:
- app opens without freezing;
- torrent list is empty;
- add commands are available;
- settings have sensible defaults.

The app must not:
- show invalid phantom torrents;
- freeze due to engine initialization;
- require technical/manual bootstrap steps from user.

### 3.2. Subsequent App Launch
The user has used the app before and launches it again.

Expected behavior:
- torrent list appears immediately from persisted state;
- app does not wait for full torrent engine startup before showing list;
- after engine initialization, state syncs with real runtime;
- previously running torrents are started again;
- previously paused torrents remain paused;
- commands issued before full engine startup are not lost.

This is one of the most important scenarios in the project.

### 3.3. Add `.torrent` File
The user selects a `.torrent` file.

Expected behavior:
1. app reads torrent metadata;
2. app shows preview;
3. user can:
   - cancel;
   - choose destination folder;
   - select only part of files;
4. after confirmation, torrent appears in list;
5. selected files are downloaded, unselected files are not.

If torrent is already added:
- user gets a clear message;
- app does not create a duplicate.

### 3.4. Add Magnet Link
The user pastes a magnet link.

Expected behavior:
- link is accepted;
- app first resolves magnet metadata;
- app then normalizes magnet input into the same prepared torrent representation used by `.torrent`;
- app then opens the **same preview and validation flow** used for `.torrent`;
- user confirms through this single unified preview flow.

Clarification: behavior for magnet and `.torrent` must be unified as early as reasonably possible and follow one shared logic path after metadata is available.

### 3.5. Add Torrent via File Association
The user opens a `.torrent` file from Windows Explorer.

Expected behavior:
- if app is closed, app starts and handles the file;
- if app is already running, app accepts the file without breaking current state;
- add flow goes through the same preview/validation logic as UI add flow.

### 3.6. View Torrent List
User sees torrent list as the main app screen.

Expected behavior:
- list does not jump unexpectedly;
- items do not disappear on their own;
- list ordering is stable and understandable;
- list is restored after restart;
- status updates do not break user selection.

### 3.7. Start Torrent
User presses start.

Expected behavior:
- torrent transitions to active state;
- when engine is ready, real download/seeding begins;
- status and speeds update;
- user choice is persisted for next launch.

### 3.8. Pause Torrent
User presses pause.

Expected behavior:
- torrent stops active download/seeding;
- download speed becomes 0;
- upload speed becomes 0;
- visual status matches user-stopped state;
- this state is persisted across restarts.

Note: internal runtime states `Paused` and `Stopped` may differ, but in UI they must be shown as one unified user-facing state.

### 3.9. Remove Torrent
User removes torrent from list.

Expected behavior:
- record disappears from list;
- state is removed from persisted catalog;
- torrent does not return after restart;
- if engine is not initialized yet, remove intent is still applied.

Removal mode must be chosen by the user at removal time:
- remove only record from client;
- or remove downloaded files from disk too.

Neither mode has product-level priority over the other.

### 3.10. Open Torrent Folder
User wants to open torrent download folder.

Expected behavior:
- Windows Explorer opens;
- correct path is opened;
- if path does not exist, user gets a clear message.

### 3.11. Change Settings
User opens settings page.

Expected behavior:
- page opens quickly;
- fields show current values;
- changes first live in local form state;
- pressing Apply persists settings;
- after save, required settings are applied.

Behavior should be uniform for all settings on the same page.

### 3.12. Close Application Window
User clicks the main window close button (`X`).

Expected behavior:
- if **"Minimize to tray on close" is enabled**, the app hides to tray;
- if this option is disabled, the app exits fully.

Clarification: this rule applies specifically to **main window close action**, not tray menu exit.

### 3.13. Exit from Tray
User selects `Exit` in tray menu.

Expected behavior:
- app exits fully;
- "Minimize to tray on close" setting is ignored in this case;
- state is saved correctly.

### 3.14. Recovery After Invalid Previous State
Previous shutdown may have been non-ideal, or engine state may be damaged.

Expected behavior:
- app restores list from catalog whenever possible;
- if runtime state is broken and corresponding torrents no longer exist in real engine, app is not required to keep them in UI;
- app degrades safely: no freeze, no startup break, no misleading fake runtime items.

---

## 4. What User Can Do Before Full Engine Initialization

This is a separate critical logic area.

Before full engine startup, user must still be able to:
- see cached torrent list;
- start torrent;
- pause torrent;
- remove torrent;
- view/change settings;
- close or minimize app.

If action is performed before engine init:
- system must accept it;
- system must persist it as user intent;
- system must apply it after engine is ready.

User does not need to know action was deferred.
The app should behave like a normal client.

---

## 5. User-Facing Torrent States

From user perspective, torrent has understandable states.

Minimum set:
- waiting for client initialization;
- fetching metadata;
- checking data;
- downloading;
- seeding;
- paused/stopped (single user-facing state);
- error.

Important:
- user-facing states do not need 1:1 mapping with internal MonoTorrent states;
- internal intermediate states are allowed, but UI must map them to understandable user states.

### Special Startup Rule
At app startup, app may first show cached state, then refine with live runtime.
User must not see broken behavior like:
- everything looked running, then suddenly appears paused without reason;
- torrent disappears from list;
- same torrent appears as duplicate.

---

## 6. User Behavior Invariants

Rules below must always hold.

### 6.1. One Torrent = One List Item
Same user torrent must not exist in UI twice.

### 6.2. Adding Existing Torrent Must Not Create Duplicate
If user adds already-added torrent:
- new item is not created;
- user gets clear message.

### 6.3. List Must Survive Restarts
If user did not remove torrent, it must be visible after next launch.

### 6.4. User Intent Is Stronger Than Transitional Startup State
If user left torrent running, system should try to restore running.
If user left torrent paused, system must not auto-start it.

### 6.5. Actions Before Engine Startup Are Not Lost
Pause, Start, Remove before MonoTorrent init must be applied later.

### 6.6. Settings Behave Uniformly
Settings on one page must follow same model:
- edit;
- see unsaved changes;
- save;
- apply.

### 6.7. Explicit Exit Overrides Regular Close
Tray-menu exit is always full exit.

### 6.8. UI Must Stay Responsive
App must not freeze:
- on startup;
- when opening settings;
- during active torrent transfer;
- during tray updates.

---

## 7. What User Should Not Notice

Internal complexity must not leak into user layer.

User should not need to know:
- list is shown from cache first, then synced with runtime;
- some commands are queued as intent before engine startup;
- MonoTorrent has manager registration constraints;
- some statuses are restored through intermediate technical entities.

All this is allowed internally, but externally must appear as:
- clear state;
- clear messages;
- predictable behavior.

---

## 8. User Errors and Messages

Errors must be:
- localized;
- human-readable;
- free of low-level technical wording unless needed.

### Examples of Good Messages
- `This torrent is already added to the application.`
- `Failed to open torrent folder.`
- `Failed to load torrent file.`
- `Failed to apply settings.`

### Examples of Bad Messages
- raw MonoTorrent exceptions;
- messages with internal class names;
- messages where user cannot understand next step.

---

## 9. Constraints and Not-Yet-Finalized Decisions

Some questions are not fully implemented yet or require explicit product decisions.

### 9.1. Delete Data from Disk
User must explicitly choose removal mode at action time:
- remove record from client only;
- remove record and downloaded files from disk.

No default priority between these options at product-logic level.

### 9.2. File Priorities
If deeper per-file priority management is added in future, define it as separate scenario.

### 9.3. Deep Engine Error Handling
Right now, clear user messages are more important than exposing full technical details.

---

## 10. What Is Especially Important for Future Architecture

This document is not just UI description; it is a base for target architecture.

Architecture must clearly support these responsibility levels:

### 10.1. User Scenarios
A dedicated layer should model user actions:
- add torrent;
- add magnet;
- start;
- pause;
- remove;
- apply settings;
- close window;
- exit app.

### 10.2. User Intent
System must store not only runtime state, but also **user intent**:
- should torrent be running;
- should torrent be removed;
- should state be restored after startup.

### 10.3. Cache-Runtime Synchronization
This is a separate system responsibility.
It should not be scattered across UI, random services, or event handlers.

### 10.4. State Model
There should be one clear user state model, not a mix of:
- raw MonoTorrent states;
- temporary UI hacks;
- special startup-only branches.

### 10.5. Separate Settings Model
Settings should be modeled in a consistent way:
- read model;
- edit model;
- save/apply scenario.

---

## 11. What to Do Next After Approving This Document

After this document is accepted as "how app must behave", next step is a **target architecture** document.

It must answer:
- which rules are domain rules;
- which scenarios are application-level;
- what remains infrastructure adapter logic;
- which models are source of truth;
- which commands/events must exist in the system.

So the next document should answer not:
- **"what should user see?"**

but:
- **"what architecture is needed to implement this behavior reliably and simply?"**
