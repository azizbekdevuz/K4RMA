# Player crouch

- Tool: built-in image_gen, transparent output.
- Reference: `Assets/K4RMA/Art/Resources/K4RMAArtwork/Characters/Player/Actions.png`.
- Output: `Assets/K4RMA/Art/Resources/K4RMAArtwork/Characters/Player/Actions.png`의 `Crouch` 포즈.
- 생성 원본 1254 × 1254를 209 × 209로 아틀라스에 패킹했다. 기존 19개 포즈의 픽셀은 유지했다.
- Unity import: Actions 시트의 Crouch, 130 pixels per unit, foot pivot (0.55, 0.108). 원본 대비 1/6 크기로 패킹해 게임 표시 크기를 유지한다.
- Hold S / Down while grounded to show the new pose. Jump and combat animations retain priority.
- P toggles test invincibility without a HUD status label; new runs start with it disabled. Room changes preserve it. Fall deaths still apply.
- Crouching changes the displayed pose and stops horizontal movement; collider dimensions remain unchanged.

## Final prompt

Use case: stylized-concept. Asset: one transparent 2D game crouch sprite for K4RMA. Input image is a character identity and style reference sheet, not a layout to reproduce. Create ONE single full body pose of EXACTLY this black-haired ponytailed young swordsman facing RIGHT, navy short sleeved kimono, charcoal hakama, ivory rope belt and sash, dark sandals, sheathed katana at waist. Deep balanced squat, hips lowered, both knees bent, both sandal soles on same horizontal ground line, torso slightly forward, one hand holding scabbard near waist and other hand on forward knee. Ready grounded crouch, not sitting cross legged, not kneeling. Normal head and limb proportions, crouch height 55-60 percent standing height. Match reference clean dark linework, restrained flat anime cel shading and muted palette; do not add texture or painterly rendering. Full character with ponytail, sandals and scabbard uncropped, centered on square canvas with transparent margin. Actual alpha transparency. No scenery, shadow, text, grid, extra poses or effects.
