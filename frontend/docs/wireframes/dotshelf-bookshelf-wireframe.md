# dotshelf Bookshelf Wireframe

Source SVG: `docs/wireframes/dotshelf-bookshelf-wireframe.svg`

## Plan

1. Define the bookshelf experience around `벽면2 정면뷰`, because detailed bookshelf manipulation belongs there rather than in `쿼터뷰`.
2. Show three core states on one board: bookshelf browsing, drag gap preview, and book focus.
3. Keep the wireframe aligned with the Unity phase rules: 4x4 shelf sections, slot-based book placement, turntable shelf switching, and preview-only drag state.

## Decisions Needed

- Cross-shelf edge hold threshold: the current wireframe uses `0.6s`, matching the phase guide recommendation.
- Section alignment controls: the wireframe exposes left, center, and right alignment after tapping empty section space.
- Visitor mode: edit controls are hidden, while public book information remains readable.

## Screen A: 벽면2 정면뷰

- Main shelf is centered as a 4x4 grid.
- Top bar includes room return, edit mode, and add shelf entry.
- Left rail lists shelves and reflects turntable-based switching.
- Side panel documents section actions:
  - Empty section tap opens alignment controls.
  - Book tap opens `책 포커싱 뷰`.
  - Book drag enters slot preview.
  - Wheel or pinch controls zoom.
  - Empty-space drag controls camera pan while zoomed.

## Screen B: Drag Gap Preview

- Dragged book stays visually separate from the target slot.
- Ghost book marks the future insertion slot.
- Neighboring books spread around the ghost position.
- Cross-shelf pending state is shown at the left/right edge with directional progress.
- Domain data must not change during this preview state.

## Screen C: 책 포커싱 뷰

- Focused book moves forward and presents the cover.
- Detail panel contains cover, title, author, publisher, reading status, rating, one-line review, visibility, review entry, and back action.
- MY_ROOM enables editing controls.
- VISITOR_MODE hides or disables editing controls and shows only allowed public content.

## Implementation Notes

- Book placement is saved by `shelfId`, `sectionRow`, `sectionCol`, and `slotIndex`.
- Preview state changes view transforms and ghost objects only.
- Domain placement and API persistence happen only after valid drop commit.
- Drag behavior remains slot-based, not physics-based.
- Cross-shelf drag keeps the dragged book under a world-fixed `DragAnchor`; shelf objects rotate via turntable.
- Mock API and real API should differ only by base URL.

## Verification Method

- Import the SVG into Figma and confirm the three frames remain legible at desktop canvas scale.
- Compare interaction labels against Phase 2 through Phase 6 rules.
- During Unity implementation, validate that each wireframe state maps to a concrete state transition:
  - `Idle -> Pressed -> Focused`
  - `Idle -> Pressed -> Dragging -> Preview`
  - `Preview -> CrossShelfPending -> CrossShelfTransition -> Preview`
  - `Dropping -> Idle`
  - `Returning -> Idle`
