# MASTER PROMPT — FINANCE OS
## Build a Premium Enterprise Productivity Desktop Application

### 1. ROLE & MISSION

You are an elite multidisciplinary software engineering team consisting of:

- Principal Software Architect
- Senior Windows Desktop Engineer
- Senior Frontend Engineer
- Senior UI/UX Product Designer
- Database & Security Engineer
- Enterprise Workflow Automation Specialist
- Quality Assurance & Performance Engineer
- Product Manager specializing in financial corporate operations

Your mission is to **design, architect, implement, test, optimize, and package a production-quality Windows desktop productivity application called FINANCE OS**.

This application should help employees working in large corporations such as Bosch, particularly in Finance, Accounting, Controlling, Accounts Payable, Accounts Receivable, and Source-to-Pay departments.

The application must feel like a premium commercial software product, not an internal company tool or a coding experiment.

**Your objective is to build the actual working product, not simply describe how it could be built.**

---

### 2. CORE PRODUCT VISION

Create an exceptionally elegant, intelligent, lightweight desktop application that helps corporate employees organize their entire professional workday.

The application should combine:

- Personal task management
- Advanced reminders and alarms
- Daily and weekly planning
- Deadline and priority management
- Corporate financial workflow tracking
- Recurring activities
- Productivity analytics
- Intelligent organization
- Personal workflow customization
- Optional enterprise integrations
- Automated notifications and follow-ups
- Project and workload management
- Time tracking
- Search and filtering
- Automation rules

The essential philosophy is:

**Simple enough for someone who only needs a checklist. Powerful enough for someone managing hundreds of tasks, invoices, requests, deadlines, dependencies, and recurring responsibilities.**

Never sacrifice simplicity for functionality.

Advanced features should remain discoverable but unobtrusive.

---

### 3. NON-NEGOTIABLE REQUIREMENTS

The application must:

1. Run on Windows 10/11 corporate laptops.
2. Be launchable by double-clicking an application icon.
3. Have a distributable Windows executable.
4. Offer a portable, per-user deployment option where technically possible.
5. Avoid requiring administrator privileges for normal operation.
6. Work without Python, Node.js, or developer tools installed on the user's computer.
7. Work completely offline for its core functionality.
8. Store user data persistently between launches.
9. Launch quickly and remain responsive.
10. Consume minimal CPU and RAM.
11. Support Windows notifications and scheduled alarms.
12. Maintain reminders after closing and reopening the application.
13. Restore pending reminders after laptop sleep, restart, or application relaunch.
14. Avoid cloud dependencies for basic functionality.
15. Respect corporate IT security and application restrictions.
16. Never require unauthorized access to Microsoft 365, SAP, Outlook, Teams, or internal corporate systems.
17. Support easy upgrades without deleting existing user data.
18. Have reliable error handling and recovery.
19. Never silently lose tasks, notes, or reminders.
20. Offer both Simple Mode and Advanced Mode.

The final deliverable must be a genuinely usable desktop application.

---

### 4. TECHNOLOGY & ARCHITECTURE

Research and select the most appropriate architecture for this specific use case.

Evaluate at least:

- Tauri 2 + React + TypeScript
- Native Windows .NET desktop approaches, such as WPF
- Other alternatives only if they offer meaningful advantages

Consider:

- Resource consumption
- Windows compatibility
- Deployment without administrator permissions
- Corporate security restrictions
- UI quality and animation capabilities
- Notification reliability
- Long-term maintainability
- Packaging complexity
- Offline capabilities
- Self-contained deployment
- Ease of distributing updates

A preferred candidate is:

**Tauri + React + TypeScript + SQLite + Tailwind CSS + shadcn/ui + Lucide Icons.**

However, do not blindly use this stack.

Choose the architecture that best satisfies the product requirements, and explain your decision.

For Tauri, explicitly consider WebView2 availability and Windows notification limitations.

Avoid unnecessary dependencies and excessive abstraction.

Use modular architecture with separate components for:

- Desktop shell
- UI presentation
- Application state
- Domain/business logic
- Persistence
- Notification scheduling
- Background lifecycle
- Automation engine
- Optional integrations
- Import/export
- Diagnostics

The codebase must be maintainable, testable, and extensible.

---

### 5. PREMIUM UI/UX DESIGN

This is one of the highest-priority requirements.

The application must be visually outstanding.

The design should look like a premium 2026 productivity application created by a world-class product design team.

Design inspiration may include the craftsmanship of:

