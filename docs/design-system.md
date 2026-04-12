# Status Tracker — Design System

**Version:** 1.0  
**Date:** 2026-04-11  
**Source:** Derived from `prototype/index.html` and `docs/requirements.md`

---

## Table of Contents

1. [Design Philosophy](#1-design-philosophy)
2. [Color System](#2-color-system)
3. [Typography](#3-typography)
4. [Spacing and Layout](#4-spacing-and-layout)
5. [Shadows and Elevation](#5-shadows-and-elevation)
6. [Border Radius](#6-border-radius)
7. [Motion and Easing](#7-motion-and-easing)
8. [Component Inventory](#8-component-inventory)
9. [Status Indicator Patterns](#9-status-indicator-patterns)
10. [Navigation Design](#10-navigation-design)
11. [Date and Time Display](#11-date-and-time-display)
12. [Responsive Breakpoints](#12-responsive-breakpoints)
13. [Dark Mode Considerations](#13-dark-mode-considerations)
14. [MudBlazor Theme Configuration](#14-mudblazor-theme-configuration)
15. [Accessibility Guidelines](#15-accessibility-guidelines)
16. [White-Label Branding System](#16-white-label-branding-system)

---

## 1. Design Philosophy

### Core Principles

**Information over decoration.** Every visual element must earn its place by communicating data. The dashboard is used operationally — users need to scan it quickly and identify problems. Decorative patterns, gradients, and visual noise are excluded.

**Signal clarity.** Status states (Up, Degraded, Down, Unknown) are the primary communication channel. Color, iconography, and spatial hierarchy all serve the goal of making status immediately legible at a glance.

**Generic by default.** This is a white-label application. No branding, colors, or visual identity is hardcoded. Site title, logo, accent color, and footer are database-driven via the `SiteSettings` table. The design system accommodates any accent color substituted at runtime.

**Density with breathing room.** The dashboard displays many endpoints simultaneously. The design uses compact rows and tight spacing on data, while keeping card surfaces and padding generous enough to avoid cognitive overload.

**Monospace as a design element.** Numeric data (response times, uptime percentages, timestamps, HTTP codes) is rendered in a monospace font to align columns and communicate the technical nature of the values. Monospace is also used for metadata labels.

**Reduced motion respect.** A `prefers-reduced-motion` media query suppresses all animations and transitions for users who have that system preference set.

### Design Register

The application targets developers and DevOps engineers. The aesthetic is professional and technical — clean surfaces, neutral backgrounds, precise typography — not playful or consumer-facing. Think internal tooling with a high polish level.

---

## 2. Color System

### Surface Colors

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-bg` | `#f4f6f9` | Page background |
| `--color-surface` | `#ffffff` | Cards, panels, header |
| `--color-surface-alt` | `#f8f9fb` | Row hover states, group headers, alternating surfaces |

### Text Colors

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-ink` | `#1a1d23` | Primary body text, headings, values |
| `--color-ink-light` | `#4b5060` | Secondary body text, table data |
| `--color-muted` | `#8b92a0` | Labels, metadata, timestamps, helper text |

### Accent Color

The accent is the one color that is runtime-configurable per the `SiteSettings.AccentColor` field. The prototype uses a blue, but the design system treats it as a variable.

| Token | Default Hex | Usage |
|-------|-------------|-------|
| `--color-accent` | `#3d6ce7` | Interactive links, back navigation, active states |
| `--color-accent-hover` | `#2d55c4` | Hover state for accent elements |

The accent color should be injected as a CSS custom property at runtime when the user's configured value differs from the default.

### Status / Semantic Colors

These colors are not configurable. They carry universal meaning and must remain consistent across all deployments for accessibility and usability.

#### Up (Healthy / Operational)

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-up` | `#1a9a45` | Status text, indicator bar, uptime bar segments, chart line |
| `--color-up-bg` | `rgba(26, 154, 69, 0.08)` | Badge background for Up state |

WCAG contrast on white: **4.6:1** (passes AA for normal text at 14px+ bold, and large text).

#### Degraded (Elevated latency, partial failure)

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-degraded` | `#d4910a` | Status text, indicator bar, uptime bar segments |
| `--color-degraded-bg` | `rgba(212, 145, 10, 0.08)` | Badge background for Degraded state |

WCAG contrast on white: **3.5:1** (passes AA for large text and UI components; use bold weight for body text to ensure legibility).

#### Down (Failure / Outage)

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-down` | `#d63031` | Status text, indicator bar, uptime bar segments, spike dots on chart |
| `--color-down-bg` | `rgba(214, 48, 49, 0.08)` | Badge background for Down state |

WCAG contrast on white: **4.5:1** (passes AA).

#### Unknown (No data / Never checked)

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-unknown` | `#8b92a0` | Status text, indicator bar (same as `--color-muted`) |
| `--color-unknown-bg` | `rgba(139, 146, 160, 0.08)` | Badge background for Unknown state |

### Structural Colors (Borders and Rules)

| Token | Hex | Usage |
|-------|-----|-------|
| `--color-border` | `#e0e4eb` | Card and panel outer borders |
| `--color-rule` | `#e8ecf1` | Internal dividers (table header borders, section rules) |
| `--color-rule-light` | `#f0f2f6` | Row dividers within tables (lightest separation) |

### Status Color Quick Reference

```
Up        #1a9a45  ████  Green
Degraded  #d4910a  ████  Amber
Down      #d63031  ████  Red
Unknown   #8b92a0  ████  Gray
```

---

## 3. Typography

### Font Families

| Role | Family | Weights Used | Usage |
|------|---------|-------------|-------|
| Display | Bricolage Grotesque | 400, 600, 700, 800 | Site title, page headings, card titles, metric values |
| Body | DM Sans | 400, 500, 600 | Body text, endpoint names, incident descriptions |
| Mono | JetBrains Mono | 400, 500, 600 | Status labels, URLs, timestamps, uptime percentages, response times, HTTP codes, nav labels, section labels, table headers |

All three fonts are loaded from Google Fonts. In production, self-host the font files or use a CDN with appropriate `font-display: swap` to prevent layout shift.

### Type Scale

The scale uses fluid type with `clamp()`. Values scale continuously between the minimum and maximum based on viewport width.

| Token | Min | Max | Usage |
|-------|-----|-----|-------|
| `--text-xs` | 0.70rem (11.2px) | 0.80rem (12.8px) | Labels, metadata, nav labels, table headers, timestamps |
| `--text-sm` | 0.80rem (12.8px) | 0.875rem (14px) | Table data, URLs, secondary info, incident descriptions |
| `--text-base` | 0.875rem (14px) | 1.00rem (16px) | Body text, endpoint names |
| `--text-lg` | 1.00rem (16px) | 1.25rem (20px) | Card/panel titles (Response Time, Recent Checks, Incident History) |
| `--text-xl` | 1.25rem (20px) | 1.75rem (28px) | System banner status message |
| `--text-2xl` | 1.50rem (24px) | 2.50rem (40px) | Site title, metric values |
| `--text-3xl` | 2.00rem (32px) | 3.50rem (56px) | Detail page endpoint name heading |
| `--text-hero` | 2.50rem (40px) | 5.00rem (80px) | Reserved for future hero/landing sections |

### Typography Hierarchy

```
Site Title              Bricolage Grotesque 800  --text-2xl   letter-spacing: -0.02em
Detail Page H1          Bricolage Grotesque 800  --text-3xl   letter-spacing: -0.02em
Card / Panel Titles     Bricolage Grotesque 700  --text-lg    letter-spacing: -0.01em
Metric Values           Bricolage Grotesque 800  --text-2xl   letter-spacing: -0.02em
System Banner           Bricolage Grotesque 700  --text-xl    letter-spacing: -0.01em

Endpoint Name           DM Sans 600              --text-base
Body / Description      DM Sans 400              --text-sm
Group Header            DM Sans 600              --text-sm    uppercase, letter-spacing: 0.04em

Section Label           JetBrains Mono 600       --text-xs    uppercase, letter-spacing: 0.12em
Nav Label               JetBrains Mono 400       --text-xs    uppercase, letter-spacing: 0.10em
Table Header            JetBrains Mono 600       --text-xs    uppercase, letter-spacing: 0.10em
Status Badge            JetBrains Mono 700       --text-xs    uppercase, letter-spacing: 0.08em
Status Row Text         JetBrains Mono 700       --text-xs    uppercase, letter-spacing: 0.08em
URL / Endpoint URL      JetBrains Mono 400       --text-xs
Timestamps              JetBrains Mono 400       --text-xs
Metric Labels           JetBrains Mono 400       --text-xs    uppercase, letter-spacing: 0.10em
Response / Uptime Data  JetBrains Mono 400       --text-sm
```

### Line Height

- Body text: `1.5`
- Display headings: `1.0`
- Labels and metadata: `1.0` to `1.2`

---

## 4. Spacing and Layout

### Spacing Scale

Built on an 8px (`0.5rem`) base unit.

| Token | Value | Pixels | Usage |
|-------|-------|--------|-------|
| `--space-2xs` | 0.25rem | 4px | Tight gaps (dot-to-text, badge padding vertical, icon-to-label) |
| `--space-xs` | 0.50rem | 8px | Cell padding, small internal gaps |
| `--space-sm` | 1.00rem | 16px | Card padding, row padding, grid gaps |
| `--space-md` | 2.00rem | 32px | Between major sections, header padding |
| `--space-lg` | 4.00rem | 64px | Before footer, large vertical rhythm |
| `--space-xl` | 6.00rem | 96px | Reserved for future spacious layouts |

### Layout Shell

**Max content width:** `1200px`, centered with `margin: 0 auto`.

**Page padding:** `--space-md` (32px) on left and right.

**Header:** Full-width, 1px bottom border, `--space-sm` vertical padding, `--space-md` horizontal padding. Contains site title (left) and nav meta (right).

**Main content:** Single-column within the max-width container. Sections separated by `--space-md` bottom margin.

**Footer:** Full-width, 1px top border, `--space-sm` vertical padding, centered monospace text.

### Grid Layouts

**Endpoint table row (desktop):** 7-column grid

```
6px  |  200px  |  70px   |  70px   |  60px  |  60px  |  1fr
bar  |  name   |  status |  resp   |  24h   |  7d    |  90-day uptime bar
```

**Endpoint table row (mobile, ≤900px):** 3-column grid

```
6px  |  1fr   |  70px
bar  |  name  |  status
```

**Metrics strip:** 4-column equal-width grid (desktop), 2x2 grid (mobile).

---

## 5. Shadows and Elevation

Three shadow levels. All use near-black at low opacity for a natural, soft appearance.

| Token | Value | Usage |
|-------|-------|-------|
| `--shadow-sm` | `0 1px 2px rgba(0,0,0,0.04), 0 1px 4px rgba(0,0,0,0.03)` | Cards with secondary importance (metrics strip, chart, uptime bar, checks table) |
| `--shadow-md` | `0 1px 3px rgba(0,0,0,0.04), 0 4px 12px rgba(0,0,0,0.06)` | Primary data cards (endpoint list table) |
| `--shadow-lg` | `0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)` | Reserved for modals, dropdowns, popovers |

Elevation hierarchy: page background (0) → card surface (sm/md) → floating element (lg).

---

## 6. Border Radius

| Token | Value | Usage |
|-------|-------|-------|
| `--radius-sm` | `6px` | Time range tab buttons, tooltip/date popover on uptime segments |
| `--radius-md` | `10px` | Cards, panels, system banner, endpoint list |
| `--radius-lg` | `14px` | Reserved for large modal dialogs or drawers |
| `100px` (literal) | Full pill | Status badge on detail page header |

Uptime bar segments use `2px` radius — intentionally minimal to read as data, not as buttons.
Check status dot uses `50%` — circular.
Live pulse dot uses `50%` — circular.

---

## 7. Motion and Easing

### Easing Curves

| Token | Value | Character |
|-------|-------|-----------|
| `--ease-out-expo` | `cubic-bezier(0.23, 1, 0.32, 1)` | Fast initial movement, soft landing. Used for row enter animations and hover transitions. |
| `--ease-out-quart` | `cubic-bezier(0.25, 1, 0.5, 1)` | Slightly less aggressive. Used for page transitions. |

### Animation Inventory

| Name | Duration | Usage |
|------|----------|-------|
| `page-enter` | 400ms ease-out-quart | Page swap: fade in + translateY(8px → 0) |
| `row-enter` | 400ms ease-out-expo | Endpoint rows stagger in: fade in + translateX(-8px → 0). Each row delayed by `index * 60ms` via CSS custom property `--i`. |
| `pulse-up` | 2s ease-in-out infinite | Live dot in navigation: opacity 1 → 0.6 → 1 |

### Hover / Interactive Transitions

| Element | Property | Duration |
|---------|----------|----------|
| Endpoint row | background-color | 120ms ease-out-expo |
| Back navigation link | color | 120ms |
| Time range tab | background, color | 120ms |

### Reduced Motion

All durations collapse to `0.01ms` when `prefers-reduced-motion: reduce` is set. The `animation-iteration-count` is also set to `1` to stop infinite loops.

---

## 8. Component Inventory

### 8.1 Site Header

A sticky top bar containing the site title and navigation metadata.

- **Site title:** Display font, `--text-2xl`, weight 800. Clicking navigates back to the dashboard. In production this is the `SiteSettings.SiteTitle` value.
- **Nav meta (right side):** Three items separated by pipe characters. Font: mono, `--text-xs`, uppercase. Contains: live pulse dot + "Live" label, separator, "Last updated: [timestamp]".
- **Background:** `--color-surface` with `--shadow-sm`.
- **Bottom border:** 1px `--color-border`.

### 8.2 System Status Banner

Full-width summary card at the top of the dashboard. States:

| State | Left border color | Text example |
|-------|------------------|--------------|
| All systems operational | `--color-up` | "All systems operational" |
| Partial degradation | `--color-degraded` | "Partial degradation detected" |
| Outage | `--color-down` | "Service disruption detected" |

- **Border-left:** 4px solid status color. This is the primary state indicator.
- **Meta text (right):** Mono `--text-xs`, muted. Shows count (e.g., "1 of 8 endpoints reporting issues").
- **Border radius:** `--radius-md`. **Shadow:** `--shadow-sm`.

### 8.3 Endpoint Group

A collapsible section heading above a set of endpoint rows.

- **Group header:** Body font, `--text-sm`, weight 600, uppercase, letter-spacing 0.04em. Background `--color-surface-alt`. Inline count badge in mono/muted.
- **Endpoint list container:** White card with `--radius-md`, `--shadow-md`, border `--color-border`.
- **Column header row:** Mono font, `--text-xs`, muted. Columns: (blank), Endpoint, Status, Resp., 24h, 7d, 90 Days.

### 8.4 Endpoint Row

The primary data row. Clickable, navigates to the detail page.

Columns (left to right):
1. **Status indicator bar** — 6px wide, full row height, colored by status. No label.
2. **Name cell** — Endpoint display name (body font, weight 600) over the URL in mono/muted/xs.
3. **Status text** — Mono, `--text-xs`, weight 700, uppercase. Colored by status.
4. **Response time** — Mono, `--text-sm`. Right-aligned.
5. **24h uptime %** — Mono, `--text-sm`. Right-aligned.
6. **7d uptime %** — Mono, `--text-sm`. Right-aligned.
7. **90-day uptime bar** — Visual strip of 90 segments. Each segment colored by day's status.

Hover state: background changes to `--color-surface-alt`.

### 8.5 Status Badge (Detail Page Header)

A pill-shaped badge indicating current endpoint state on the detail view.

- Shape: `border-radius: 100px` (full pill).
- Font: Mono, `--text-xs`, weight 700, uppercase.
- Style: colored border + matching background at 8% opacity + text in status color.
- States: Operational (up), Degraded, Down.

### 8.6 Metrics Strip

A horizontal row of 4 metric cells displayed on the detail page below the header.

- **Container:** 4-column grid, white card, `--radius-md`, `--shadow-sm`.
- **Cell dividers:** 1px right border `--color-rule` (last cell has none).
- **Label:** Mono `--text-xs`, uppercase, muted. `display: block`, margin below.
- **Value:** Display font, `--text-2xl`, weight 800. May include `--text-lg` for longer strings like "12d ago".
- **Unit:** Inline mono `--text-xs`, muted (e.g., "%" or "ms").

Metrics shown: Uptime (30d), Avg Response, Total Checks, Last Incident.

### 8.7 Response Time Chart

Canvas-based line chart. In production this is replaced with an ApexCharts component.

- **Container:** White card, `--radius-md`, `--shadow-sm`, `--space-md` padding.
- **Header:** Card title (display font, `--text-lg`, weight 700) left, time range tabs right.
- **Chart area:** 200px height. Line color: `--color-up`. Area fill: `rgba(26, 154, 69, 0.06)`. Grid lines: `--color-rule`. Spike dots (high response time): `--color-down`.
- **Axis labels:** JetBrains Mono 10–11px, muted color.

**Time Range Tabs:** A segmented button group. Tabs are borderless buttons with `--radius-sm` on ends. Active state: dark background (`--color-ink`), white text.

### 8.8 90-Day Uptime Bar (Detail Page)

- **Container:** White card, `--radius-md`, `--shadow-sm`.
- **Segments:** 90 equal-width bars, `36px` height, `2px` gap, `2px` border-radius.
- **Legend:** Two mono/muted/xs labels — "90 days ago" (left) and "Today" (right).
- **Uptime percentage:** Displayed in the card header, mono font, `--text-lg`, weight 700, colored `--color-up`.
- **Hover tooltip:** Date label appears above the hovered segment using a CSS pseudo-element. Styled as a small popover with `--radius-sm`, border, and `--shadow-sm`.

### 8.9 Recent Checks Table

Standard data table on the detail page.

- **Container:** White card, `--radius-md`, `--shadow-sm`, hidden overflow.
- **Card header:** Display font, `--text-lg`, weight 700.
- **Column headers:** Mono `--text-xs`, uppercase, muted. Bottom border `--color-rule`.
- **Data cells:** Mono `--text-sm`, `--color-ink-light`. Row dividers: `--color-rule-light`.
- **Row hover:** `--color-surface-alt`.
- **Status column:** 8px circular dot (filled with `--color-up` or `--color-down`) followed by status text.
- **Columns:** Timestamp, Status, Response, HTTP (code).

### 8.10 Incident History Panel

A timeline-style list of past incidents.

- **Container:** White card, `--radius-md`, `--shadow-sm`, hidden overflow.
- **Card header:** Display font, `--text-lg`, weight 700.
- **Each incident:** 3-column grid: 4px status bar (left) | incident text (center) | timestamp + duration (right).
- **Status bar:** 4px wide, full item height, `2px` border-radius. Red (`--color-down`) for active, green (`--color-up`) for resolved.
- **Incident title:** Body font, `--text-base`, weight 600.
- **Description:** `--text-sm`, `--color-ink-light`.
- **Timestamp:** Mono `--text-xs`, muted, right-aligned.
- **Duration:** Mono `--text-xs`, muted, displayed below timestamp.

### 8.11 Section Label

A styled heading for content sections on the dashboard.

- Font: Mono, `--text-xs`, weight 600, uppercase, letter-spacing 0.12em, muted color.
- Bottom border: 1px `--color-rule`.
- Bottom margin: `--space-sm`.

### 8.12 Back Navigation Link

Used on the detail page to return to the dashboard.

- Font: Mono, `--text-xs`, uppercase, letter-spacing 0.10em.
- Color: `--color-accent`. Hover: `--color-accent-hover`.
- Content: left-arrow character + "All endpoints".
- No border, no background — pure text link style.

---

## 9. Status Indicator Patterns

Status is communicated through multiple redundant channels. Never rely on color alone.

### Communication Layers

| Layer | Up | Degraded | Down | Unknown |
|-------|----|----------|------|---------|
| Left indicator bar (row) | Green solid | Amber solid | Red solid | Gray solid |
| Status text (row) | "UP" | "DEGRADED" | "DOWN" | "UNKNOWN" |
| Status badge (detail) | "OPERATIONAL" | "DEGRADED" | "DOWN" | "UNKNOWN" |
| System banner border | Green | Amber | Red | — |
| Uptime bar segments | Green | Amber | Red | Light gray |
| Chart spike dots | — | — | Red dot | — |
| Incident bar | Green (resolved) | — | Red (active) | — |

### Status Hierarchy (Dashboard Summary)

When computing the overall system status for the banner:
- Any endpoint Down → system is **Down**
- No Down, but any Degraded → system is **Degraded**
- All Up or Unknown → system is **Operational**

### Live Indicator

The "Live" label in the nav includes a 6px circular dot that pulses (opacity 1 → 0.6, 2s infinite). This communicates that the data is being refreshed in real time. Color: `--color-up`.

---

## 10. Navigation Design

### Structure

The application has a two-level navigation model:

**Level 1 — Dashboard:** The root view. Shows all endpoint groups and their rows. Always accessible by clicking the site title.

**Level 2 — Endpoint Detail:** Drill-down for a single endpoint. Reached by clicking any endpoint row. A back link returns to the dashboard.

No sidebar, no tabs, no secondary navigation. The single-page nature of the prototype (page swap via class) maps directly to Blazor's routing model with two routes: `/` (dashboard) and `/endpoint/{id}` (detail).

### Navigation Updates Required When Adding New Pages

Any future page (e.g., admin settings, endpoint management) must:
1. Be reachable from the header navigation (add to `site-nav` or a dropdown).
2. Include the site title back-link to the dashboard.
3. Use the same page transition animation class.

### Admin Navigation (Future)

An admin-only section will require a nav element distinguishable from the public dashboard. Recommended approach: add an icon-only or labeled link in the header (visible only to authenticated users) that opens an `/admin/` prefix route tree. Do not create orphan admin pages.

---

## 11. Date and Time Display

### Principles

All timestamps in the UI must be displayed in the user's local timezone. The application stores timestamps in UTC (the `CheckResult.Timestamp` field). Conversion to local time happens at render time.

### Formatting Patterns

| Context | Format | Example |
|---------|--------|---------|
| Recent checks timestamp | `h:mm:ss a` (local time) | `2:47:33 PM` |
| Incident date | `MMM d, yyyy` (local date) | `Mar 30, 2026` |
| Last updated (header) | `h:mm:ss a` (local time) | `02:17:45 PM` |
| Uptime bar tooltip | `MMM d` (local date) | `Jan 12` |
| Relative time | Human-readable delta | `12d ago`, `3h ago` |

### Blazor Implementation Note

Use `DateTime.ToLocalTime()` or JavaScript `toLocaleTimeString()` with the browser's locale. Do not pass raw UTC strings to the UI. The prototype uses `toLocaleTimeString('en-US', ...)` — in Blazor, rely on the browser timezone via JS interop or format using the user's culture from `CultureInfo.CurrentCulture`.

---

## 12. Responsive Breakpoints

### Defined Breakpoints

| Name | Max-width | Behavior |
|------|-----------|---------|
| Mobile | ≤ 900px | Endpoint table collapses to 3 columns (bar, name, status). Response time and uptime columns hidden. Metrics strip goes 2×2. Detail header stacks vertically. |

A single breakpoint covers the prototype scope. Future additions should follow a mobile-first approach with these additions:

| Name | Target | Recommended max-width |
|------|--------|-----------------------|
| Tablet | iPad portrait | ≤ 768px |
| Mobile | Phones | ≤ 480px |

### Responsive Rules

- The main content column (`max-width: 1200px`) uses `padding: var(--space-md)` on all sides, which naturally provides safe margins on small screens.
- Endpoint names may overflow on very narrow screens. Use `overflow: hidden; text-overflow: ellipsis` on name cells below 480px.
- The 90-day uptime bar segments (each `min-width: 2px`) will compress gracefully on narrow viewports. Below ~320px, consider reducing the visible day count.
- The chart canvas uses `width: 100%` and redraws on window resize — this pattern carries forward to ApexCharts via its `Width="100%"` property.

---

## 13. Dark Mode Considerations

The prototype is light-mode only. Dark mode is not currently implemented but the token architecture supports it cleanly.

### Recommended Dark Mode Token Overrides

A dark mode can be activated via a `data-theme="dark"` attribute on `<html>` or via `@media (prefers-color-scheme: dark)`.

| Light Token | Light Value | Recommended Dark Value |
|-------------|-------------|------------------------|
| `--color-bg` | `#f4f6f9` | `#0f1117` |
| `--color-surface` | `#ffffff` | `#1a1d23` |
| `--color-surface-alt` | `#f8f9fb` | `#21242c` |
| `--color-ink` | `#1a1d23` | `#f0f2f6` |
| `--color-ink-light` | `#4b5060` | `#9ba3b2` |
| `--color-muted` | `#8b92a0` | `#5c6370` |
| `--color-border` | `#e0e4eb` | `#2a2e38` |
| `--color-rule` | `#e8ecf1` | `#252830` |
| `--color-rule-light` | `#f0f2f6` | `#1e2128` |

Status and accent colors remain the same in dark mode. The 8% opacity background tokens on status colors may need to increase to ~12% in dark mode for visibility.

Shadow values in dark mode: replace `rgba(0,0,0,...)` with `rgba(0,0,0,...)` at higher opacity, or invert to use highlight-based shadows (glow).

MudBlazor dark mode: set `Dark = true` on `MudTheme` or use `MudThemeProvider` with `IsDarkMode`.

---

## 14. MudBlazor Theme Configuration

MudBlazor uses Material Design tokens. The design system maps to MudBlazor's theming API as follows.

### Palette Mapping

```csharp
var theme = new MudTheme
{
    PaletteLight = new PaletteLight
    {
        // Primary = SiteSettings.AccentColor (loaded at runtime)
        Primary         = "#3d6ce7",
        PrimaryDarken   = "#2d55c4",

        // Status colors (not assigned to MudBlazor semantic slots —
        // use custom CSS variables alongside MudBlazor)
        Success         = "#1a9a45",
        Warning         = "#d4910a",
        Error           = "#d63031",
        Info            = "#8b92a0",  // Unknown / neutral

        Background      = "#f4f6f9",
        Surface         = "#ffffff",
        DrawerBackground = "#ffffff",
        AppbarBackground = "#ffffff",
        AppbarText      = "#1a1d23",

        TextPrimary     = "#1a1d23",
        TextSecondary   = "#4b5060",
        TextDisabled    = "#8b92a0",

        ActionDefault   = "#1a1d23",
        ActionDisabled  = "#8b92a0",

        Divider         = "#e0e4eb",
        DividerLight    = "#f0f2f6",

        TableLines      = "#e8ecf1",
        TableHover      = "#f8f9fb",

        DrawerText      = "#1a1d23",
        DrawerIcon      = "#4b5060",
    },
    PaletteDark = new PaletteDark
    {
        Primary         = "#3d6ce7",
        Success         = "#1a9a45",
        Warning         = "#d4910a",
        Error           = "#d63031",
        Background      = "#0f1117",
        Surface         = "#1a1d23",
        AppbarBackground = "#1a1d23",
        DrawerBackground = "#1a1d23",
        TextPrimary     = "#f0f2f6",
        TextSecondary   = "#9ba3b2",
        TextDisabled    = "#5c6370",
        Divider         = "#2a2e38",
        TableHover      = "#21242c",
    },
    Typography = new Typography
    {
        Default = new Default
        {
            FontFamily = new[] { "DM Sans", "sans-serif" },
            FontSize   = "1rem",
            LineHeight = 1.5,
        },
        H1 = new H1
        {
            FontFamily   = new[] { "Bricolage Grotesque", "sans-serif" },
            FontSize     = "clamp(2rem, 1.5rem + 2.5vw, 3.5rem)",
            FontWeight   = 800,
            LetterSpacing = "-0.02em",
        },
        H2 = new H2
        {
            FontFamily   = new[] { "Bricolage Grotesque", "sans-serif" },
            FontSize     = "clamp(1.5rem, 1.1rem + 1.8vw, 2.5rem)",
            FontWeight   = 800,
            LetterSpacing = "-0.02em",
        },
        H3 = new H3
        {
            FontFamily   = new[] { "Bricolage Grotesque", "sans-serif" },
            FontSize     = "clamp(1.25rem, 1rem + 1vw, 1.75rem)",
            FontWeight   = 700,
            LetterSpacing = "-0.01em",
        },
        H6 = new H6
        {
            // Used for card titles
            FontFamily   = new[] { "Bricolage Grotesque", "sans-serif" },
            FontSize     = "clamp(1rem, 0.9rem + 0.5vw, 1.25rem)",
            FontWeight   = 700,
            LetterSpacing = "-0.01em",
        },
        Caption = new Caption
        {
            FontFamily   = new[] { "JetBrains Mono", "monospace" },
            FontSize     = "clamp(0.7rem, 0.65rem + 0.25vw, 0.8rem)",
            LineHeight   = 1.2,
        },
        Overline = new Overline
        {
            FontFamily   = new[] { "JetBrains Mono", "monospace" },
            FontSize     = "clamp(0.7rem, 0.65rem + 0.25vw, 0.8rem)",
            LetterSpacing = "0.12em",
        },
        Body2 = new Body2
        {
            FontFamily = new[] { "JetBrains Mono", "monospace" },
            FontSize   = "clamp(0.8rem, 0.75rem + 0.25vw, 0.875rem)",
        },
    },
    Shadows = new Shadow
    {
        // MudBlazor shadow indices 1–3 map to sm/md/lg
        Elevation = new string[]
        {
            "none",                                                                                    // 0
            "0 1px 2px rgba(0,0,0,0.04), 0 1px 4px rgba(0,0,0,0.03)",                               // 1 = sm
            "0 1px 3px rgba(0,0,0,0.04), 0 4px 12px rgba(0,0,0,0.06)",                              // 2 = md
            "0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)",                              // 3 = lg
            // ... fill remaining indices with elevation 3 or progressive values
        }
    },
    LayoutProperties = new LayoutProperties
    {
        DefaultBorderRadius = "10px",   // --radius-md is the primary card radius
        DrawerWidthLeft     = "260px",
        AppbarHeight        = "64px",
    }
};
```

### Runtime Accent Color Injection

The `SiteSettings.AccentColor` field allows per-deployment accent customization. Implement this by:

1. Loading `SiteSettings` at app startup.
2. Injecting the color as a CSS custom property on `<html>` via a Blazor layout's `OnAfterRenderAsync`.
3. Passing the color to `MudThemeProvider` by building the `MudTheme` dynamically with the stored `AccentColor` as `PaletteLight.Primary`.

```razor
@* In MainLayout.razor *@
<MudThemeProvider Theme="@_theme" />

@code {
    private MudTheme _theme = default!;

    protected override async Task OnInitializedAsync()
    {
        var settings = await SiteSettingsService.GetAsync();
        _theme = ThemeFactory.Build(settings.AccentColor);
    }
}
```

### MudBlazor Component Recommendations

| UI Element | MudBlazor Component | Notes |
|------------|---------------------|-------|
| Endpoint table | `MudTable` or custom `MudPaper` + CSS grid | CSS grid provides the 7-column layout exactly. `MudTable` is more accessible but less flexible for the custom indicator bar. |
| Status badges | `MudChip` with custom color class | Apply status color via `Color="Color.Success"` etc., or use `Class` attribute with CSS custom properties. |
| System banner | `MudAlert` | Severity maps to status: `Severity.Success`, `Severity.Warning`, `Severity.Error`. Override left border style. |
| Metric cards | `MudPaper` + custom layout | Or `MudCard` with `MudCardContent`. |
| Charts | `ApexCharts` (Blazor wrapper) | Line chart for response time. Bar/segment chart for uptime history. |
| Time range tabs | `MudToggleGroup` or `MudChipSet` | Segmented control behavior. |
| Admin forms | `MudTextField`, `MudNumericField`, `MudSwitch`, `MudSelect` | With `FluentValidation` via `MudForm` and `DataAnnotationsValidator`. |
| Modals / Dialogs | `MudDialog` | For delete confirmation (EP-6). |
| Nav | `MudAppBar` + `MudText` | Keep header minimal per prototype. |

---

## 15. Accessibility Guidelines

Target: WCAG 2.1 Level AA minimum across all components.

### Color Contrast

| Pairing | Ratio | Pass Level |
|---------|-------|------------|
| `--color-ink` on `--color-surface` | 16.8:1 | AAA |
| `--color-ink-light` on `--color-surface` | 7.0:1 | AAA |
| `--color-muted` on `--color-surface` | 3.6:1 | AA (large text and UI components only) |
| `--color-up` on `--color-surface` | 4.6:1 | AA |
| `--color-down` on `--color-surface` | 4.5:1 | AA |
| `--color-degraded` on `--color-surface` | 3.5:1 | AA large text / UI components |
| `--color-accent` on `--color-surface` | 4.5:1 | AA |

**Action required for `--color-muted` and `--color-degraded`:** These do not pass AA at small normal-weight text sizes. All muted-color text must be `--text-xs` or larger with a contrasting context. Degraded status text uses `font-weight: 700` (bold) and uppercase, which improves perceived contrast. Monitor these ratios if deploying a custom accent color.

### Status Accessibility (Not Color-Only)

All status states are communicated through text labels in addition to color:
- Endpoint rows include a text status column ("UP", "DEGRADED", "DOWN").
- The system banner contains a text description.
- Status badges use text ("OPERATIONAL", "DEGRADED", "DOWN").
- The uptime bar segments alone are color-only. Add a summary text value (e.g., "99.95%") and an ARIA label on the container: `aria-label="90-day uptime history"`.

### Keyboard Navigation

- All interactive elements (endpoint rows, time range tabs, back link, site title) must be focusable.
- Endpoint rows rendered as `<div>` elements must receive `role="button"`, `tabindex="0"`, and handle `Enter`/`Space` key events. In Blazor, prefer `<button>` or `<a>` elements over click-handled `<div>` elements.
- Time range tabs are `<button>` elements — carry this through to the MudBlazor implementation.
- Focus rings must be visible. Do not suppress `outline` without providing a custom focus style.

### ARIA Roles and Labels

| Component | Recommended ARIA |
|-----------|-----------------|
| Endpoint list table | `role="table"` with `aria-label="Monitored endpoints"` if using CSS grid instead of `<table>` |
| System banner | `role="status"` or `aria-live="polite"` for real-time updates |
| Uptime bar container | `aria-label="90-day uptime history: [X]%"` |
| Live dot | `aria-label="Live updates active"` or `aria-hidden="true"` if adjacent text covers the meaning |
| Detail back link | `aria-label="Back to all endpoints"` |
| Time range tabs | `role="group"` on container, each button uses `aria-pressed` |

### Forms (Admin UI)

- All form fields must have visible, associated `<label>` elements (not just placeholders).
- Validation errors must be surfaced via `aria-describedby` linking the field to its error message.
- Required fields must be marked with `aria-required="true"` and a visible indicator.
- `MudForm` with `FluentValidation` handles most of this natively.

### Motion

The `prefers-reduced-motion` override in the CSS foundation covers the row stagger and page transition animations. Verify that ApexCharts animation is also disabled when reduced motion is active (ApexCharts supports `chart.animations.enabled: false`).

---

## 16. White-Label Branding System

### Database-Driven Fields

All fields in the `SiteSettings` table are user-configurable at runtime through the admin UI. No source code change is needed.

| Field | Default | Design Impact |
|-------|---------|---------------|
| `SiteTitle` | "Status Tracker" | Replaces the header `site-title` text; also used in `<title>` and footer. |
| `LogoUrl` | `null` | If set, displays an `<img>` before or in place of the text title. Scale to `32px` height max in the header. |
| `AccentColor` | `#3d6ce7` | Replaces `--color-accent` and MudBlazor `Primary` palette. Must be validated as a hex color. |
| `FooterText` | "Powered by Status Tracker · Checks every 60s" | Replaces footer content. |

### Invariant Design Elements

The following must not be customizable through branding settings (they carry semantic meaning):

- Status colors: Up green, Degraded amber, Down red, Unknown gray
- All status text labels (UP, DOWN, DEGRADED, UNKNOWN, OPERATIONAL)
- Shadow and border-radius tokens
- Typography font families (Bricolage Grotesque, DM Sans, JetBrains Mono)

### Accent Color Validation

When an admin sets `AccentColor`, validate that:
1. It is a valid CSS hex color (`#RRGGBB` or `#RGB`).
2. It achieves at least 4.5:1 contrast against `--color-surface` (`#ffffff`) to maintain WCAG AA for interactive links and buttons.
3. It achieves at least 3.0:1 contrast against `--color-bg` (`#f4f6f9`).

Reject submissions that fail the contrast check and surface a clear validation error.

### Logo Guidelines

- Recommended logo dimensions: `32px` height, `auto` width.
- Supported formats: PNG (with transparency), SVG.
- Maximum file size for URL-referenced logos: document this as a deployment concern (no inline storage in v1).
- If both `LogoUrl` and `SiteTitle` are set, display both: logo on the left, title text to the right.

---

## Appendix A — Design Token Reference

All CSS custom properties defined in the prototype, in declaration order:

```css
/* Surfaces */
--color-bg:          #f4f6f9
--color-surface:     #ffffff
--color-surface-alt: #f8f9fb

/* Text */
--color-ink:         #1a1d23
--color-ink-light:   #4b5060
--color-muted:       #8b92a0

/* Accent (runtime configurable) */
--color-accent:      #3d6ce7
--color-accent-hover:#2d55c4

/* Status (invariant) */
--color-up:          #1a9a45
--color-up-bg:       rgba(26, 154, 69, 0.08)
--color-down:        #d63031
--color-down-bg:     rgba(214, 48, 49, 0.08)
--color-degraded:    #d4910a
--color-degraded-bg: rgba(212, 145, 10, 0.08)
--color-unknown:     #8b92a0
--color-unknown-bg:  rgba(139, 146, 160, 0.08)

/* Structure */
--color-border:      #e0e4eb
--color-rule:        #e8ecf1
--color-rule-light:  #f0f2f6

/* Shadows */
--shadow-sm: 0 1px 2px rgba(0,0,0,0.04), 0 1px 4px rgba(0,0,0,0.03)
--shadow-md: 0 1px 3px rgba(0,0,0,0.04), 0 4px 12px rgba(0,0,0,0.06)
--shadow-lg: 0 2px 4px rgba(0,0,0,0.04), 0 8px 24px rgba(0,0,0,0.08)

/* Radius */
--radius-sm: 6px
--radius-md: 10px
--radius-lg: 14px

/* Typography */
--font-display: 'Bricolage Grotesque', sans-serif
--font-body:    'DM Sans', sans-serif
--font-mono:    'JetBrains Mono', monospace

/* Type Scale */
--text-xs:   clamp(0.7rem,   0.65rem + 0.25vw, 0.8rem)
--text-sm:   clamp(0.8rem,   0.75rem + 0.25vw, 0.875rem)
--text-base: clamp(0.875rem, 0.82rem + 0.3vw,  1rem)
--text-lg:   clamp(1rem,     0.9rem  + 0.5vw,  1.25rem)
--text-xl:   clamp(1.25rem,  1rem    + 1vw,    1.75rem)
--text-2xl:  clamp(1.5rem,   1.1rem  + 1.8vw,  2.5rem)
--text-3xl:  clamp(2rem,     1.5rem  + 2.5vw,  3.5rem)
--text-hero: clamp(2.5rem,   1.8rem  + 4vw,    5rem)

/* Spacing */
--space-unit: 0.5rem
--space-2xs:  0.25rem   (4px)
--space-xs:   0.5rem    (8px)
--space-sm:   1rem      (16px)
--space-md:   2rem      (32px)
--space-lg:   4rem      (64px)
--space-xl:   6rem      (96px)

/* Easing */
--ease-out-expo:  cubic-bezier(0.23, 1, 0.32, 1)
--ease-out-quart: cubic-bezier(0.25, 1, 0.5, 1)
```

---

## Appendix B — Component States Checklist

For each interactive component, verify all states are designed:

| State | Endpoint Row | Time Range Tab | Back Link | Admin Form Field |
|-------|-------------|----------------|-----------|-----------------|
| Default | White bg | Muted text, border | Accent color | Border, label |
| Hover | `--color-surface-alt` bg | `--color-surface-alt` bg | Darker accent | Border highlight |
| Focus | Visible outline | Visible outline | Visible outline | Focused border |
| Active / Selected | N/A | Dark bg, white text | N/A | N/A |
| Disabled | N/A | N/A | N/A | Muted bg, muted text |
| Error | N/A | N/A | N/A | Red border, error message |
| Loading | Skeleton / shimmer | N/A | N/A | N/A |
