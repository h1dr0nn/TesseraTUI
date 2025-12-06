# 🤖 Tessera – Agents Overview  
A clear guide to how each agent (or module) in Tessera operates, communicates, and keeps the system stable.  
Written for maintainability and clarity — not for end-users.

---

## 1. Purpose of Agents

Tessera is split into small, focused “agents.”  
Each agent handles a single responsibility: UI rendering, data syncing, validation, file IO, etc.

This separation ensures:
- easier debugging  
- better testability  
- predictable behavior  
- clean architecture for future extensions (formulas, plugins, Unity integration)

---

## 2. Agent List

### **2.1. UI Agents (Avalonia)**  
Handle rendering, interactions, and state presentation.  
These agents never contain core logic — they only reflect the Unified Data Core.

#### **TableViewAgent**
- Renders virtualized grid  
- Handles edit, copy/paste, selection  
- Sends cell updates to DataSyncAgent  
- Displays validation errors from ValidationAgent

#### **SchemaViewAgent**
- Shows inferred types, nullable, ranges, distinct values  
- Applies schema changes to SchemaAgent  
- Warns user before schema-breaking edits

#### **JsonViewAgent**
- Displays prettified JSON  
- Validates JSON syntax on edit  
- Sends JSON → Table sync via DataSyncAgent  
- Prevents invalid JSON from overwriting data

#### **NavigationAgent**
- Manages sidebar, tabs, theme switching, window states  

---

### **2.2. Core Logic Agents (Unified Data Core)**  
These agents hold **the real data** and handle all transformation logic.

#### **TableAgent**
- Stores rows, cells, and metadata  
- Applies cell updates  
- Efficient lookups for large CSVs  
- Exposes safe mutation APIs

#### **SchemaAgent**
- Holds column types, rules, nullable/support  
- Performs schema inference  
- Validates full-column rules  
- Communicates errors clearly back to UI Agents

#### **JsonAgent**
- Converts TableModel ↔ JSONModel  
- Validates structure, missing keys, type conflicts  
- Provides diff before applying changes

#### **ValidationAgent**
- Core validation engine for all views  
- Type checking, rule enforcement  
- Batch validation after major changes  
- Guarantees: *no invalid state ever reaches disk*

#### **DataSyncAgent**
- Central hub that syncs:
  - Table ↔ Schema  
  - Table ↔ JSON  
  - JSON ↔ Schema  
- Ensures “one source of truth”  
- Rejects invalid updates and rolls back safely

---

### **2.3. IO Agents (File Services)**

#### **CsvFileAgent**
- Efficient CSV load/save  
- Handles quote rules, delimiters, encoding  
- Preserves original structure wherever possible

#### **JsonFileAgent**
- Reads/writes prettified JSON  
- Ensures JSON output matches schema

#### **SchemaFileAgent**
- Saves schema alongside CSV  
- Ensures future openings load consistent types

---

### **2.4. System Agents**

#### **LoggingAgent**
- Centralized logs for debug, validation, errors  
- Structured output for bug reports

#### **SettingsAgent**
- Manages user preferences, theme, last-open paths  
- Cross-platform safe storage

#### **ClipboardAgent**
- Safe copy/paste for large table selections

---

## 3. Agent Communication Model
```
UI Agents  →  DataSyncAgent  →  Core Agents  →  ValidationAgent
    ↑                                                 ↓
    └─────────────────── Feedback to UI ──────────────┘
```
Rules:

- UI NEVER updates data directly.  
- All changes go through **DataSyncAgent**.  
- Only **ValidationAgent** decides what is valid.  
- Core Agents never talk to UI; they only return results.  
- If an update fails, DataSyncAgent rolls back.

This ensures no UI bug can corrupt data.

---

## 4. Agent Behavior Guarantees

- **Consistency:** All views always represent the same data.  
- **No invalid save:** Validation must pass before writing any file.  
- **Predictable updates:** Only the DataSyncAgent can mutate data.  
- **Crash safety:** A failed sync cannot damage the underlying models.  
- **Extensibility:** New features (formula engine, plugins) plug into Core Agents without touching UI.

---

## 5. Future Agent Expansion

The architecture supports new agents easily:

- **FormulaAgent** (Phase 8)  
- **PluginAgent** (Phase 10)  
- **UnityBridgeAgent** (Phase 9)  
- **StatsAgent** (data profiling)  
- **HistoryAgent** (advanced versioning)

These plug into the DataSyncAgent without breaking existing flows.

---

## 6. Commit Naming Convention

To keep the history clean and consistent, Tessera follows a simple structured commit style.

### 6.1. Format

`type(scope): short description`

- `type` = category of change  
- `scope` = the module/agent affected  
- `short description` = imperative, concise, lowercase

**Examples:**
- `feat(core): add unified data core models`
- `feat(ui-table): implement basic table view editing`
- `fix(validation): prevent invalid json from being applied`
- `refactor(agents): split datasync logic`
- `chore(repo): init solution structure`
- `test(core): add schema inference tests`

---

### 6.2. Allowed Types

- `feat` — new feature  
- `fix` — bug fix  
- `refactor` — internal code changes  
- `perf` — performance improvement  
- `test` — unit/integration tests  
- `chore` — repo maintenance, configs, tools  
- `docs` — documentation updates  

---

### 6.3. Example Scopes

- `core`, `schema`, `table`, `json`, `validation`  
- `ui-table`, `ui-schema`, `ui-json`, `navigation`  
- `agents`, `datasync`, `io-csv`, `io-json`  
- `build`, `package`, `unity`, `ci`

---

## 7. Summary

Tessera’s agent architecture ensures the editor is:

- stable  
- predictable  
- safe for critical data pipelines  
- easy to maintain  
- ready for long-term expansion  

Every feature — now and future — flows through these agents, keeping Tessera clean and robust.
