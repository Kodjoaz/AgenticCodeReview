---
name: deployment-readiness
description: Run pre-release readiness checks for testing, docs, security, and deployment.
---

# Deployment Readiness Skill

**For all agents before claiming a task complete**: Pre-flight checklist ensuring work is production-ready, tested, documented, and validated before pushing to cloud.

## When to Use

- **Before marking any task DONE**
- **Before merging a PR** (automated in CI/CD)
- **Before deploying to production** (PlatformEngineer responsibility)

## The Checklist

### 🧪 Testing (QualityEngineer Validates)

- [ ] **Unit tests added/updated**
  - [ ] Code coverage >= 80%?
  - [ ] All happy paths tested?
  - [ ] At least one unhappy path tested (error case)?
  - [ ] Tests pass locally?
  - [ ] Tests pass in CI?

- [ ] **Integration tests** (if cross-domain)
  - [ ] Backend + Frontend tested together?
  - [ ] Database migrations tested?
  - [ ] External services mocked?

- [ ] **Manual testing performed**
  - [ ] Feature works as described in acceptance criteria?
  - [ ] No obvious UX issues?
  - [ ] Performance acceptable (not degraded)?

### Documentation

- [ ] **Code comments** added where non-obvious
  - [ ] Why (not what) is documented?
  - [ ] Complex algorithms explained?
  - [ ] External API integrations documented?

- [ ] **README/guide updated** (if user-facing)
  - [ ] New feature documented?
  - [ ] Configuration options listed?
  - [ ] Troubleshooting guide added?

- [ ] **CHANGELOG updated**
  ```markdown
  ## [Unreleased]
  ### Added
  - New agent statistics dashboard showing model performance metrics

  ### Fixed
  - Fixed memory leak in LLM connection pool
  ```

- [ ] **API documentation** (if endpoint added)
  - [ ] OpenAPI spec updated (`config/openapi.yaml`)?
  - [ ] Request/response examples provided?
  - [ ] Error codes documented?

### Security & Compliance (SecurityEngineer Validates)

- [ ] **Security review passed**
  - [ ] No hardcoded secrets?
  - [ ] Input validation in place?
  - [ ] RBAC/auth enforced?
  - See `security-review-checklist` skill

- [ ] **Compliance checks passed** (if applicable)
  - [ ] PII handling compliant?
  - [ ] Audit logging in place?
  - [ ] Data retention policies followed?

### 🚀 Deployment Readiness (PlatformEngineer Validates)

- [ ] **Code quality gates passed**
  - [ ] Lint errors: 0
  - [ ] Type errors: 0
  - [ ] Unused imports/variables: 0

- [ ] **Dependencies updated**
  - [ ] Requirements.txt/package.json committed?
  - [ ] Lock files up-to-date?
  - [ ] No security vulnerabilities detected?

- [ ] **Configuration for all environments**
  - [ ] Local development config works?
  - [ ] Staging config tested?
  - [ ] Production config ready?
  - [ ] Environment variables documented?

- [ ] **Database migrations** (if schema changed)
  - [ ] Migration file created?
  - [ ] Migration tested against prod-like data?
  - [ ] Rollback procedure documented?

- [ ] **Container/image updated** (if infrastructure change)
  - [ ] Dockerfile syntax valid?
  - [ ] Build passes without warnings?
  - [ ] Image scanned for vulnerabilities?

### Acceptance Criteria Met

- [ ] **All acceptance criteria from task completed**
  - [ ] Acceptance criteria from GitHub issue/PR met?
  - [ ] No deferred features (MVP completed)?

- [ ] **Known limitations documented**
  - [ ] If feature is MVP, what's phase 2?
  - [ ] If scaling is limited, document the limit?

### 📊 Monitoring & Observability

- [ ] **Logging added** (especially for errors)
  - [ ] Errors logged with context?
  - [ ] No sensitive data in logs?
  - [ ] Log level set appropriately?

- [ ] **Metrics added** (if performance-critical)
  - [ ] Latency tracked?
  - [ ] Error rate monitored?
  - [ ] Resource usage visible?

- [ ] **Alerts configured** (if critical path)
  - [ ] Alert if error rate > threshold?
  - [ ] Alert if latency degraded?

### Git & Version Control

- [ ] **Commit history clean**
  - [ ] No WIP or debug commits?
  - [ ] Commits follow `conventional-commits` format?
  - [ ] Each commit is atomic (one logical change)?

