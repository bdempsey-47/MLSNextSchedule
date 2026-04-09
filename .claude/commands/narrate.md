---
description: Generate a narrative of this repo's git history
---

Run gitnarrative against the current repository to produce a structured narrative
of its evolution. Use the output to understand what was built, in what order,
and what key decisions were made.

Steps:
1. Run: `gitnarrative narrate --repo . --since 2024-01-01`
2. Read the output at `.gitnarrative/narrative.md`
3. Use the narrative to inform your understanding of the codebase architecture
   and design decisions