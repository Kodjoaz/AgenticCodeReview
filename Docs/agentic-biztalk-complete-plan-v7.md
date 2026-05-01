# Agentic BizTalk Application Migration – Complete Assessment Plan (v7)

## Audience and Intent

This document is written for a team with **no prior background in agentic systems**. It explains **what each concept means**, **why it exists**, and **how all parts fit together** in our BizTalk migration assessment project.

This is not an AI experiment. It is an **engineered assessment system** that uses AI reasoning in a controlled, auditable way.

---

## 1. What “Agentic” Means (in plain terms)

An *agentic system* is **not a single AI**.

It is a **coordinated system of small, specialized components**, each with **one clear responsibility**, working together under strict rules.

You should think of it like an industrial process:

- Each station does one job
- Stations do not improvise
- The assembly line controls the order
- The final product is predictable and reviewable

---

## 2. The Four Core Building Blocks

Everything in this project is built from **four roles**:

1. **Orchestrator** – controls the flow
2. **Agent** – reasons about one topic
3. **Skill** – extracts raw facts
4. **Prompt** – encodes expert rules

If you understand these four, you understand the whole system.

---

## 3. Orchestrator (The Control Plane)

### What it is

The **Orchestrator** is the component that:
- decides *what runs next*
- ensures steps happen in the right order
- passes outputs from one step to the next
- handles failures safely

It is similar to:
- a workflow engine
- a Durable Functions orchestrator
- a saga coordinator

### What it does NOT do

The Orchestrator:
- does **not** analyze BizTalk
- does **not** interpret meaning
- does **not** apply rules or judgments

It only controls execution.

### Example

```
Run Inventory Agent
If successful → Run Dependency Agent
If 실패 → Stop assessment safely
```

---

## 4. Agent (Reasoning Unit)

### What an Agent is

An **Agent**:
- focuses on exactly **one topic**
- uses expert reasoning to interpret facts
- produces a **structured output**

Examples in this project:
- Messaging Patterns Agent
- Security Architecture Agent
- Long‑Running Process Agent

Each agent answers one question:
> *“What does the collected data tell us about THIS aspect?”*

### What an Agent is NOT

- Not a workflow engine
- Not a file parser
- Not a data extractor

Agents **never call other agents**.

---

## 5. Skill (Fact Extraction Only)

### What a Skill is

A **Skill** is a simple, deterministic action, such as:
- parse a BizTalk binding file
- read adapter configuration
- extract orchestration shapes
- decompile a DLL

Skills:
- take input
- return raw data
- produce the same result every time

### Golden rule

> **If something “thinks”, it is NOT a skill.**

Skills are intentionally boring.

---

## 6. Prompt (Encoded Expertise)

### What a Prompt is

A **Prompt** defines *how an Agent reasons*.

It contains:
- domain rules (BizTalk patterns)
- classification logic
- conservative assessment principles

Example:
- “Parallel + Receive + Correlation = Parallel Convoy”
- “Kerberos dependency = hard identity coupling”

Prompts never:
- execute code
- control workflows
- access files

---

## 7. Workflow (The Plan)

A **Workflow** is a static definition of:
- which agents must run
- in what order
- with which dependencies

The workflow is:
- interpreted by the Orchestrator
- not executed on its own

Think of it as a *recipe*, not a *chef*.

---

## 8. Routing: Who Decides What

There are **three different routing responsibilities**:

### 8.1 Execution Routing

**Who runs next?**

✅ Handled only by the Orchestrator

Examples:
- Inventory → Dependency
- Skip Pipeline Agent if no pipelines exist

---

### 8.2 Semantic Routing

**What kind of thing is this?**

✅ Done inside individual Agents using Prompts

Examples:
- Sequential vs Parallel Convoy
- Acceptable vs risky authentication

---

### 8.3 Consolidation Routing

**What does everything mean together?**

✅ Done by a dedicated Consolidation Agent

The consolidation agent:
- merges outputs
- resolves overlaps
- produces final conclusions

---

## 9. Consolidation Agent (Final Synthesis)

### Purpose

This agent:
- collects all agent outputs
- normalizes them
- builds the final assessment summary

It does not discover new facts.

This is the **only agent allowed to produce the final report structure**.

---

## 10. Full Execution Flow (End‑to‑End)

```
Orchestrator
  ├─ Inventory Agent
  ├─ Dependency Agent
  ├─ External Systems Agent
  ├─ Adapter & Pipeline Agents
  ├─ Messaging Patterns Agent
  ├─ Long‑Running Semantics Agent
  ├─ Security Architecture Agent
  ├─ Anti‑Patterns Agent
  ├─ Complexity & Risk Agent
  └─ Consolidation Agent
        ↓
   Final Migration Assessment
```

---

## 11. Why This Structure Is Safe for a First Agentic Project

This design ensures:
- no hidden logic
- full replayability
- explainable decisions
- easy debugging
- gradual learning curve

You can inspect any stage independently.

---

## 12. Non‑Negotiable Rules

1. Orchestrator controls execution
2. Agents reason only about their domain
3. Skills extract facts only
4. Prompts contain all judgment logic
5. Consolidation happens once, at the end

If these rules are respected, the system remains stable.

---

## 13. One‑Line Mental Model (Memorize)

> **Orchestrator controls the flow, Agents interpret facts, Skills extract data, Prompts encode expertise, Consolidation synthesizes meaning.**

---

## 14. Final Note

This document exists so that **any new team member** can understand:
- what an agentic system is
- how ours works
- where each responsibility lives

That clarity is what makes this project sustainable.
