# Task 2 — K4RMA Complete Game UI/UX Transformation

## Mission

Upgrade K4RMA's existing basic/prototype-looking UI and game presentation into a polished, distinctive and cohesive 2D action-game experience.

Transform the implemented Unity screens, interactions, reusable components and visual assets—not merely design a mockup.

Preserve the established K4RMA colors, story, Japanese-fantasy sword-training atmosphere, character designs, official requirements and latest approved decisions.

The result should feel like an interface created specifically for K4RMA.

## Scope and sequencing

Begin Task 2 only after Task 1 is technically implemented and has completed available verification.

Use a fresh Cursor Agent conversation with the same repository and persistent rules.

Verify source art and latest design decisions before choosing the interface direction.

Task 2 is broader than the original assigned boss subsystem. Inspect teammate ownership before editing shared UI scenes or systems. Avoid destructive collisions with concurrent work.

## Research and design references

Use real game references for their design principles, not to copy their artwork, screens or styles.

### Hollow Knight

Study:
- Low visual clutter.
- Compact and symbolic HUD information.
- Ornamental consistency.
- Atmosphere without sacrificing readability.

Apply to K4RMA's approved visual direction rather than importing Hollow Knight imagery.

### Sekiro: Shadows Die Twice

Study:
- Strong combat hierarchy.
- Boss status clarity.
- Immediate combat feedback.
- Minimal distractions around the main action.

Do not add unapproved posture mechanics.

### Persona 5 Royal

Study:
- Memorable menu composition.
- Highly recognizable selection state.
- Responsive motion and navigation feedback.
- Effective graphic hierarchy.

Do not copy its red-black comic aesthetic, fonts, character illustrations or screen layouts.

### Ghost of Tsushima

Study:
- Restrained Japanese-inspired atmosphere.
- Integration between world identity and interface.
- Refined material, motion and composition.

Only use motifs supported by K4RMA's actual art direction.

### Unity official samples and documentation

Review relevant Unity UI guidance and samples such as Dragon Crashers for reusable views, navigation and data binding.

Choose uGUI, UI Toolkit or a justified hybrid based on the existing Unity project. Do not migrate frameworks merely because one is newer.

## Mandatory art-direction discovery

Inspect the game's presentations, Notion, current screens, characters and environments.

Derive an actual K4RMA visual system:

- Approved palette and accent colors.
- Surface materials and background treatment.
- Existing visual motifs.
- Typography and Korean glyph requirements.
- Rendering style.
- Pixel density and sprite filtering.
- Ornament proportions.
- Consistent spacing.
- Motion style and transition timing.
- Combat-specific visual priority.

Do not default to generic fantasy gold, black/red, cyberpunk neon, glassmorphism or SaaS design.

Candidate motifs such as engraved sword edges, dojo architecture, ink, training seals or decorative paper may be used only when consistent with approved artwork.

## Target design quality

The UI must feel deliberately designed, not like default Unity text and buttons with a new texture.

Improve five dimensions:

1. **Visual identity:** the interface unmistakably belongs to K4RMA.
2. **Hierarchy:** important game information is instantly recognizable.
3. **Interaction:** selections, confirmation, cancellation, navigation and disabled states feel responsive.
4. **Motion:** transitions feel intentional, restrained and coherent.
5. **Integration:** menus, HUD and combat presentation share a recognizable visual language.

Do not create visual complexity for its own sake.

## UI design system

Build practical reusable components:

- Primary and secondary buttons.
- Selected/focused/hovered/pressed/disabled states.
- Keyboard navigation and focus.
- Panel frames and background materials.
- Technique icons and inherited-skill indicators.
- Health displays.
- Section headings and dividers.
- Context prompts.
- Confirmation overlays.
- Transition effects.
- Text hierarchy and fallback fonts.
- UI sound/motion responses using suitable approved or compatible assets.

Do not overengineer a general-purpose design framework.

### Example button transformation

Instead of:

`[ START GAME ]`

Create a visually distinct action appropriate to the established game-world materials, with meaningful hierarchy, clear focused state, immediate press feedback and a short transition into gameplay.

Avoid generic rounded web-app buttons.

## Screen 1 — Title and main menu

Implement a coherent title composition using existing approved artwork.

Requirements:

- Preserve K4RMA title/logo identity.
- Strong first impression.
- Clear actions.
- Visible keyboard selection.
- Appropriate hover and press feedback.
- Functional navigation.
- Elegant, short transitions.
- No unnecessary delays.
- Consistency with in-game presentation.

## Screen 2 — Story

The official story must remain unchanged.

Upgrade typography, layout, scrolling, background and visual hierarchy.

Make the complete text comfortably readable and provide working return/navigation behavior.

Do not sacrifice legibility to decoration.

## Screen 3 — Controls

Use verified actual bindings.

Create a visual controls guide with recognizable key indicators and grouped actions.