- Linear
- Raycast
- Notion
- Things 3
- Todoist
- Arc
- Modern macOS utilities
- Premium enterprise SaaS dashboards

Do not clone these products.

Create a unique, consistent design language.

#### Visual requirements

- Sophisticated typography
- Beautiful spacing and alignment
- Carefully selected color palette
- Light and dark themes
- Elegant cards
- Minimalistic sidebar
- Beautiful dashboard widgets
- Modern charts
- Subtle shadows
- Tasteful glass effects where appropriate
- Smooth, restrained animations
- Fluid transitions
- Consistent iconography
- Rich hover feedback
- Excellent empty states
- Premium notification design
- Professional microinteractions
- Beautiful modal dialogs
- Elegant context menus
- Excellent form controls
- Highly readable information hierarchy
- Clear keyboard focus states
- High-DPI and display scaling support

Avoid:

- Generic admin-template aesthetics
- Cluttered layouts
- Excessive gradients
- Too many colors
- Unnecessary animation
- Tiny text
- Deeply nested menus
- Overwhelming dashboards

Use animation only when it improves clarity.

Support reduced-motion preferences.

#### Main layout

Create an elegant desktop shell with:

Left navigation sidebar:
- Overview
- My Day
- Tasks
- Calendar
- Reminders
- Workflows
- Projects
- Analytics
- Archive
- Settings

Top area:
- Global search
- Quick task creation
- Current date
- Notification center
- Focus status
- User preferences

Main area:
- Modular dashboard with customizable widgets

Optional right panel:
- Upcoming deadlines
- Active timer
- Quick notes
- Task details
- Relevant context

All sections must have a coherent design.

---

### 6. TWO DISTINCT EXPERIENCE MODES

#### SIMPLE MODE

Designed for employees who want an extremely straightforward experience.

Display only essential functionality:

- Today's tasks
- Add task
- Complete task
- Reminders
- Upcoming deadlines
- Simple calendar
- Quick notes

A new user should understand the application within 60 seconds.

Minimal setup.

Minimal visual complexity.

Simple Mode is not a separate application. It is a simplified presentation of the same underlying data and core features.

#### ADVANCED MODE

For power users managing complex corporate workloads.

Enable:

- Custom task fields
- Advanced filters
- Workflow stages
- Recurring task rules
- Task dependencies
- Detailed notifications
- Multi-project management
- Time tracking
- Productivity analytics
- Workload forecasting
- Automation rules
- Advanced views
- Custom dashboards
- Custom categories
- SLA tracking
- Bulk operations
- Templates

Users must be able to switch between Simple and Advanced Mode without losing information or changing the underlying task data.

---

### 7. INTELLIGENT DASHBOARD

Design a genuinely useful daily command center.

The main screen should immediately answer:

- What do I need to do today?
- What is urgent?
- What is overdue?
- What am I waiting for?
- What is due tomorrow?
- What can I postpone?
- How much work do I have?
- What should I focus on next?

Suggested components:

**Daily Overview**
- Personalized greeting
- Current date
- Today's priorities
- Upcoming commitments

**Task Summary**
- Pending tasks
- Completed tasks
- Overdue tasks
- Tasks awaiting external input

**Today's Timeline**
- Chronological schedule
- Meetings
- Reminders
- Task time blocks

**Priority Focus**
- Top 3 important tasks
- Critical deadlines
- Suggested next task

**Workload Overview**
- Daily and weekly workload
- Task distribution
- Estimated effort

**Quick Actions**
- Add task
- Add reminder
- Start focus session
- Open recurring workflow
- Search existing task

Allow users to customize which widgets appear, their order, and their visibility.

Dashboard customization must be saved persistently.

---

### 8. ADVANCED TASK MANAGEMENT

Each task should support:

- Title
- Description
- Category
- Project
- Priority
- Status
- Due date
- Due time
- Start date
- Estimated duration
- Actual duration
- Reminder configuration
- Tags
- Checklists
- Subtasks
- Notes
- External reference number
- Related task links
- Dependencies
- Recurrence
- Completion history
- Optional responsible person
- Optional requester
- Optional external system link

Default statuses:

- Not Started
- In Progress
- Waiting for Response
- Blocked
- Completed
- Cancelled

Users may configure their own statuses in Advanced Mode.

Provide different views:

- List
- Kanban board
- Calendar
- Timeline
- Today
- Upcoming
- Overdue
- Waiting for Response
- Completed

Support:

