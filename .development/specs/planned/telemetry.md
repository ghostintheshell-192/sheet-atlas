# Telemetry

**Status**: planned
**Release**: TBD
**Priority**: nice-to-have
**Depends on**: settings-configuration.md

## Summary

Optional, opt-in telemetry focused on crash/error reporting and feature usage analytics, using Sentry.

## User Stories

- As a developer, I want to know when the app crashes and why (stack traces, context)
- As a developer, I want to know which features are most used to prioritize development
- As a user, I want full control over whether my usage data is collected
- As a user, I want transparency about what data is collected

## Privacy Note

SheetAtlas is marketed as "100% local processing". Telemetry must be clearly communicated as:
- Completely opt-in (disabled by default)
- Never sends file contents, file names, or user data
- Only anonymous crash reports and usage statistics
- File processing remains 100% local regardless of telemetry setting

## Requirements

### Functional

#### Opt-in Flow
- [ ] Disabled by default
- [ ] **Delayed prompt**: after N sessions (e.g., 5), show friendly prompt "Help us improve SheetAtlas?"
  - [ ] Clear explanation of what's collected
  - [ ] "Yes, I want to help" / "No thanks" / "Remind me later"
  - [ ] Don't show again if user chooses Yes or No
- [ ] **Always accessible**: toggle in Settings, regardless of prompt response
- [ ] "What we collect" link to documentation/privacy policy

#### Data Collection — Crash/Error Reporting (Priority)
- [ ] Unhandled exceptions with stack traces
- [ ] App version, OS version, runtime info
- [ ] Error context (which operation failed, not what data)
- [ ] **Excluded**: file contents, file names, file paths, user identifiers

#### Data Collection — Feature Usage (Secondary)
- [ ] Feature usage counts via Sentry breadcrumbs:
  - [ ] Search performed
  - [ ] Comparison performed
  - [ ] Export performed
  - [ ] Template created/applied
- [ ] File format distribution (xlsx/xls/csv counts, not names)
- [ ] Session start/end (for session duration)

#### Technical
- [ ] Sentry .NET SDK integration
- [ ] Graceful failure if network unavailable (no retry queue needed)
- [ ] Respect opt-in flag before any Sentry initialization

### Non-Functional
- Privacy: GDPR-compliant, no PII, anonymous by design
- Transparency: clear documentation of what's collected
- Performance: zero impact on app responsiveness (Sentry is async)

## Technical Notes

- **Service**: Sentry (free tier: 5K errors/month, sufficient for early stage)
- Sentry SDK: `Sentry.Sentry` NuGet package
- Initialize Sentry only if opt-in flag is true
- Use breadcrumbs for feature tracking, events for errors
- Store opt-in preference in settings JSON file

## Future Consideration

Feature feedback (qualitative) is better served by a **contact form on website** rather than telemetry. Out of scope for this spec.

## Acceptance Criteria

- [ ] Sentry integration works when opted in
- [ ] No Sentry initialization if not opted in (verify no network calls)
- [ ] Prompt appears after N sessions (configurable)
- [ ] Settings toggle enables/disables telemetry
- [ ] Crash reports appear in Sentry dashboard with useful context
- [ ] No PII in any Sentry event (verify with test data)
