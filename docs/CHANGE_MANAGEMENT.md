# ClinicQ change management

ClinicQ handles appointments and patient records for a multi-branch clinic, so a bad release is
felt at the front desk immediately. Every production change follows this process.

## Change classification

| Class | Examples | Approval | Window |
| --- | --- | --- | --- |
| **Standard** | Content or copy fix, fee schedule update through the UI, adding a doctor | Pre-approved; log it | Any time |
| **Normal** | New feature, schema change, dependency upgrade | Service owner + one reviewer | Scheduled release window |
| **Emergency** | Production outage, data corruption, security fix | Verbal approval from the service owner, RFC raised within 24 hours | Immediate |

## RFC template

Copy this into the change ticket.

```markdown
# RFC-<yyyy>-<nnn>: <short title>

**Requested by:**
**Date raised:**
**Change class:** Standard | Normal | Emergency
**Target release window:**
**Services affected:** ClinicQ web app / Azure SQL / Blob storage / Hangfire jobs

## Summary
One paragraph: what changes and why.

## Business justification
Who benefits and what happens if we do nothing.

## Scope
- In scope:
- Out of scope:

## Technical detail
- Code changes (PR links):
- Database changes (new scripts in `db/`, forward-only):
- Configuration or Key Vault secret changes:
- Infrastructure changes (`infra/main.bicep`):

## Risk assessment
| Risk | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- |
|  | L/M/H | L/M/H |  |

**Overall risk:** Low | Medium | High
**Downtime expected:** none (slot swap) | <n> minutes

## Test evidence
- [ ] `dotnet build` clean
- [ ] `dotnet test` green (state machine, validators, fee calculation, API integration)
- [ ] Newman collection green against the Dev deployment
- [ ] Manual check: book -> confirm -> check in -> consult -> complete -> invoice -> pay
- [ ] Dashboard renders with expected utilization, wait time and revenue

## Rollback plan
See the rollback section below; state explicitly whether the database change is
backward compatible with the previous application build.

## Communication
- Notify: branch managers, front desk leads, billing office
- Channel and timing:

## Approvals
| Role | Name | Date |
| --- | --- | --- |
| Requester |  |  |
| Reviewer |  |  |
| Service owner |  |  |
```

## Release checklist

**Before the window**

- [ ] RFC approved and linked to the build.
- [ ] Change frozen on `main`; the CI badge is green.
- [ ] Database scripts reviewed and confirmed idempotent and forward-only.
- [ ] Confirm the change is backward compatible with the currently deployed build (required for a
      slot swap, since both versions briefly share the database).
- [ ] Key Vault secrets in place for any new configuration key.
- [ ] Announce the window to the branches.

**During the release**

- [ ] Pipeline `Build` and `Test` stages green.
- [ ] `DeployDev` succeeded; Newman smoke passed against Dev.
- [ ] Apply database scripts to Prod (gated step) and confirm success.
- [ ] `DeployProd` publishes to the `staging` slot.
- [ ] Staging `/health` returns `Healthy`; sign in and spot-check the booking screen on the slot.
- [ ] Approve the swap; `AzureAppServiceManage@0` swaps staging into production.

**After the swap**

- [ ] Production `/health` healthy.
- [ ] Smoke test: sign in, open today's queue, book a test appointment, cancel it.
- [ ] Swagger UI loads and `/api/v1/auth/token` issues a token.
- [ ] `/hangfire` shows the three recurring jobs scheduled, with no failed jobs.
- [ ] Application Insights: no new exception spike for 30 minutes.
- [ ] Update the RFC with the outcome and close it.

## Rollback plan

| Scenario | Action | Expected time |
| --- | --- | --- |
| Application defect found after swap | Swap the slots back (`AzureAppServiceManage@0`, or the portal's Swap button). The previous build is still warm in `staging`. | < 2 minutes |
| Defect found before swap | Abandon the release; the production slot was never touched. | Immediate |
| Bad configuration or secret | Correct the Key Vault secret version or app setting and restart the site. | < 5 minutes |
| Database change is the problem | Apply the compensating script prepared with the RFC. Never edit an applied script; forward-only. | Depends on the change |
| Data corruption | Azure SQL point-in-time restore to a new database, verify, then repoint `ConnectionStrings--Default`. | 30-60 minutes |

Because a swap puts the previous build back against the *current* database, every schema change
must be additive and tolerate both builds for at least one release: add columns as nullable or
with defaults, deploy the code that writes them, and only remove the old shape a release later.

## Post-implementation review

Hold a short review for every Normal and Emergency change:

- Did the change deliver what the RFC described?
- Was the downtime and risk estimate accurate?
- Did monitoring surface the problem, or did a user report it first?
- For emergencies: what would have caught this earlier, and is that now a test?

Record actions in the RFC before closing it.