- Drag and drop
- Bulk editing
- Duplicate task
- Task templates
- Keyboard shortcuts
- Undo actions
- Fast inline editing
- Saved filters
- Multi-criteria sorting
- Archive and restore
- Soft deletion with recovery

Task creation must be extremely fast.

Example:

A user should be able to create a task such as:

"Check pending intercompany invoices tomorrow at 09:30"

and configure its reminder quickly.

If natural-language date parsing is implemented, it must work locally without requiring an AI API.

---

### 9. REMINDERS, ALARMS & NOTIFICATION ENGINE

This is a mission-critical module.

Build a robust reminder and notification engine.

Support:

- One-time reminders
- Daily reminders
- Weekly reminders
- Monthly reminders
- Custom intervals
- Reminders before deadlines
- Multiple reminders per task
- Follow-up reminders
- Overdue alerts
- Optional sound alerts
- Silent notifications
- Windows toast notifications
- In-app notification center
- Snooze options
- Dismiss actions
- Direct navigation to relevant task

Include configurable snooze options:

- 5 minutes
- 10 minutes
- 15 minutes
- 30 minutes
- 1 hour
- Tomorrow
- Custom date/time

A notification should allow fast, actionable interaction where supported by Windows.

Avoid intrusive behavior by default.

Respect:
- Do Not Disturb
- Focus mode
- Working hours
- Weekend preferences
- Individual reminder settings

#### Reliability requirements

Notifications should remain correctly scheduled after:

- Application restart
- System restart
- Laptop sleep
- Temporary system suspension
- Application crash

Define and implement clear recovery semantics.

Missed notifications must never disappear silently.

Distinguish between:

1. Notifications delivered while the application is running.
2. Notifications managed while the application is minimized to the system tray.
3. Notifications requiring an approved background or startup mechanism.

Explain Windows platform limitations rather than promising impossible background behavior.

If background reminders require the application to remain active, provide a lightweight tray mode.

Optional startup with Windows must require the user's consent and respect corporate IT policies.

---

### 10. CORPORATE FINANCE WORKFLOWS

The application should be optimized for financial operations but remain useful for other corporate departments.

Provide configurable workflow templates.

Examples:

#### Intercompany Invoicing
- Receive request
- Validate information
- Check required details
- Process invoice
- Verify posting
- Send confirmation
- Close task

#### Accounts Payable
- Receive invoice
- Check documentation
- Verify approval
- Resolve discrepancies
- Process invoice
- Confirm completion

#### Month-End Closing
- Prepare closing checklist
- Reconcile accounts
- Check outstanding documents
- Resolve exceptions
- Complete controls
- Submit reporting
- Confirm closure

#### Request Follow-Up
- Request received
- Additional information needed
- Waiting for requester
- Follow-up scheduled
- Response received
- Task completed

#### Recurring Financial Controls
- Daily controls
- Weekly checks
- Monthly reconciliation
- Quarterly reporting
- Year-end procedures

Each workflow should be customizable.

Users should be able to create workflow templates without coding.

Allow checklists, deadlines, statuses, and reminders to be attached to workflow stages.

Do not hardcode Bosch-specific internal procedures.

---

### 11. WAITING FOR RESPONSE & FOLLOW-UP SYSTEM

Many corporate tasks depend on other people.

Implement a dedicated follow-up management system.

For example:

An employee requests missing information from a colleague.

The user can mark the task as:

**Waiting for Response**

The application should offer:

- Expected response date
- Follow-up date
- Optional contact name
- Reminder to follow up
- Waiting duration
- Follow-up history
- Escalation suggestion
- Return to active status when resolved

Display all waiting tasks in a dedicated view.

Support a configurable automatic reminder rule such as:

"If a task remains Waiting for Response for 2 business days, remind me."

Automatic communication with colleagues must not happen unless explicitly authorized and configured.

---

### 12. CALENDAR & TIME MANAGEMENT

Implement:

- Daily view
- Weekly view
- Monthly view
- Agenda view
- Task scheduling
- Drag-and-drop rescheduling
- Time blocking
- Working hours configuration
- Holidays and nonworking days
- Calendar-based reminders
- Deadline visualization
- Recurring events

Include an intelligent planner that can suggest a workable schedule based on:

- Deadlines
- Task priorities
- Estimated effort
- Available working hours
- Existing planned activities

Scheduling recommendations must be clearly identified as suggestions.

Never silently change deadlines.

---

### 13. AUTOMATION RULE ENGINE

Create a lightweight, configurable automation engine.

Users should be able to create rules in the format:

WHEN [trigger]
IF [conditions]
THEN [action]

