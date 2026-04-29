# CADO Framework PR Checklist

## Risk Tier

- [ ] Low
- [ ] Medium
- [ ] High

## Approvals

- [ ] Approval not required
- [ ] `cado-approve` label present before Build
- [ ] Extra reviewer or owner approval captured if local policy requires it

## Evidence

- [ ] Tests run or CI result recorded
- [ ] Lint or format result recorded
- [ ] Build or package result recorded if relevant
- [ ] Security checks recorded if relevant
- [ ] Migration and rollback notes recorded if relevant
- [ ] Docs updates recorded if relevant
- [ ] Evidence section is complete

## Rollback Notes

- [ ] Rollback not required
- [ ] Rollback steps documented
- [ ] Operator impact documented

## Evidence Section

```md
## Evidence

- Category: <Tests | Lint | Build | Security | Migration | Docs>
  Tool: <tool>
  Command: <command or job>
  Result: pass | fail | partial
  Link: <optional>
  Notes: <short outcome>
```

