# Step 12 — AI gateway and agent safety architecture

Status: **Design only. No provider API calls, models, or clinical agents are deployed.**

## Architecture
All AI features call a server-side AI Gateway. Desktop/mobile/web clients never receive provider credentials. The gateway manages provider configuration, approved model IDs, routing, usage/cost limits, timeouts, retries, redaction, prompt templates, safety policy, audit metadata, and output labeling.

## Initial AI capabilities (assistive only)
1. Report drafting assistant: proposes wording from clinician-entered structured findings; clinician verifies all content.
2. Translation/language helper: translate patient-facing instructions or report text; original text remains visible.
3. Voice transcription: separate opt-in, source audio access, transcript review, clear correction workflow.
4. Quality assistant: completeness checks, missing fields, conflicting entered values; no diagnosis or treatment decision.
5. Knowledge assistant: retrieve approved institutional material; citations/retrieved passage shown, not presented as medical authority.
6. Operations assistant: nonclinical help with app settings and workflow troubleshooting.

## Data protection and AI input controls
- Default: send only minimum necessary data; use synthetic or de-identified data during development.
- No patient-identifying data to external AI providers until contractual, legal, privacy, security, retention, and deployment reviews are completed and provider configuration explicitly approved.
- Per-feature data allowlist and consent/authorization gates; avoid sending full record/media by default.
- Provider keys are secret-managed server-side, never in app config, source code, logs, or browser/mobile builds.
- Retention, training-use settings, region, subprocessor, and logging behavior reviewed for each provider before enabling.
- Treat uploaded documents, images, transcripts, and retrieved content as untrusted data. Prompt injection inside these materials must never override safety, permissions, or tool policies.

## AI action boundaries
- No AI agent may autonomously sign a report, change patient demographics, delete records/media, grant access, manage licenses, or operate endoscope hardware.
- No autonomous diagnosis, triage, treatment advice, or patient-specific recommendations in the initial release.
- External tool calls are allowlisted, permission-checked by backend, and subject to user confirmation for consequential actions.
- AI output is marked as generated, with model/provider/version, template version, timestamp, input provenance, and clinician edits where appropriate.
- Clinician remains accountable for reviewing and accepting clinical text; AI output may be wrong or incomplete.

## Reliability and cost controls
- Hard request timeouts, bounded retries, circuit breaker, concurrency limits, per-hospital quotas, per-feature budgets, and provider outage fallback.
- Never silently substitute an unapproved model. Model changes require configuration review and regression evaluation.
- Output schema validation, length limits, refusal/unsafe-content handling, and explicit unavailable/error state.
- Separate audit of AI usage from prompt/content logging; default logs must not contain PHI or secrets.

## Evaluation gate before enabling any clinical assistance
- [ ] Approved intended-use statement and risk classification
- [ ] Privacy, security, legal, and provider contract review
- [ ] Dataset permissions and de-identification verified
- [ ] Representative evaluation set, clinician review, error analysis, bias assessment
- [ ] Hallucination, omission, and unsafe recommendation tests
- [ ] Prompt injection and data exfiltration tests
- [ ] Regression suite for model/provider/template changes
- [ ] UI clearly distinguishes AI suggestion from clinician-authored content
- [ ] Human review and rejection/edit workflow validated
- [ ] Monitoring, incident response, rollback, and feature kill-switch ready