Examples:

- When a task becomes overdue → increase its urgency.
- When a task is completed → update its workflow progress.
- When a recurring task is completed → schedule the next occurrence.
- When a task remains blocked for 3 days → generate a reminder.
- When a deadline is tomorrow → show a warning.
- When a task enters Waiting for Response → suggest a follow-up date.

Advanced users should be able to configure these automations through a visual rule builder.

No coding should be required.

Implement safeguards against infinite automation loops, duplicate executions, and conflicting rules.

Every automatic action should be traceable and reversible where appropriate.

---

### 14. PRODUCTIVITY ANALYTICS

Provide useful analytics without turning the product into an employee surveillance system.

Potential metrics:

- Tasks completed per day
- Tasks completed per week
- Average completion time
- Overdue task percentage
- Estimated versus actual effort
- Workload by category
- Recurring workload
- Time spent on different projects
- Most frequent blocking reasons
- Tasks awaiting response
- Deadline adherence

Use beautiful, restrained visualizations.

Support charts, trends, and filters.

Analytics should be private by default.

Avoid misleading productivity scores that equate task count with employee performance.

Do not introduce employee monitoring, screenshot capture, keyboard tracking, or intrusive tracking.

---

### 15. PERSONALIZATION

Allow users to customize:

- Light/dark mode
- Accent color
- Dashboard layout
- Visible navigation items
- Categories
- Task statuses
- Priority labels
- Work hours
- Notification sounds
- Date format
- Language
- Default task settings
- Startup behavior
- Simple/Advanced Mode
- Keyboard shortcuts
- Default calendar view

Provide English and Romanian localization.

Structure the application so other languages can be added easily.

---

### 16. QUICK CAPTURE & GLOBAL SEARCH

Implement an exceptionally fast quick-add experience.

The user should be able to create a task with very few interactions.

Include:

- Keyboard shortcut to open Quick Add
- Task suggestions
- Recent categories
- Recent projects
- Date/time shortcuts
- Quick priority selection
- Optional natural-language parsing

Search should cover:

- Task titles
- Task descriptions
- Notes
- Tags
- Categories
- Projects
- References
- Archived tasks

Include filters and advanced search operators where practical.

Search must remain fast with thousands of tasks.

---

### 17. OPTIONAL MICRO-TOOLS

Include or plan a small collection of genuinely helpful utilities:

- Quick Notes
- Personal Checklist
- Focus Timer
- Pomodoro Timer
- Mini Calendar
- Workday Countdown
- Deadline Countdown
- Quick Calculator
- Pinned Tasks
- Daily Planning Assistant
- Reusable Workflow Templates

These utilities must not make the interface cluttered.

Users should choose which are visible.

---

### 18. OPTIONAL INTEGRATIONS

Design a clean integration architecture.

Possible future integrations:

- Microsoft Outlook
- Microsoft Teams
- Microsoft To Do
- Microsoft 365 Calendar
- Excel
- SharePoint
- OneDrive
- SAP workflow links
- CSV import/export

Critical restrictions:

- Integrations must be optional.
- The application must work without integrations.
- Use only approved APIs, such as Microsoft Graph where authorized.
- Respect enterprise identity and permission requirements.
- Never bypass company authentication.
- Never scrape corporate credentials.
- Never read internal mailboxes without explicit authorized access.
- Never assume access to SAP or Bosch internal systems.
- Do not implement unauthorized background collection of company data.

If authentication or permissions are unavailable, provide properly isolated placeholder integration adapters and document how approved integration can be added later.

A standalone local application remains the primary product.

---

### 19. PRIVACY & ENTERPRISE SECURITY

Because this application may be used in corporate financial environments, security is essential.

Requirements:

- Local-first architecture
- No required cloud account
- No advertising
- No third-party analytics by default
- No silent telemetry
- No unnecessary network communication
- Secure local storage
- Safe file handling
- Input validation
- Protection against common injection vulnerabilities
- Strong separation between UI and privileged system operations
- Safe handling of exported files
- Protected authentication secrets
- Application logging without exposing sensitive information
- Configurable data retention
- Secure update strategy
- Data recovery options

Use appropriate Windows-supported secret protection, such as DPAPI or Windows Credential Manager, where applicable.

Consider encryption at rest for potentially sensitive local data, with an explicit key-management and recovery strategy.

Prevent accidental exposure of invoice content, financial details, and other confidential corporate data.

Provide a privacy-friendly demo dataset.

Any real deployment must remain subject to corporate IT approval.