Clearly explain original skills and personalized abilities.

Do not invent controls.

Support Korean text and correct font fallback.

## Screen 4 — Combat HUD

Preserve mandatory information:

- Player health.
- Guardian health and name.
- Stage.
- Original technique availability.
- Personalized techniques.
- Guardian inheritance state.

Upgrade:

- Placement and hierarchy.
- Visual proportions.
- Material and ornamental treatments.
- Technique iconography.
- Active/disabled states.
- Health depletion feedback.
- Combat readability.
- Information density.
- Responsive anchoring.

Keep central combat space clear.

Never remove mandatory information merely to imitate minimalist games.

## Screen 5 — Technique personalization

Treat this as K4RMA's flagship interaction.

Support the three original techniques:

- 검기 → 관통베기.
- 상승베기 → 체공.
- 방어 → 반격.

Communicate:

- What original technique the player relinquishes.
- What personalized ability is gained.
- What original ability the Guardian inherits.
- Which choices are currently available.
- Whether a selection is provisional or confirmed.

Build a visually expressive technique-choice experience rooted in approved art, not three generic cards.

Use suitable symbols or technique seals only when stylistically justified.

Provide real selection, cancellation, confirmation and transition behavior.

All six technique orders must remain functional.

## Screen 6 — Stage and Guardian transitions

Show stage progression and Guardian identity with cinematic restraint.

Communicate primary Guardian type and inherited techniques without revealing every future pattern.

Use short transitions and appropriate visual hierarchy.

No unnecessary long waits.

## Screen 7 — Pause

Implement a professional pause overlay that preserves the game's identity.

Ensure gameplay pauses, navigation works, focus is correct, and resume/restart/title behavior follows existing rules.

Prevent input leakage, double confirmation and hidden focus bugs.

## Screen 8 — Victory and defeat

Replace raw result text with a deliberate completion/failure presentation.

Preserve official restart, title and ending behavior.

Use suitable typography, atmosphere and transitions.

Do not invent rewards, shops or permanent progression.

Ensure game state is correctly cleaned up.

## Screen 9 — In-game presentation polish

Improve only existing, appropriate presentation elements:

- Guardian introductions.
- Stage labels.
- Technique acquisition.
- Inheritance changes.
- Interaction prompts.
- Combat warning readability.
- Appropriate HUD responses.
- Short transitions.
- Relevant hit/skill presentation.

Do not introduce new gameplay or clutter the battlefield.

## New UI assets

Allowed when necessary:

- Game-style button frames.
- UI panels.
- Borders and separators.
- Technique icons.
- Health ornaments.
- Selection indicators.
- Transition masks.
- UI-only visual effects.

Assets must belong to K4RMA's actual visual identity.

Use source-derived or deterministic creation when appropriate. Generated UI artwork requires review for stylistic, transparency and resolution consistency.

Do not alter canonical character artwork.

Do not copy protected material from reference games.

## Technical implementation

Inspect existing UI framework, prefabs, hierarchy and input.

Prefer:

- Reusable prefabs or UI components.
- Consistent styling/tokens.
- Separated state and presentation.
- Existing game-state bindings.
- Proper layout anchors.
- Efficient rendering.
- Correct keyboard navigation.
- Accessible readable text.
- Safe screen transitions.

Avoid:

- Monolithic UI managers.
- Duplicate state sources.
- Excessive Canvas rebuilds.
- Unnecessary framework migration.
- Overly complicated animation controllers.
- Massive unrelated scene rewrites.

## Accessibility and quality

- Maintain readable text sizes and contrast.
- Never communicate essential state by color alone.
- Use appropriate labels/icons.
- Avoid excessive flashes and shake.
- Preserve correct Korean glyph rendering.
- Check common Windows desktop resolutions.
- Avoid clipping, overlap, invisible clickable regions, and broken navigation.
- Keep important actions fast and predictable.

## Development order

1. Discover current UI and approved aesthetics.
2. Establish lightweight design primitives.
3. Upgrade title/buttons.
4. Upgrade story/controls.
5. Upgrade combat HUD.
6. Upgrade technique personalization.
7. Upgrade stage transitions.
8. Upgrade pause/results.
9. Polish common transitions and interaction.
10. Perform integrated gameplay/UI regression.

Make a local Conventional Commit after each coherent, validated UI feature or fix.

## Acceptance criteria

A screen is complete only when it:

- Is actually implemented in the Unity project.
- Looks stylistically consistent.
- Has functional navigation.
- Displays correct game data.
- Supports real user interaction.
- Does not break other screens or gameplay.
- Has been checked with accessible rendering and runtime tools.

Do not call a static design mockup a finished game interface.

Do not call visual quality validated without actual visual evidence.

## Final Task 2 deliverable

A coherent working interface across K4RMA's required screens, integrated with the existing game, committed locally in logical increments, with a final verification and merge-risk report.

No Git pushes.