# FPS Project Working Rules

## Unity Project Exploration Rules
This is a large Unity project. Do not begin non-trivial work with a repository-wide recursive grep.
- For scenes, prefabs, GameObjects, components, GUIDs, serialized references, or current Editor state, query Unity-Skills first.
- For C# symbols, callers/callees, architecture, references, or change impact, query the local Codebase Memory MCP first.
- Read source only after the relevant symbols/assets have been narrowed down.
- Do not read raw `.meta`, `.unity`, or `.prefab` YAML when the Editor route can answer the question.
- Treat both indexes as best-effort evidence; check coverage, timestamps, Git status, source, and Unity state before making negative or destructive claims.
- Save Gate (Mandatory): Always save existing authored scenes and call AssetDatabase.SaveAssets() before and after any editor action or test; never save or prompt for Untitled/temporary scenes, and ensure isDirty == false before finishing.

## Scope, Precedence & Approval Gates
- Precedence: 1. Direct user instructions & approved flow/mockup > 2. Project rules (this file) > 3. Skill guidance. If conflicting, follow the higher rank and cite it in the report.
- Approval Sequence: Flow Approved → Mockup Approved (UI only) → Code Implementation.
  - Never add, modify, or delete interactables, quests, or narrative content without an explicitly approved flow. Approval for one plan does not apply to another.
  - Mockup approval must precede UI code.
  - Self-approval is strictly forbidden; approval requires explicit user confirmation or an independent reviewer.
- Scope Boundary: Apply only skill sections relevant to the current task. Do not touch weapons, audio, AI, or save logic unless requested. List extras as proposals, never implement them.

## Art & UI Visual Standards
- Asset Hierarchy: Inspect `Assets/` first (inspect before inventing). Reuse existing fonts, frames, icons, and prefabs.
- No Primitives: Never draw UI/props via code, primitives, or flat colors. Use clearly marked greybox placeholders if missing. No image generation without user approval.
- World Space: No floating 3D text. Use decals, physical signage, or screen-space tags upon aiming. Track source and license for every asset.
- UI Theme Standard: Max 4 colors (White main, Orange immediate action, Red danger, Gray secondary), 1 main font + 1 numeric font, 3 text sizes, 9-sliced panels. No hardcoded styles outside the theme.
- Simplicity: Show only contextual information. Merge health and infection into one bar.

## Skill Routing & Evidence
- Execution Evidence: Open relevant `SKILL.md` before starting. In the report, state: skills read and the exact rule applied. Never claim unread skills.
- Routing:
  - Quest/level flow: `level-design`
  - HUD/UI: `unity-mcp-ui-layout`, `game-ui-ux`
  - Feedback/audio: `game-feel`, `audio-design`
  - Combat/encounters/Tank: `fps-shooter`
  - Save/checkpoints: `save-systems` (atomic writes, schema version, no GameObject references)

## Network, Verification & Reports
- Authority: Server decides completion, phase, prerequisites, and Tank state. Clients display and request only. Test flow as both host and client.
- Tests & Evidence: Flow changes must include working Editor tests (paste real execution logs). Label claims with `[CODE file:line]`, `[SCENE]`, `[PLAYTEST screenshot]`, or `[INFERENCE]`.
- UI Done Criteria: Requires 1080p and 4K before/after Game-view screenshots + playtest of the modified step.
- Output: Reports are written in Vietnamese and saved in `Reports/` (outside `Assets/`). Delete review screenshots once independent review finishes. If the scene is dirty before a read-only task, report and abort immediately.