Never attempt to evade application control, endpoint protection, installation restrictions, or company security policies.

---

### 20. DATABASE & DATA INTEGRITY

Use a reliable local database, preferably SQLite where suitable.

Provide:

- Database schema migrations
- Transactional writes
- Data validation
- Backup functionality
- Restore functionality
- Import/export
- Corruption detection where practical
- Recovery procedures
- Version compatibility
- Reliable recurrence storage
- Proper timezone handling
- Safe task deletion
- Referential integrity

Store application data in an appropriate per-user Windows application data directory rather than alongside the executable.

The executable should be replaceable without destroying user information.

Support exporting user data to a portable format such as JSON or CSV.

---

### 21. PERFORMANCE TARGETS

Optimize for ordinary corporate laptops.

Desired performance targets on a representative Windows office machine:

- Cold launch target: under 3 seconds
- Responsive user interactions
- Smooth scrolling
- No noticeable input lag
- Low idle CPU usage
- Idle RAM target below approximately 200 MB when feasible
- Efficient rendering with thousands of tasks
- Efficient database queries
- Lazy loading where appropriate
- Minimal unnecessary background polling

These are engineering targets, not excuses to compromise reliability.

Measure performance using reproducible benchmarks and report actual results.

The application must not require a powerful gaming computer.

---

### 22. WINDOWS DESKTOP EXPERIENCE

The final product should feel native to Windows.

Include:

- Application icon
- Proper application window
- Minimize/maximize
- Optional minimize to tray
- System tray menu
- Windows notification integration
- Remembered window size
- Remembered window position
- Startup preference
- Keyboard shortcuts
- Reliable task switching
- Windows display scaling support
- Installer when appropriate
- Portable executable option when feasible

Double-clicking the packaged application must launch the actual working product.

The user must not need to execute terminal commands after installation.

The application must not expose development consoles.

No browser tab should be required for normal use.

If Windows security policies block unsigned or unapproved executables, document the legitimate IT approval/signing requirements.

---

### 23. ONBOARDING EXPERIENCE

Create a beautiful first-run onboarding flow.

Keep onboarding short.

Suggested steps:

1. Welcome screen
2. Choose Simple or Advanced Mode
3. Choose light or dark theme
4. Configure working hours
5. Set notification preferences
6. Open dashboard

Allow the user to skip optional setup.

Provide sample tasks and workflow templates without mixing demo information into real data.

The first launch should feel polished and welcoming.

---

### 24. ERROR HANDLING & QUALITY

Implement:

- Comprehensive validation
- Meaningful error messages
- Graceful degradation
- Crash-safe persistence
- Database failure handling
- Notification failure handling
- Import validation
- Broken-reference handling
- Safe migration procedures
- Logging
- Diagnostic export
- Automated tests

Prioritize real-world reliability over novelty.

Never show a button that pretends to work but does nothing.

Every interactive component in the delivered application must either be functional or clearly marked as unavailable.

---

### 25. TESTING REQUIREMENTS

Build meaningful automated tests covering:

- Task creation
- Task editing
- Task deletion and restoration
- Persistence across restarts
- Recurring tasks
- Multiple reminders
- Snooze behavior
- Reminder recovery after downtime
- Date and timezone correctness
- Workflow transitions
- Automation rules
- Import/export
- Database migrations
- Settings persistence
- Simple/Advanced Mode switching
- Search and filtering
- User data isolation
- Critical UI flows

Include integration tests where reasonable.

Test notification edge cases, including system sleep and application restart, on an actual Windows environment.

Do not claim Windows-specific tests passed unless they were actually executed.

Fix implementation issues before considering the product complete.

---

### 26. DEVELOPMENT EXECUTION STRATEGY

You must work systematically.

#### PHASE 1 — Product and Architecture

Define:

- Final technology stack
- Application architecture
- Data model
- Module boundaries
- UI component architecture
- Notification lifecycle
- Windows packaging strategy
- Security approach

Identify technical risks early.

Do not spend the entire project writing documentation.

#### PHASE 2 — Design System

Create a cohesive premium design system.

Implement:

- Colors
- Typography
- Spacing
- Buttons
- Inputs
- Cards
- Sidebar
- Dialogs
- Navigation
- Dark/light themes
- Responsive desktop layouts
- Motion system

Establish the visual quality before expanding the application.

#### PHASE 3 — Working Core

Implement:

- Application startup
- Persistent database
- Dashboard
- Task creation/editing
- Task lists
- Calendar
- Reminders
- Notifications
- Simple/Advanced Mode

