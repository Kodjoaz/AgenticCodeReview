---
name: api-contracts
description: Maintain backend/frontend API contracts, schema sync, and contract validation workflows.
---

# API Contracts Skill

**For Backend and Frontend engineers to maintain synchronous API schemas**: Automated OpenAPI/JSON Schema validation, type generation, and contract testing ensure Backend-Frontend APIs never drift out of sync.

## The Problem We Solve

**Without this skill**:
- Backend changes endpoint schema -> Opens GitHub issue -> Frontend manually updates types -> Sometimes missed -> Runtime errors in production
- No single source of truth for API contracts
- Type mismatches caught too late (in testing or in prod)

**With this skill**:
- Backend updates `openapi.yaml` -> CI auto-validates -> TypeScript types auto-generated -> Frontend gets PR with updates -> Everyone sees the contract

## When to Use

- Adding a new API endpoint
- Changing request/response shape
- Adding/removing query parameters
- Changing authentication headers
- Adding validation rules (min/max, required fields)
- Versioning API changes

## The Workflow

### For Backend Engineers: Adding an Endpoint

1. **Design the contract first**:
   ```yaml
   # config/openapi.yaml
   /api/v1/agents/{id}/stats:
     get:
       summary: Get agent usage statistics
       parameters:
         - name: id
           in: path
           required: true
           schema: { type: string }
         - name: days
           in: query
           schema: { type: integer, minimum: 1, maximum: 365 }
       responses:
         200:
           content:
             application/json:
               schema: { $ref: '#/components/schemas/AgentStats' }

   components:
     schemas:
       AgentStats:
         type: object
         properties:
           agent_id: { type: string }
           total_requests: { type: integer }
           latency_p95_ms: { type: number }
           error_rate: { type: number, minimum: 0, maximum: 1 }
   ```

2. **Commit to `config/openapi.yaml`**:
   ```bash
   git add config/openapi.yaml
   git commit -m "feat: add GET /api/v1/agents/{id}/stats endpoint"
   ```

3. **CI validates the contract**:
   - Schema is valid JSON Schema
   - No breaking changes (unless explicitly versioned)
   - All properties have descriptions
   - All responses documented

4. **TypeScript types auto-generated** (via CI workflow):
   ```bash
   openapi-generator generate -i config/openapi.yaml -g typescript-axios
   ```

5. **PR created automatically with generated types** (via GitHub Actions):
   - FrontendEngineer reviews + merges
   - Types are now in sync with backend

6. **Implement the backend endpoint**:
   ```python
   # src/control-center/core/app/routers/agents.py
   @router.get("/agents/{id}/stats")
   async def get_agent_stats(id: str, days: int = Query(7, ge=1, le=365)):
       # Matches the OpenAPI contract exactly
       return {
           "agent_id": id,
           "total_requests": 1234,
           "latency_p95_ms": 250.5,
           "error_rate": 0.02
       }
   ```

### For Frontend Engineers: Consuming an Endpoint

1. **Check generated types** (auto-updated by backend PR):
   ```typescript
   import { AgentStats } from '@api/types';

   const fetchAgentStats = async (agentId: string): Promise<AgentStats> => {
       const response = await apiClient.get(`/api/v1/agents/${agentId}/stats`, {
           params: { days: 7 }
       });
       return response.data;
   };
   ```

2. **Types are guaranteed to match backend** (contract-driven):
   - TypeScript compiler ensures you're passing correct query params
   - Response type is guaranteed to match what backend sends
   - No manual type synchronization needed

## Contract Files & Their Role

### **`config/openapi.yaml`** — Source of Truth
- Single YAML file that defines all API contracts
- Version-controlled in Git
- Updated by Backend when endpoints change
- Validated by CI/CD

### **Generated TypeScript Types** (`src/control-center/frontend/api/generated/types.ts`)
- Auto-generated from `openapi.yaml`
- Pulled in by `openapi-generator` CI step
- Read-only (never manually edit)
- Regenerated on every Backend contract update

### **Backend Pydantic Models** (`src/control-center/core/app/schemas/*.py`)
- Define request/response validation
- Must match the OpenAPI schema exactly
- Use `@dataclass` or Pydantic `BaseModel` with matching field names/types

## CI/CD Integration

### GitHub Actions Workflow: `validate-api-contract.yml`
```yaml
on:
  push:
    paths:
      - config/openapi.yaml
      - src/control-center/core/app/schemas/**
      - src/control-center/core/app/routers/**

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - name: Validate OpenAPI schema
        run: |
          npx openapi-validator config/openapi.yaml

      - name: Check for breaking changes
        run: |
          npx openapi-diff main config/openapi.yaml

      - name: Generate TypeScript types
        run: |
          openapi-generator generate -i config/openapi.yaml -g typescript-axios -o temp-types

      - name: Create PR with updated types
        if: github.ref == 'refs/heads/main'
        run: |
          cp temp-types/* src/control-center/frontend/api/generated/
          git add src/control-center/frontend/api/generated/types.ts
          git commit -m "chore(deps): update API types from openapi.yaml"
          gh pr create --title "chore: API types auto-updated" --body "Generated from openapi.yaml"
```

## Best Practices

1. **Document every parameter**: Add `description:` to all fields in OpenAPI schema
2. **Use versioning for breaking changes**: `v1 -> v2` path versioning, not deprecation
3. **Provide examples**: Include example requests/responses in OpenAPI
4. **Test the contract**: Backend unit tests validate responses match schema
5. **Link GitHub issues**: If contract changes address a bug, reference in commit message

## Common Mistakes to Avoid

- [ ] **Changing response schema without updating OpenAPI**: CI will catch this
- [ ] **Frontend working from outdated types**: Always regenerate when Backend updates
- [ ] **No parameter validation**: OpenAPI schema serves as contract + validation specification
- [ ] **Undocumented fields**: Every field must have `description:` and `type:`

## Integration with conventional-commits

When updating API contracts:
- **New endpoint**: `feat: add GET /api/v1/agents/{id}/stats`
- **Breaking change**: `feat!: change response format for GET /api/v1/agents`
- **Fix validation**: `fix: require agent_id in POST /api/v1/agents`
- **Type-only**: `chore: regenerate types from openapi.yaml`

See `conventional-commits` skill for full convention.

## Debugging Contract Mismatches

### TypeError: Cannot read property 'latency_p95_ms' of undefined
-> Backend endpoint is returning unexpected response shape
-> Check: Does backend match OpenAPI schema? Run `openapi-validator`

### 400 Bad Request: Missing required parameter
-> Frontend sending wrong query parameter
-> Check: TypeScript types are stale. Regenerate from `openapi.yaml`

### CI pipeline failed: Breaking change detected
-> You changed response schema
-> Decision: Is this intentional? Update OpenAPI version or adjust schema
