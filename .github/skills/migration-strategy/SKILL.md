---
name: migration-strategy
description: Structured database migration planning skill covering dry-run sequencing, rollback safety, tenant isolation, and zero-downtime posture for Alembic and schema changes.
---

# Migration Strategy Skill

Primary owners:
- `DataEngineer`
- `PlatformEngineer` for deploy-sequencing coordination

---

## When to Use

- New Alembic migration that adds, alters, or removes columns, tables, or indexes
- Any migration touching tenant-scoped tables or shared-tenant schemas
- Migrations that must coordinate with a code deploy (column rename, type change, FK addition)
- Backfill or data transformation migrations
- Vector schema changes in Qdrant (collection recreation, dimension change, metric change)

Do NOT invoke for pure additive nullable column additions with no consumer code change —
those are low-risk and can go through standard review.

---

## Migration Planning Pass

### 1. Change Classification

Classify the migration as one of:

- `additive` — new nullable column, new table, new index on existing data
- `destructive` — drop column, drop table, truncate, rename with data rewrite
- `transformative` — backfill, type coercion, FK addition, column rename requiring data copy
- `tenant-scoped` — any migration touching data partitioned by tenant_id or equivalent

Higher classification = more scrutiny required.

### 2. Downgrade Safety

Confirm:
- `downgrade()` is implemented and tested locally
- downgrade does not lose data that cannot be recovered
- downgrade is safe to run after the upgrade has committed live data

If downgrade is irreversible (e.g. DROP column with live data), require:
- explicit approval via `approval-packet`
- backup confirmation before execution

### 3. Zero-Downtime Check

For each migration, answer:
- does the app need to be offline during migration?
- if yes, what is the maintenance window requirement?
- if no, does the ORM layer support the intermediate schema state (column exists but nullable before code ships)?

Pattern guidance:
- add column nullable first, deploy code, then add NOT NULL constraint in a second migration
- rename: add new column, backfill, update code to write both, drop old column in a later migration
- never combine destructive DDL with large backfills in one migration script

### 4. Tenant Isolation Check

Explicitly answer for each affected table:
- is the table tenant-scoped?
- does the migration touch all tenants atomically or per-tenant?
- can a partial migration failure leave tenants in inconsistent states?
- does the migration require a per-tenant lock or row-level operation?

Flag any cross-tenant risk to SecurityEngineer.

### 5. Rollback Plan

State:
- rollback command (e.g. `alembic downgrade -1`)
- last safe checkpoint migration ID
- data recovery action if data was written between upgrade and rollback
- whether PlatformEngineer must coordinate the rollback with a code revert

---

## Migration Strategy Output

```text
MIGRATION STRATEGY

Migration:
- [alembic revision ID or description]

Classification:
- [additive | destructive | transformative | tenant-scoped]

Downgrade safety:
- [SAFE | IRREVERSIBLE — reason and required approval]

Zero-downtime posture:
- [YES | NO — required window or sequencing step]

Tenant isolation:
- [SAFE | RISK — finding]

Rollback plan:
- Command: [alembic downgrade ...]
- Checkpoint: [revision ID]
- Data recovery: [none | step]
- Code revert required: [yes | no]

Preconditions:
- [condition]

Recommendation:
- READY FOR MIGRATION
- CONDITIONAL — sequencing step required
- BLOCKED — destructive or tenant risk requires approval
```

---

## Guardrails

- Never approve a migration without a tested `downgrade()`.
- Destructive migrations on tenant-scoped tables require `approval-packet` before execution.
- Backfill migrations on tables with > 10k rows should be chunked or run as background jobs.
- Qdrant collection schema changes require recreation — treat as destructive and plan accordingly.