At the end of this phase, the application must already be usable.

#### PHASE 4 — Advanced Capabilities

Implement:

- Workflow templates
- Recurring tasks
- Advanced filters
- Waiting for Response
- Automation engine
- Analytics
- Time tracking
- Dashboard customization
- Quick capture

#### PHASE 5 — Optimization & Hardening

Improve:

- Performance
- Memory usage
- Startup speed
- Stability
- Error handling
- Accessibility
- Keyboard navigation
- Security
- Edge-case testing
- UI polish

#### PHASE 6 — Windows Packaging

Produce:

- Working Windows executable
- Appropriate installer or portable build
- Application icon
- Build scripts
- README
- User guide
- Technical documentation
- Test report

Verify that the packaged application works without developer tooling.

If the current execution environment cannot create or test a Windows executable, provide reproducible Windows build scripts and clearly distinguish unverified artifacts from tested deliverables.

---

### 27. USER EXPERIENCE SCENARIOS

Design and test the application against realistic situations.

**Scenario A — Simple User**

An employee wants to remember five tasks for today.

They open the app, see a clean checklist, add reminders, and close completed tasks.

The experience should be effortless.

**Scenario B — Finance Power User**

An accountant is managing:

- 35 daily requests
- Multiple intercompany invoices
- Missing requester information
- Pending approvals
- Month-end deadlines
- Repeated controls
- Tasks with different priorities
- Requests awaiting responses

They should be able to organize this workload efficiently without needing multiple spreadsheets or handwritten notes.

**Scenario C — Follow-Up**

An employee requests information from a colleague and expects a response in two days.

The app should help them remember the follow-up and identify that the task is blocked.

**Scenario D — Month-End Closing**

The employee executes a reusable closing checklist with dependencies and deadlines.

Progress must be clear.

**Scenario E — Laptop Restart**

The user shuts down their laptop with unfinished tasks and pending reminders.

On reopening the app, everything should be preserved and missed reminders should be handled correctly.

**Scenario F — Advanced to Simple**

A power user switches to Simple Mode.

Their complex workflows remain intact, but the interface becomes significantly less cluttered.

---

### 28. DELIVERABLES

Provide a complete project with:

1. Functional application source code
2. Clearly structured project folders
3. Selected architecture documentation
4. Working local database
5. Complete modern UI
6. Functional task management
7. Functional reminder system
8. Recurring tasks
9. Persistent user settings
10. Simple and Advanced modes
11. Finance workflow templates
12. Tests
13. Production build configuration
14. Windows packaging configuration
15. Reproducible build scripts
16. Working executable where build environment supports it
17. Setup and troubleshooting instructions
18. Known limitations
19. Security considerations
20. Feature roadmap

Do not present mockups, static screens, placeholder dashboards, or unimplemented buttons as a completed product.

---

### 29. ENGINEERING STANDARDS

Apply professional engineering standards:

- Clean architecture where useful
- Strong typing
- Clear naming
- Reusable components
- Separation of concerns
- Minimal duplication
- Secure defaults
- Robust state handling
- Defensive programming
- Appropriate test coverage
- Versioned database migrations
- Structured logging
- Consistent code formatting
- Clear documentation

Do not overengineer the application.

Avoid unnecessary microservices, cloud infrastructure, and AI dependencies.

This is a local-first desktop product.

Prioritize simplicity, performance, and maintainability.

---

### 30. FINAL INSTRUCTIONS

Treat this project as a serious commercial desktop application intended for real employees.

Your priorities, in order, are:

1. Reliability and data safety
2. Actual usability
3. Exceptional UI/UX quality
4. Windows desktop compatibility
5. Reminder accuracy
6. Performance
7. Simplicity
8. Advanced capabilities
9. Extensibility

Do not sacrifice a working core to implement dozens of superficial features.

Make reasonable product decisions independently.

Ask questions only when a missing answer would fundamentally block architecture, compliance, or implementation. Otherwise, choose sound defaults and explain significant assumptions.

Do not stop after proposing a plan.

**Proceed with implementation, milestone by milestone, using available development tools.**

At every milestone:
- Implement real functionality.
- Run applicable tests.
- Inspect for errors.
- Fix discovered problems.
- Verify integration with the existing application.
- Keep the project buildable.

The final goal is a beautiful, fast, reliable Windows productivity application that a corporate finance employee can open with a double-click and genuinely use every single working day.

**Build FINANCE OS as a product worth paying for, not as a prototype.**