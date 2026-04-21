# AGENTS.md

This repository is a Unity-based 3D book room MVP.

## Required Direction
- Follow the phase documents under `C:\SSAFY\Unity\Unity_codex\.codex\codex_phase_prompts_improved`.
- Keep the Unity application architecture under `Assets/App/...`.
- Do not put `UnityEngine` references in the Domain layer.
- Feature code should go through Application/UseCase boundaries before touching Infrastructure/API code.
- Drag and placement behavior must remain slot-based, not physics-based.
- Preview state must not commit Domain data.
- Mock API and real API paths should differ only by base URL.

## Output Order For Future Work
1. Plan
2. Decisions needed
3. File changes after approval or explicit implementation request
4. Changed file summary
5. Verification method

