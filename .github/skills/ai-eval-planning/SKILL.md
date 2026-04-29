---
name: ai-eval-planning
description: Upfront AI evaluation and capacity planning for RAG pipelines, model changes, embedding upgrades, and inference reliability work.
---

# AI Eval Planning Skill

Primary owners:
- `AIEngineer`
- `QualityEngineer` for release gate alignment

---

## When to Use

- New model added to LiteLLM config or registry
- RAG pipeline change (chunking strategy, embedding model, retrieval parameters, re-ranking)
- Embedding model upgrade or dimension change
- KB index rebuild (Qdrant collection recreation or schema change)
- Prompt template change with material output-quality implications
- Inference reliability or fallback routing change
- Any change where the question "does it produce better results?" cannot be answered by a unit test

---

## Eval Planning Pass

### 1. Change Type and Quality Risk

Classify the change as one of:

- `model-swap` — new LLM or embedding model, same task
- `pipeline-change` — chunking, retrieval, re-ranking, or post-processing modification
- `config-change` — temperature, max_tokens, system prompt, routing weights
- `infra-change` — deployment, scaling, fallback routing, token budget
- `kb-rebuild` — index recreation, collection schema change, full re-ingest

Higher quality risk = more eval coverage required.

### 2. Evaluation Criteria

For each change, define:
- the observable quality signal (e.g. retrieval precision, answer relevance, latency p95)
- a baseline measurement from the current system
- a minimum acceptable threshold for the new version
- a method to measure: automated eval, human spot-check, or A/B comparison

Do not ship a model or pipeline change without a defined threshold.

### 3. Evaluation Data Set

Specify:
- representative query or input set (min 10 samples for smoke, 50+ for release)
- known-good expected outputs or rubric for subjective quality
- edge cases: empty context, multi-language, long inputs, adversarial prompts

### 4. Capacity and Cost Check

For the new configuration, estimate:
- tokens per request (prompt + completion)
- requests per minute at expected load
- GPU / CPU headroom at peak
- embedding dimension impact on Qdrant index size and query latency
- monthly token cost delta at current usage level

If capacity is unknown, flag to PlatformEngineer before rollout.

### 5. Rollback Criteria

Define:
- what triggers rollback (quality degradation, latency spike, cost overrun, error rate)
- how to revert (config rollback, previous model tag, previous collection version)
- who monitors the rollout signal and for how long

---

## Eval Planning Output

```text
AI EVAL PLAN

Change:
- [one-line description]

Change type:
- [model-swap | pipeline-change | config-change | infra-change | kb-rebuild]

Quality signal:
- [metric] — baseline: [value] — threshold: [minimum acceptable]

Eval method:
- [automated | spot-check | A/B]

Eval data set:
- [size and source]

Edge cases to cover:
- [case]

Capacity estimate:
- Tokens/request: [value]
- Peak RPM capacity: [value]
- Cost delta: [+/- estimate]
- Qdrant index impact: [none | size delta]

Rollback trigger:
- [condition]

Rollback action:
- [step]

Recommendation:
- READY FOR ROLLOUT
- CONDITIONAL — eval threshold or capacity gap
- BLOCKED — no baseline, no threshold, or capacity risk unresolved
```

---

## Guardrails

- Do not ship a model swap or RAG pipeline change without at least a smoke eval on 10 representative queries.
- Embedding dimension changes require a full KB rebuild — treat as destructive and coordinate with DataEngineer.
- If token cost delta exceeds 20%, require explicit capacity approval before rollout.
- LLM provider changes that touch auth or key routing must involve SecurityEngineer.
