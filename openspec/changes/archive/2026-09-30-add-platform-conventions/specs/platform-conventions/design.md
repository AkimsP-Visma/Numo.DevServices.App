## Context

Five capability specs were backfilled from existing code with no shared home for rules that apply
to more than one of them. Two specs ended up independently restating the same platform-wide
decision (no arbitrary pass-through) in their own words - free to drift apart the next time either
capability changes, without anyone noticing the other copy exists.

## Goals / Non-Goals

**Goals:**
- One place for a rule that applies to more than one capability, discoverable the same way any
  other capability is (it lives in `openspec/specs/`, not a separate top-level folder).
- Remove existing duplication (the pass-through rule) rather than leaving two copies.

**Non-Goals:**
- Full ADR tooling (immutable numbered decision records, supersession links). Considered and
  rejected for this repo's current scale: a community-built `spec-driven-with-adr` OpenSpec schema
  exists and solves a related problem well, but it writes decisions to a top-level `adr/` folder a
  session has no automatic reason to open while reading a capability's own spec - the opposite of
  the locality this repo already relies on (`CLAUDE.md` tells every session to check
  `openspec/specs/` first). A capability living in that same tree costs nothing extra to discover.
- Migrating every prose bullet from `docs/Purpose.md`. Only the two that are genuinely rule-like
  and duplicated or reusable across features move here; narrative "why" (rejected alternatives,
  live-probe findings, open questions) stays in `docs/Purpose.md`, which remains the first read for
  that kind of context.

## Decisions

- **Named `platform-conventions`, not `cross-cutting`.** "Cross-cutting" is architecture jargon a
  session has to already know to parse; "platform-conventions" says what's inside without it.
- **Lives in `openspec/specs/`, is a normal capability.** Nothing in OpenSpec reserves capability
  names or treats one specially - a change that touches a platform-wide rule lists
  `platform-conventions` as a Modified Capability exactly like any other.
- **Deduplicate by MODIFYING the two existing specs, not by only adding the new one.** Leaving the
  old restatements in place would recreate the exact problem this change exists to fix - two
  descriptions of the same rule, only now three.
- **Each capability keeps its own concrete instance or exception; only the general rule moves up.**
  `service-data-browsing`'s resource catalogue mechanism, its own error id list, and
  `api-catalog-browser`'s CORS disclosure behavior are genuinely capability-specific and stay; only
  the restated general principle is replaced with a reference to this capability.
- **`design.md` lives per-capability, co-located with `spec.md`** (`openspec/schemas/spec-driven`
  was project-forked to change the `design` artifact's output path to `specs/**/design.md`), not
  archived away at the change root - discovered, while investigating this exact problem, that the
  default schema's `design.md` is never promoted out of `openspec/changes/archive/` on archive; a
  living per-capability file sidesteps that gap by never depending on archive-time promotion for
  discovery, since it is read directly from `openspec/specs/<capability>/`.

## Risks / Trade-offs

- [Risk] A future change updates a capability's concrete instance of a platform rule without
  checking whether the platform rule itself also needs to change. Mitigation: none automatic;
  relies on the same discipline `CLAUDE.md` already asks for ("check `openspec/specs/` first").
- [Risk] `platform-conventions` could itself grow into the one-big-file problem this effort started
  from, if too much gets folded into it. Mitigation: only a rule genuinely shared across two or
  more capabilities belongs here; a rule specific to one feature stays in that feature's own spec
  even if it sounds general.
