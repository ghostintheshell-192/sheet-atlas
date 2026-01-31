# Payment System & Licensing

**Status**: backlog
**Release**: v1.0.0
**Priority**: must-have
**Depends on**: none (infrastructure, not feature)

## Summary

Implement payment processing and license management for commercial release. Users purchase a license, receive a key, and the app validates it.

## User Stories

- As a user, I want to purchase a license from the website
- As a user, I want to enter my license key and unlock the full version
- As a user, I want my license to persist across reinstalls
- As a developer, I want to know how many licenses are active

## Requirements

### Functional

- [ ] License Key System
  - [ ] Generate unique license keys
  - [ ] Validate keys (format, checksum, not revoked)
  - [ ] Store license locally (encrypted)
  - [ ] Grace period or trial mode?

- [ ] Payment Processing
  - [ ] Payment provider integration (Stripe? Gumroad? Paddle?)
  - [ ] Automatic key delivery after purchase
  - [ ] Receipt/invoice generation

- [ ] In-App Experience
  - [ ] "Enter License Key" dialog
  - [ ] License status in Settings/About
  - [ ] Clear messaging for trial/unlicensed state
  - [ ] "Buy Now" link to website

- [ ] License Tiers (from project overview)
  - [ ] Personal: $79
  - [ ] Professional: $199
  - [ ] Enterprise: $499
  - [ ] What differs between tiers? Features? Support? Seats?

### Non-Functional

- Offline validation (no phone-home for basic use)
- Secure storage (no plain-text keys)
- No aggressive DRM (trust users, don't punish them)

## Technical Notes

- Consider: cryptographic signature in license key (no server needed for validation)
- Consider: machine binding? (prevents sharing but adds friction)
- Payment providers that handle VAT/tax: Paddle, Gumroad, LemonSqueezy
- Self-hosted vs managed — managed is easier for solo dev

## Open Questions

- [ ] Which payment provider?
- [ ] Trial period? How long? What's limited?
- [ ] Machine binding or honor system?
- [ ] What differentiates license tiers?
- [ ] Subscription vs one-time purchase?
- [ ] Upgrade path between tiers?

## Acceptance Criteria

- [ ] Can purchase license from website
- [ ] Can enter key in app and unlock
- [ ] License persists across sessions
- [ ] Clear UX for unlicensed state
- [ ] Works offline after initial activation

---

*Spec created 2025-12-10 — needs detailed review before v1.0*
