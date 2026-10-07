# platform-conventions design

## How it works
`platform-conventions` is an ordinary capability in `openspec/specs/`, the same tree every other
capability lives in, holding only rules that apply to more than one capability. A capability
following one of them documents its own concrete instance or exception in its own spec and
references this one for the general rule, rather than restating it.

## Decisions
- **One home per shared rule.** A rule restated in two capabilities drifts the next time either
  changes, with nobody noticing the other copy. Only the general rule lives here. Concrete instances,
  such as service data's resource catalogue or the API catalogue's fixed document path, stay in
  their own specs.
- **Named `platform-conventions`, not `cross-cutting`.** The name says what is inside without
  needing architecture jargon to parse.
- **A capability, not a separate decision-record folder.** Readers already open `openspec/specs/`
  before changing code, so a rule here is found without anyone needing a reason to look elsewhere.
- **Narrative stays in `docs/Purpose.md`.** Rejected alternatives, live-probe findings and open
  questions are the why; only rules shared across features belong here.

## Approaches that don't work
- **A top-level ADR folder (for example the community `spec-driven-with-adr` schema).** It solves a
  related problem well, but a reader working on one capability has no automatic reason to open a
  separate `adr/` folder, so its decisions go unread where they matter.

## Constraints and limitations
- Nothing checks automatically that a change to a capability's concrete instance also considers the
  general rule. That relies on reading `openspec/specs/` first.
- This capability must not become a catch-all. A rule specific to one feature stays in that
  feature's spec, even if it sounds general.