- [ ] **Branch ready for merge**
  - [ ] Up-to-date with main?
  - [ ] No merge conflicts?
  - [ ] All CI checks passing?

- [ ] **PR description complete**
  - [ ] What changed?
  - [ ] Why was this change needed?
  - [ ] Any breaking changes?
  - [ ] Linked GitHub issues?

### Sign-Off

- [ ] **Specialist sign-off**
  - [ ] Implementing agent (BackendEngineer, etc.) confirms complete
  - [ ] QualityEngineer approves tests
  - [ ] SecurityEngineer approves security checks
  - [ ] PlatformEngineer approves deployment readiness

- [ ] **ProductManager validates**
  - [ ] Acceptance criteria met?
  - [ ] Matches definition of done (DoD)?
  - [ ] Ready to release?

## Automation in CI/CD

### GitHub Actions Workflow: `deployment-readiness.yml`
```yaml
on:
  pull_request:
    branches: [main]

jobs:
  readiness:
    runs-on: ubuntu-latest
    steps:
      # Code Quality
      - name: Lint
        run: pnpm run lint && uv run flake8 src/

      - name: Type Check
        run: |
          cd src/control-center/frontend && tsc --noEmit
          cd ../../core && mypy src/

      # Testing
      - name: Unit Tests
        run: |
          pnpm run test
          uv run pytest

      # Security
      - name: Dependency Audit
        run: |
          npm audit --audit-level moderate
          safety check

      - name: SAST Scan
        run: |
          bandit -r src/control-center/core/
          eslint-plugin-security src/control-center/frontend/

      # Documentation
      - name: Check Changelog
        if: github.event.pull_request.head.ref != 'version-bump'
        run: grep -q "## \[Unreleased\]" CHANGELOG.md

      # Report
      - name: Comment on PR
        if: always()
        uses: actions/github-script@v6
        with:
          script: |
            const checks = [
              { name: 'Lint', passed: ${{ job.lint_status == 'success' }} },
              { name: 'Type Check', passed: ${{ job.typecheck_status == 'success' }} },
              { name: 'Unit Tests', passed: ${{ job.test_status == 'success' }} },
              { name: 'Security Audit', passed: ${{ job.security_status == 'success' }} }
            ];
            const body = checks
              .map(c => `- [${c.passed ? 'x' : ' '}] ${c.name}`)
              .join('\n');
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: `## Deployment Readiness\n\n${body}`
            });
```

## Quick Start

### For Implementing Agent (e.g., BackendEngineer)
```bash
# Before claiming done, run locally:
uv run pytest --cov=app --cov-report=term
uv run flake8 src/control-center/core/
cd src/control-center/core && mypy src/ --strict
uv run bandit -r src/

# Verify CHANGELOG updated
cat CHANGELOG.md | head -20

# Push & wait for CI
git push origin feature/my-feature
# Check GitHub Actions dashboard
```

### For PR Reviewer (e.g., QualityEngineer)
1. [x] All tests passing?
2. [x] Coverage > 80%?
3. [x] CHANGELOG updated?
4. [x] No security warnings?
5. [x] Acceptance criteria met?
6. [x] Merge!

## Definition of Done (DoD)

A task is **DONE** when:
1. [x] Code written and tested locally
2. [x] All automated tests passing (CI/CD green)
3. [x] Code reviewed and approved
4. [x] Documentation updated (README, CHANGELOG, API)
5. [x] Security review passed (if applicable)
6. [x] Performance acceptable (no regressions)
7. [x] Deployment-readiness checklist signed off
8. [x] PR merged to main branch

## Integration with conventional-commits

Each commit follows the convention; CHANGELOG is generated from commits:
- `feat: ...` -> Added to CHANGELOG under `### Added`
- `fix: ...` -> Added to CHANGELOG under `### Fixed`
- `chore: ...` -> Not added to CHANGELOG
- `docs: ...` -> Added to CHANGELOG under `### Changed`

See `conventional-commits` skill for full convention.

## Common Failures & Fixes

| Failure | Fix |
|---|---|
| **"Lint errors: 10"** | `pnpm run lint --fix` (auto-fixes) |
| **"Tests failed in CI but pass locally"** | Push again (timing issue) or use `uv run pytest -x` to debug |
| **"Type errors in CI but not locally"** | Different Python/Node versions? Check CI logs |
| **"Coverage dropped below 80%"** | Add tests for new code paths before merge |
| **"CHANGELOG not updated"** | Add entry under `[Unreleased]` section |
