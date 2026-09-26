# tagtag pitch

Nine slides in the app’s visual style, with one dedicated live-demo slide.

- [Editable PowerPoint](tagtag-pitch.pptx): narration is included in the speaker notes.
- [Presentation PDF](tagtag-pitch.pdf): preserves the rendered typography and artwork on any computer.
- [Four-minute script](VIDEO_SCRIPT.md): exact timestamps, demo actions, and fallback narration.
- [Slide plan](DECK_PLAN.md): structure and visual direction.

Slide 6 reserves **1:55–3:10** for the live demo. Return to slide 7 at 3:10 and finish by 4:00.

## Fonts

For editing or presenting the PowerPoint, install the app’s [Shadows Into Light](../../../Assets/Resources/Tagtag/Fonts/ShadowsIntoLight.ttf), [Instrument Sans Regular](../../../Assets/Resources/Tagtag/Fonts/InstrumentRegular.ttf), and [Instrument Sans Semibold](../../../Assets/Resources/Tagtag/Fonts/InstrumentSemibold.ttf). The PDF uses rendered slide images and needs no installed fonts; its text is not selectable.

## Evidence

The screenshots are labelled simulator/UI previews. Community trails are a proposed pilot. Optional Sepolia souvenirs are described as disabled pending verification. POAP’s historical figure of 46,210 refers to issuers. Sources are in the script and relevant speaker notes.

## Rebuild

`build.mjs` uses the Codex presentation runtime. Set `RUNTIME_NODE_MODULES` to the runtime’s `node_modules`, `SKILL_DIR` to the presentations skill directory, and `RUNTIME_PYTHON` to its Python executable, then run the builder from the repository root with the runtime’s Node executable. Set `PITCH_BUILD_DIR` to a fresh temporary directory for each build. Finalization validates the PowerPoint before writing it to that directory’s `output/` folder.
