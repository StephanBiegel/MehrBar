---
name: grill-me
description: Interview the user relentlessly about a plan or design until reaching shared understanding, resolving each branch of the decision tree. Use when user wants to stress-test a plan, get grilled on their design, or mentions "grill me".
---

Interview me relentlessly about every aspect of this plan until we reach a shared understanding. Walk down each branch of the design tree, resolving dependencies between decisions one-by-one. For each question, provide your recommended answer.

Ask the questions one at a time.

If a question can be answered by exploring the codebase, explore the codebase instead.

## Persistence — write answers back to the source file

The **source file** is the plan or design document being grilled (the file the user pointed at, or the most recently discussed plan/spec/design doc — when ambiguous, ask once which file to use).

**Always persist to a source file — create one if none exists.** The results of a grill must never live only in the chat. If there is no design/plan document for the topic being grilled, do not just ask and wait: create one before the first question. Pick a sensible name and location matching the project's conventions (e.g. a design-docs / planning directory if the project has one, otherwise the repo root), capture the topic and the user's initial idea at the top, and tell the user one line: which file you created. Only ask the user which file to use when there are *several plausible existing* documents — not when there is simply none.

Before the first question, append a `## Grill Session — {YYYY-MM-DD}` heading to the end of the source file. Then, after each answer, append the Q&A to that section immediately (don't batch — one write per resolved question, so progress survives interruption):

```markdown
### Q{n}: {question topic — short}
**Question:** {full question text}
**Recommended:** {your recommendation}
**Answer:** {user's answer, verbatim where possible}
**Follow-ups:** {any branches this opened, if relevant}
```

If the user pushes back, refines, or changes their mind in a follow-up turn, update the existing Q&A entry rather than appending a new one — the file should reflect the final resolved answer, not the back-and-forth.

## Resuming an interrupted grill

Before asking the first question, check the source file for an existing `## Grill Session` section:

- **If found and incomplete** (open branches, unresolved follow-ups, or the user says "continue"/"resume"): summarize in one line what was already decided, identify the next unresolved branch, and pick up from there. Do not re-ask resolved questions.
- **If found and the user wants to start fresh**: append a new dated `## Grill Session — {date}` section below the old one rather than overwriting.
- **If not found**: start a new session.

When resuming, your first message should be the next question — not a recap of the whole prior session.
