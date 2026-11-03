# FINANCE OS — Design / UX contract

References: original master §§5–7, 15–17, 23, 27. Goal: delightful and usable like a polished commercial Windows productivity product, **not** a generic admin dashboard.

## Brand direction
- Calm, precise, spacious, professional. Clear visual priority and restrained animations; build a coherent system instead of copying Linear/Things/Notion.
- Palette: neutral semantic surfaces plus one configurable accent; consistent contrast and semantic warning/error/success states. Both dark and light from day one.
- Typography: clean, readable, carefully spaced, with strong information hierarchy and no tiny task rows.
- Tokens: centralize spacing, radius, typography, shadows, focus ring, animation durations and color roles. Use reduced-motion preferences.

## Information architecture
- Left sidebar: Overview, My Day, Tasks, Calendar, Reminders, Workflows, Projects, Analytics, Archive, Settings.
- Header: search, quick add, date and notification center. Optional right context pane (upcoming, task details, focus timer).
- Overview should answer Today / Next / Urgent / Overdue / Waiting at a glance without overflowing with charts.
- Simple Mode hides advanced controls but not data. Preserve tasks, custom statuses and workflows when toggled.

## Required UX flows
- Add a task from anywhere in minimal interactions; allow due date, reminder and priority without long forms.
- Mark task complete, edit, snooze, restore, filter and find without surprising jumps.
- Waiting-for-Response has first-class prominence and clear next follow-up date.
- Keyboard-first quick capture, visible focus, accessible dialogs, high DPI and restrained transitions.
- Persist selected theme, modes, widgets, navigation choices, layout and localized EN/RO strings.
- Meaningful empty, loading, offline and error states. Clearly label unimplemented integrations as unavailable, not clickable fake actions.

## Visual implementation order
1. Typography, colors, layout grid, input/button primitives, navigation and dialogs.
2. App shell and working My Day / task capture workflow with real persistent data.
3. Dashboard widgets and beautiful empty states.
4. Calendar/workflow/analytics views built on shared primitives.
5. Interaction polish, reduced motion, contrast, zoom/high-DPI testing.

## UI acceptance review (each milestone)
- Visual hierarchy and dense corporate workloads are still legible.
- Simple mode can be understood quickly without tutorials.
- No cramped text, unresponsive buttons, inaccessible modal traps or excessive chrome.
- Layout works on standard enterprise laptop screens, Windows scaling and keyboard navigation.
- Screenshot review and real user interaction checks when tooling supports them.
